#!/bin/bash
#
# Runs PhotoMap locally, all of it:
#   - Postgres (Postgres.app, started if it is not running)
#   - Kafka, Kafka UI and Seq (docker compose; Docker Desktop started if it is not running)
#   - the API, which runs the worker itself, or --workers N PhotoMap.Worker.Host processes instead
#   - the frontend (../PhotoMap.Frontend, or $FRONTEND_DIR)
#
#   ./run-all.command                  everything, the worker running in the API
#   ./run-all.command --workers 2      the API and 2 workers in processes of their own
#   ./run-all.command --no-frontend    everything but the frontend
#
# Double-click it in Finder, or run it from a terminal. The output of every process comes here, prefixed with its
# name, and goes to logs/<name>.log as well. Ctrl+C stops the API, the workers and the frontend; the containers keep
# running, for the next run to start fast (docker compose stop, to stop them too).
#
# Written for the bash of macOS, 3.2.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")" || exit 1
BACKEND_DIR=$PWD
FRONTEND_DIR=${FRONTEND_DIR:-"$BACKEND_DIR/../PhotoMap.Frontend"}
LOG_DIR="$BACKEND_DIR/logs"

API_URL="https://localhost:5002"
FRONTEND_URL="http://localhost:4200"
KAFKA_UI_URL="http://localhost:8081"
SEQ_URL="http://localhost:5341"

WORKERS=0
RUN_FRONTEND=1

