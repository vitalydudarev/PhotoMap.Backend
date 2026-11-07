# Kafka

When a batch of processed photos is saved, `ProcessedImageBackgroundService` also saves one `PhotoSavedEvent` per
photo to the `outbox_messages` table, **in the same transaction**. `OutboxRelayBackgroundService` publishes those
rows to the `photo-saved` topic and marks them published once Kafka has acknowledged them. `PhotoSavedLogConsumer`
reads the topic and logs each event. It shows how a consumer works and does nothing else yet.

```
                          one transaction
ProcessedImageBackgroundService ──▶ ┌ photos          ┐
                                    └ outbox_messages ┘
                                            │  OutboxRelayBackgroundService: reads pending rows → publishes →
                                            ▼  waits for the ack → marks them published
                          [ photo-saved: partition 0 | 1 | 2 ]  key = "{userId}:{photoSourceId}"
                                            │                   header message-id = outbox row id
                                            ▼
                          PhotoSavedLogConsumer (group photo-map.photo-saved-log)
```

| Piece | Where |
| --- | --- |
| Broker, topic creation, Kafka UI | `docker-compose.yml` |
| Event contract | `PhotoMap.Shared/Events/PhotoSavedEvent.cs` |
| Outbox table | `OutboxMessageEntity`, migration `AddOutboxMessages` |
| Writing to the outbox | `ProcessedImageBackgroundService` → `PhotoRepository.AddRangeAsync` |
| Relay | `OutboxRelay` (one round), `OutboxRelayBackgroundService` (the loop) |
| Producer | `PhotoMap.Shared/Messaging/KafkaMessagePublisher.cs` |
| Consumer | `PhotoMap.Api/PhotoMap.Api/Services/PhotoSavedLogConsumer.cs` |
| Registration | `Startup.AddKafka` |
| Settings | `Kafka` section of `appsettings*.json` |

## Why an outbox

Writing to the database and then publishing to Kafka is a *dual write*. The two aren't atomic, so one can happen
without the other:

- Publish **after** the commit, and the event is lost if the application stops in between, or if Kafka is down
  for longer than the producer keeps retrying.
- Publish **before** the commit, and if the commit then fails, the event reports a photo that doesn't exist.

With the outbox, the database is the only thing written in the request path. The event exists exactly when the
photo does, and the relay keeps retrying until Kafka has it, whether that takes seconds or days.

## Running it

1. Start Docker Desktop, then run `docker compose up -d` from the repository root.
2. Run the API in the Development environment. The `AddOutboxMessages` migration is applied on start. Kafka is
   enabled in `appsettings.Development.json` and disabled otherwise. While disabled, no events are saved to the
   outbox and neither the relay nor the consumer runs.
3. Process a photo source. The log shows
   `Photo saved: ... (message 17, key 1:2, partition 1, offset 0)` for each photo.
4. Open Kafka UI at <http://localhost:8080> to browse the topic's messages (and their headers), its partitions and
   the consumer group.

`docker compose down` removes the broker together with the topics and their messages.

## Things to try

All commands run in the broker container:

```sh
alias kt='docker exec -it photo-map-kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server kafka:29092'
alias kg='docker exec -it photo-map-kafka /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server kafka:29092'
alias kc='docker exec -it photo-map-kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server kafka:29092'
alias kp='docker exec -it photo-map-kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server kafka:29092'
```

and in the database:

```sql
SELECT id, key, created_on, published_on, attempts, last_error FROM outbox_messages ORDER BY id DESC LIMIT 20;
```

- **The outbox at work:** run `docker compose stop kafka` in the middle of a run. Photos are still saved and their
  rows pile up in `outbox_messages` with `published_on` empty. After `Kafka:MessageTimeout` (30s) the relay logs a
  failure, `attempts` goes up and `last_error` shows why, and it retries every 10s. Run `docker compose start kafka`
  and the waiting rows are published in order. Nothing is lost.
- **Stop the application instead:** stop the API in the middle of a run, with or without Kafka running. On the next
  start the relay publishes whatever was still waiting.
- **At-least-once:** if the application stops after Kafka acknowledged a message but before the row was marked
  published, the message is published again on the next start. The consumer then logs the same `message N` twice.
  That's why the `message-id` header exists: a consumer that must not handle an event twice keeps the ids it has
  handled.
- **Topics and partitions:** `kt --describe --topic photo-saved` shows the 3 partitions. Every event of one user's
  photo source has the same key, so all of them land on one partition, in order. Ordering is guaranteed only within
  a partition.
- **Consumer lag:** `kg --describe --group photo-map.photo-saved-log` shows each partition's committed offset, its
  end, and the lag between them.
- **Kafka keeps what has been read:** `kc --topic photo-saved --from-beginning --property print.key=true
  --property print.headers=true` reads every event again as a new consumer group, without disturbing the
  application's group. A queue would have deleted them.
- **Replay:** stop the API, then run
  `kg --group photo-map.photo-saved-log --reset-offsets --to-earliest --topic photo-saved --execute`. On the next
  start the consumer logs every event again.
- **A poison message:** `kp --topic photo-saved --property parse.key=true --property key.separator=:` and type
  `x:not json`. The consumer logs a warning, skips the message and commits it, so the partition isn't blocked.

## Delivery guarantees

- **Database to outbox:** atomic. Photos and their events are saved in one transaction, or neither is.
- **Outbox to Kafka:** at-least-once. The relay sends a batch of up to 200 rows at once and marks each row
  published only after Kafka acknowledges it (`Acks = All`, idempotent producer). Rows that fail stay pending and
  are retried. A message can be published twice: once if the application stops between the acknowledgement and
  the update, again if it restarts.
- **Kafka to consumer:** at-least-once. The offset is committed after an event is handled.
- **End to end:** every saved photo's event reaches the consumer at least once. A consumer that must see it
  *exactly* once deduplicates by `message-id`.
- **Order:** rows are published in id order, and one key always goes to one partition, so a consumer sees each
  user's photo-source events in the order they were saved. The exception is a batch where only some messages fail:
  the failed ones are retried after later ones were delivered. In practice it's usually all or nothing, because the
  broker is either up or down.

## Housekeeping and limits

- Published rows are kept for a day, to look into what was published, and then deleted. The cleanup runs hourly.
- A partial index (`ix_outbox_messages_pending`, `WHERE published_on IS NULL`) covers only the pending rows, so
  finding them stays fast however many published rows are kept.
- The relay polls every 30s, but a save wakes it immediately through `OutboxSignal`, so events usually go out
  within milliseconds.
- There is **one relay**. A second instance of the API would publish every row twice. The usual fix is to read
  pending rows with `SELECT ... FOR UPDATE SKIP LOCKED`, so that each relay skips the rows another one holds.
- An alternative to polling is change data capture: Debezium reads the Postgres write-ahead log and publishes the
  outbox rows itself. It needs no relay code but adds another service to run.
