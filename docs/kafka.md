# Kafka

Kafka carries three kinds of message:

- **`image-processing-requests`**, pipeline messages. The API sends one per downloaded image to the Workers,
  which can run in processes of their own.
- **`photo-processed`**, pipeline messages. A Worker sends one per image back to the API, which saves the photo.
- **`photo-saved`**, a domain event. It is published through an outbox once a photo is in the database, for
  anyone who wants to react to it.

```
API: PhotoSourceProcessingService
   │  ProcessImageRequest { RequestId, FileName }  ──▶ [ image-processing-requests ]  key = RequestId (spread)
   ▼
Workers: ImageProcessingBackgroundService  (group photo-map.image-processors: PhotoMap.Worker.Host processes,
   │                                         and/or one inside the API)
   │  takes a batch → processes it in parallel → sends the results → acknowledges the batch
   │  thumbnails → staged files next to the download (claim-check)
   │  PhotoProcessed { RequestId, Image | Error }  ──▶ [ photo-processed ]  key = "{userId}:{photoSourceId}"
   ▼
API: ProcessedImageBackgroundService  (group photo-map.photo-processed-saver)
   │  reads a batch → saves photos + outbox rows in one transaction → reports each result to the waiting run
   │  through ProcessingRequests (RequestId → TaskCompletionSource) → deletes staged thumbnails → commits offsets
   ▼
outbox_messages ──OutboxRelayBackgroundService──▶ [ photo-saved ] ──▶ PhotoSavedLogConsumer
                                                  header message-id = outbox row id
```

| Piece | Where |
| --- | --- |
| Broker, topic creation, Kafka UI | `docker-compose.yml` |
| Pipeline message | `PhotoMap.Shared/Messages/PhotoProcessed.cs` |
| Event contract | `PhotoMap.Shared/Events/PhotoSavedEvent.cs` |
| Transports | `IMessageSender<T>` / `IMessageReceiver<T>`: `ChannelMessageTransport<T>` (in memory), `KafkaMessageSender<T>` / `KafkaMessageReceiver<T>` |
| Request/reply correlation | `ProcessingRequests` |
| Worker as its own process | `PhotoMap.Worker.Host` |
| Outbox table | `OutboxMessageEntity`, migration `AddOutboxMessages` |
| Relay | `OutboxRelay` (one round), `OutboxRelayBackgroundService` (the loop) |
| Producer | `PhotoMap.Shared/Messaging/KafkaMessagePublisher.cs` |
| Event consumer | `PhotoMap.Api/PhotoMap.Api/Services/PhotoSavedLogConsumer.cs` |
| Registration | `Startup.AddMessaging`, `MessagingServiceCollectionExtensions`, `AddWorkerKafkaTransports` |
| Settings | `Kafka` section of `appsettings*.json`; `ImageProcessing:InProcess`, `ImageProcessing:SendRetryDelay` |

## image-processing-requests: Workers in processes of their own

With Kafka enabled, the API only *sends* requests. Any number of Workers, in the consumer group
`photo-map.image-processors`, share them:

```sh
# the API without a Worker of its own (ImageProcessing:InProcess=false), then one or more of:
dotnet run --project PhotoMap.Worker.Host
```

`ImageProcessing:InProcess` (default `true`) keeps a Worker inside the API too. With Kafka enabled, that Worker
is just one more member of the same group. With Kafka disabled it's the only Worker, so the API refuses to start
if the setting is `false`.

- **The key is chosen for parallelism, not order.** Requests are keyed by `RequestId`, which is random, so one
  run's requests spread over all 3 partitions and up to 3 Workers process them at once. A fourth Worker would sit
  idle, waiting to take over a partition. Compare `photo-saved`, which is keyed by user and source because its
  consumers care about order.
- **Batch, then acknowledge.** A Worker takes up to `MaxDegreeOfParallelism` requests, processes them in
  parallel, sends every result, and only then commits the offsets. If it crashes mid-batch, the whole batch is
  redelivered (at-least-once).
- **Delete the input only after the output is sent.** A Worker deletes a downloaded file only once its result has
  been sent. If the file were deleted first, a redelivered request would find it gone. When a Worker stops, it
  finishes the images it's processing and acknowledges the batch only if every result went out.
- **A crash costs up to 45 seconds; a clean stop costs nothing.** A Worker that stops gracefully leaves the group,
  and its partitions move to the others at once. A Worker that crashes keeps its partitions until the broker stops
  hearing from it (`session.timeout.ms`, 45s by default), and until then its requests wait. Any Worker joining in
  the meantime waits too, because the group can't rebalance yet.
- **Requests survive having no Worker at all.** They wait in the topic, and the first Worker to start takes them.
- **Shared disk.** A request carries the downloaded file's path, not the photo (claim-check), and the Worker
  stages thumbnails next to it. Workers must see the same disk as the API: the same machine, or a shared volume
  mounted at the same path. Workers on other machines would need object storage.
