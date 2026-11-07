using PhotoMap.Shared.Messaging;
using PhotoMap.Worker;

// PhotoMap.Worker in a process of its own: it takes the image processing requests from Kafka and sends the results
// back through it. The workers share the partitions of the requests as one consumer group, so as many of them as
// there are partitions process at once; one more waits for a partition, taking it over when a worker stops.
//
// It reads the downloaded files, and leaves the thumbnails, at the paths the API gives: it must see the same disk.
//
//   dotnet run --project PhotoMap.Worker.Host
var builder = Host.CreateApplicationBuilder(args);

var kafkaSettings = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>() ?? new KafkaSettings();

builder.Services.AddWorker(builder.Configuration);
builder.Services.AddWorkerKafkaTransports(kafkaSettings);

builder.Build().Run();
