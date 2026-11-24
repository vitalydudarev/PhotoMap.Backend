#!/bin/bash
#
# Runs PhotoMap locally without Docker and without Kafka:
#   - Postgres (Postgres.app, started if it is not running)
#   - the API from the SDK, Kafka disabled, the image processing worker running inside the API
#   - the frontend (../PhotoMap.Frontend, or $FRONTEND_DIR)
#
#   ./run-local.command
#
# Double-click it in Finder, or run it from a terminal. The output of every process comes here, prefixed with its
# name, and goes to logs/<name>.log as well. Ctrl+C stops the API and the frontend.
# Seq is not started: the API's logs go to the console and logs/api.log only.
#
# Written for the bash of macOS, 3.2.

set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")" || exit 1
BACKEND_DIR=$PWD
FRONTEND_DIR=${FRONTEND_DIR:-"$BACKEND_DIR/../PhotoMap.Frontend"}
LOG_DIR="$BACKEND_DIR/logs"

API_URL="https://localhost:5002"
FRONTEND_URL="http://localhost:4200"

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

require_free_port() {
    local port=$1 what=$2 owner
    owner=$(lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null | awk 'NR == 2 { print $1 " (pid " $2 ")" }')
    [ -z "$owner" ] || fail "port $port, for $what, is taken by $owner: is it running already? Stop it first."
}

http_ok() {
    [ "$(curl -sk -o /dev/null -w '%{http_code}' --max-time 5 "$1")" = "200" ]
}

# ---------------------------------------------------------------------------------------------------------------------
step "Checking the tools and ports"

for tool in dotnet npm curl lsof nc; do
    command -v "$tool" >/dev/null || fail "$tool is not installed, or not on the PATH"
done
[ -f "$FRONTEND_DIR/package.json" ] || fail "no frontend at $FRONTEND_DIR: set FRONTEND_DIR"
FRONTEND_DIR=$(cd "$FRONTEND_DIR" && pwd)

require_free_port 5002 "the API"
require_free_port 4200 "the frontend"
info "ok"

# ---------------------------------------------------------------------------------------------------------------------
step "Starting Postgres"

if ! nc -z localhost 5432 >/dev/null 2>&1; then
    info "starting Postgres.app"
    open -a Postgres || fail "Postgres is not running on port 5432, and Postgres.app could not be started"
    wait_for "Postgres on port 5432" 60 nc -z localhost 5432
fi
info "Postgres: localhost:5432"

# ---------------------------------------------------------------------------------------------------------------------
step "Building the backend"

mkdir -p "$LOG_DIR"
if ! dotnet build "$BACKEND_DIR/PhotoMap.Backend.sln" -nologo > "$LOG_DIR/build.log" 2>&1; then
    grep -E "error|Error" "$LOG_DIR/build.log" | sort -u | head -20
    fail "the backend does not build, see $LOG_DIR/build.log"
fi
info "ok"

if [ ! -d "$FRONTEND_DIR/node_modules" ]; then
    step "Installing the frontend packages"
    (cd "$FRONTEND_DIR" && npm ci) || fail "npm ci failed"
fi

# ---------------------------------------------------------------------------------------------------------------------
step "Starting the applications"

# job control: every process started below is in a process group of its own, so that stopping it stops what it has
# started too (dotnet run its application, npm ng serve)
set -m

PIDS=()
NAMES=()
COLORS=(32 35)

# start <name> <directory> <command...>
start() {
    local name=$1 dir=$2 color=${COLORS[$(( ${#PIDS[@]} % ${#COLORS[@]} ))]}
    shift 2
    local prefix
    prefix=$(printf '\033[%sm%-9s\033[0m| ' "$color" "$name")

    : > "$LOG_DIR/$name.log"
    (
        cd "$dir" || exit 1
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

    local start=$SECONDS
    for pid in "${PIDS[@]}"; do
        while kill -0 -- "-$pid" 2>/dev/null && (( SECONDS - start < 20 )); do
            sleep 0.5
        done
        kill -KILL -- "-$pid" 2>/dev/null
    done

    info "stopped. Postgres keeps running."
    info "logs: $LOG_DIR"
}

trap 'stop_all; exit 130' INT TERM
trap 'stop_all' EXIT

# no Kafka: appsettings.Development.json enables it, these turn it off and keep the worker inside the API
export ASPNETCORE_ENVIRONMENT=Development
export Kafka__Enabled=false
export ImageProcessing__InProcess=true

start api "$BACKEND_DIR/PhotoMap.Api/PhotoMap.Api" \
    dotnet run --no-build --no-launch-profile -- --urls="$API_URL"
start frontend "$FRONTEND_DIR" npm start

wait_for "the API at $API_URL" 120 http_ok "$API_URL/swagger/v1/swagger.json"
wait_for "the frontend at $FRONTEND_URL" 180 http_ok "$FRONTEND_URL"

step "Running"
info "Frontend   $FRONTEND_URL"
info "API        $API_URL/swagger"
info "Kafka      disabled, the worker runs in the API"
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