usage() {
    sed -n '3,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --workers)
            [ $# -ge 2 ] && [[ "$2" =~ ^[0-9]+$ ]] || { usage; exit 1; }
            WORKERS=$2
            shift 2
            ;;
        --no-frontend) RUN_FRONTEND=0; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1"; usage; exit 1 ;;
    esac
done

step() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
info() { printf '    %s\n' "$*"; }
fail() { printf '\n\033[1;31merror: %s\033[0m\n' "$*" >&2; exit 1; }

# wait_for <what> <seconds> <command...>: runs the command every second until it succeeds
wait_for() {
    local what=$1 timeout=$2 start=$SECONDS
    shift 2
    until "$@" >/dev/null 2>&1; do
        if (( SECONDS - start > timeout )); then
            fail "timed out after ${timeout}s waiting for $what"
        fi
        sleep 1
    done
}

port_owner() {
    lsof -nP -iTCP:"$1" -sTCP:LISTEN 2>/dev/null | awk 'NR == 2 { print $1 " (pid " $2 ")" }'
}

require_free_port() {
    local port=$1 what=$2 owner
    owner=$(port_owner "$port")
    [ -z "$owner" ] || fail "port $port, for $what, is taken by $owner: is it running already? Stop it first."
}

http_ok() {
    [ "$(curl -sk -o /dev/null -w '%{http_code}' --max-time 5 "$1")" = "200" ]
}

# ---------------------------------------------------------------------------------------------------------------------
step "Checking the tools and ports"

for tool in dotnet docker curl lsof; do
    command -v "$tool" >/dev/null || fail "$tool is not installed, or not on the PATH"
done

if [ "$RUN_FRONTEND" = 1 ]; then
    command -v npm >/dev/null || fail "npm is not installed, or not on the PATH (or run with --no-frontend)"
    [ -f "$FRONTEND_DIR/package.json" ] ||
        fail "no frontend at $FRONTEND_DIR: set FRONTEND_DIR, or run with --no-frontend"
    FRONTEND_DIR=$(cd "$FRONTEND_DIR" && pwd)
    require_free_port 4200 "the frontend"
fi

require_free_port 5002 "the API"
info "ok"

# ---------------------------------------------------------------------------------------------------------------------
step "Starting the dependencies"

if ! docker info >/dev/null 2>&1; then
    info "starting Docker Desktop"
    open -a Docker || fail "Docker Desktop could not be started"
    wait_for "Docker" 120 docker info
fi

if ! nc -z localhost 5432 >/dev/null 2>&1; then
    info "starting Postgres.app"
    open -a Postgres || fail "Postgres is not running on port 5432, and Postgres.app could not be started"
    wait_for "Postgres on port 5432" 60 nc -z localhost 5432
fi
info "Postgres: localhost:5432"

docker compose up -d --quiet-pull >/dev/null 2>&1 || {
    docker compose up -d
    fail "docker compose up failed"
}

kafka_healthy() { [ "$(docker inspect -f '{{.State.Health.Status}}' photo-map-kafka 2>/dev/null)" = "healthy" ]; }
wait_for "Kafka" 120 kafka_healthy

# the topics are created by kafka-init, which exits once it is done
topics_created() {
    [ "$(docker compose ps -a --format '{{.State}} {{.ExitCode}}' kafka-init 2>/dev/null)" = "exited 0" ]
}
wait_for "the Kafka topics" 60 topics_created
wait_for "Seq" 60 http_ok "$SEQ_URL"
info "Kafka: localhost:9092, topics: $(docker exec photo-map-kafka /opt/kafka/bin/kafka-topics.sh \
    --bootstrap-server kafka:29092 --list 2>/dev/null | grep -v '^__' | tr '\n' ' ')"

# ---------------------------------------------------------------------------------------------------------------------
step "Building the backend"

mkdir -p "$LOG_DIR"
if ! dotnet build "$BACKEND_DIR/PhotoMap.Backend.sln" -nologo > "$LOG_DIR/build.log" 2>&1; then
    grep -E "error|Error" "$LOG_DIR/build.log" | sort -u | head -20
    fail "the backend does not build, see $LOG_DIR/build.log"
fi
info "ok"

if [ "$RUN_FRONTEND" = 1 ] && [ ! -d "$FRONTEND_DIR/node_modules" ]; then
    step "Installing the frontend packages"
    (cd "$FRONTEND_DIR" && npm ci) || fail "npm ci failed"
fi

# ---------------------------------------------------------------------------------------------------------------------
step "Starting the applications"

mkdir -p "$LOG_DIR"

# job control: every process started below is in a process group of its own, so that stopping it stops what it has
# started too (dotnet run its application, npm ng serve)
set -m

PIDS=()
NAMES=()
COLORS=(32 35 36 33 34 31)

# start <name> <directory> <command...>
start() {
    local name=$1 dir=$2 color=${COLORS[$(( ${#PIDS[@]} % ${#COLORS[@]} ))]}
    shift 2
    local prefix
    prefix=$(printf '\033[%sm%-9s\033[0m| ' "$color" "$name")

    : > "$LOG_DIR/$name.log"
    (
        cd "$dir" || exit 1
        # tee and awk ignore the SIGTERM that stops the process group: they carry the output until the application
        # closes it, so that what it logs while stopping is kept
        "$@" 2>&1 |
            (trap '' TERM; exec tee -a "$LOG_DIR/$name.log") |
            (trap '' TERM; exec awk -v prefix="$prefix" '{ print prefix $0; fflush() }')
    ) &

    PIDS+=($!)
    NAMES+=("$name")
}

stopping=0
stop_all() {
    (( stopping )) && return
    stopping=1
    trap - INT TERM

    printf '\n'
    step "Stopping the applications"

    local pid
    for pid in "${PIDS[@]}"; do
        kill -TERM -- "-$pid" 2>/dev/null
    done

    # graceful first: the workers finish their batches, the API records the runs it stops
    local start=$SECONDS
    for pid in "${PIDS[@]}"; do
        while kill -0 -- "-$pid" 2>/dev/null && (( SECONDS - start < 20 )); do
            sleep 0.5
        done
        kill -KILL -- "-$pid" 2>/dev/null
    done

    info "stopped. Postgres, Kafka, Kafka UI and Seq keep running (docker compose stop, to stop the containers)."
    info "logs: $LOG_DIR"
}

trap 'stop_all; exit 130' INT TERM
trap 'stop_all' EXIT

if (( WORKERS > 0 )); then
    # the requests go to the workers below, through Kafka
    export ImageProcessing__InProcess=false
fi

export ASPNETCORE_ENVIRONMENT=Development
start api "$BACKEND_DIR/PhotoMap.Api/PhotoMap.Api" \
    dotnet run --no-build --no-launch-profile -- --urls="$API_URL"

for (( i = 1; i <= WORKERS; i++ )); do
    start "worker-$i" "$BACKEND_DIR/PhotoMap.Worker.Host" dotnet run --no-build --no-launch-profile
done

if [ "$RUN_FRONTEND" = 1 ]; then
    start frontend "$FRONTEND_DIR" npm start
fi

wait_for "the API at $API_URL" 120 http_ok "$API_URL/swagger/v1/swagger.json"
if [ "$RUN_FRONTEND" = 1 ]; then
    wait_for "the frontend at $FRONTEND_URL" 180 http_ok "$FRONTEND_URL"
fi

step "Running"
[ "$RUN_FRONTEND" = 1 ] && info "Frontend   $FRONTEND_URL"
info "API        $API_URL/swagger"
info "Kafka UI   $KAFKA_UI_URL"
info "Seq        $SEQ_URL"
if (( WORKERS > 0 )); then
    info "Workers    $WORKERS PhotoMap.Worker.Host processes"
else
    info "Worker     in the API"
fi
info "Logs       $LOG_DIR"
info ""
info "Ctrl+C to stop."

# stops everything when one of the applications exits by itself, e.g. failing to start
while sleep 2; do
    for (( i = 0; i < ${#PIDS[@]}; i++ )); do
        if ! kill -0 "${PIDS[$i]}" 2>/dev/null; then
            fail "${NAMES[$i]} has exited, see $LOG_DIR/${NAMES[$i]}.log"
        fi
    done
done