- **Sending a request waits for Kafka.** If Kafka is down for longer than `Kafka:MessageTimeout` (30s), the
  send fails and so does the run. Resume it once Kafka is back. Unlike the in-memory queue, Kafka doesn't block a
  fast sender, but runs still wait for each page or chunk of photos to be processed before downloading the next.

## photo-processed: a pipeline over Kafka

Before, the Worker handed the API a `ProcessedImage` object holding the thumbnail bytes and the run's
`TaskCompletionSource`. Neither can cross a process boundary, so:

- **Correlation id instead of a callback.** The API registers each request's `TaskCompletionSource` in
  `ProcessingRequests` and sends only a `RequestId`. The Worker copies it into its `PhotoProcessed`, and the API
  matches the reply back by it. This is request/reply over messages. If a reply arrives after a restart, nothing is
  waiting for it, but the photo is still saved.
- **Claim-check instead of bytes.** The Worker writes the thumbnails to files next to the download, and the
  message says where they are. That keeps messages small whatever `PhotoProcessing:Sizes` is; Kafka rejects
  messages over about 1 MB by default. The API deletes the files once their batch is handled. The Worker and the
  API must see the same disk, which they do while they share a process.
- **Failures are messages too.** If an image can't be processed, the Worker sends a `PhotoProcessed` with an
  `Error`, and the API fails that file in its run.
- **Acknowledge after handling.** `IMessageReceiver<T>.Acknowledge()` commits the offsets only after a batch is
  saved. After a crash Kafka redelivers the batch, and photos that are already saved are skipped, so the API is
  idempotent. This is at-least-once delivery without an outbox: the Worker has no database to write one to.
- **Backpressure.** If Kafka is down, the Worker retries each send every `SendRetryDelay` (5s). Images waiting to
  send hold the Worker's processing slots, so the request queue fills up and the runs wait to hand over more. The
  whole pipeline slows to the transport's pace instead of piling up in memory.

With Kafka disabled, the same messages go through `ChannelMessageTransport<T>`, an in-memory queue, and
`Acknowledge()` does nothing. The code on both sides only sees `IMessageSender<T>` / `IMessageReceiver<T>`.

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
   enabled in `appsettings.Development.json` and disabled otherwise. While it's disabled, the Worker's results go
   through an in-memory queue, no events are saved to the outbox, and neither the relay nor the event consumer
   runs. **While it's enabled, photo processing needs the broker:** start Kafka first.
3. Process a photo source. Each image passes through `photo-processed`. Once it's saved, the log shows
   `Photo saved: ... (message 17, key 1:2, partition 1, offset 0)`.
4. Open Kafka UI at <http://localhost:8081> to browse both topics' messages (and their headers), their partitions
   and the two consumer groups.

`docker compose down` removes the broker together with the topics and their messages.

## Things to try

- **Share the work:** run the API with `ImageProcessing:InProcess=false` and start `PhotoMap.Worker.Host` twice.
  Each logs `assigned partitions`: one Worker gets 2 partitions and the other gets 1. Process a source and both
  log `Processed image ...`. `kg --describe --group photo-map.image-processors` shows which Worker owns which
  partition.
- **Crash a Worker:** `kill -9` one Worker while it processes. Its share stops for about 45s, until the broker
  gives up on it, then the other Worker logs `assigned partitions` for all 3 and processes the rest, including the
  batch the crashed Worker never acknowledged. Compare `Ctrl+C`, after which the partitions move at once.
- **No Worker at all:** stop every Worker and process a source. The requests wait in
  `image-processing-requests` (Kafka UI shows the lag), and the run waits with them. Start a Worker and it takes
  over from where the group left off.

- **Watch the pipeline:** `kc --topic photo-processed --property print.key=true` shows each image's result as the
  Worker sends it: the `RequestId`, the processed image with its `ThumbnailFiles`, or an `Error`.
- **Kafka down during a run:** `docker compose stop kafka`. The Worker logs `Failed to send the result ... trying
  again in 00:00:05`, processing stalls (backpressure), and the run's progress stops moving. Start Kafka again
  and everything resumes. Nothing is lost, because the Worker kept retrying.
- **The API crashing mid-batch:** kill the API (not a graceful stop) while photos are being saved. On restart,
  `kg --describe --group photo-map.photo-processed-saver` shows the lag. The uncommitted batch is consumed again,
  the photos already saved are logged as `already saved, skipping`, and the rest are saved.

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

- **API to Workers (`image-processing-requests`):** at-least-once. A batch is acknowledged only after all its
  results are sent, so a request a Worker crashed on goes to another Worker, or back to the same one when it
  restarts. A redelivered request whose result was already sent finds its file gone and sends a failure, which the
  API ignores because the run already has the result.
- **Worker to API (`photo-processed`):** at-least-once. The Worker retries each send until Kafka has it, and the
  API commits a batch's offsets only after saving it. A redelivered message is harmless, because photos that are
  already saved are skipped.
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
