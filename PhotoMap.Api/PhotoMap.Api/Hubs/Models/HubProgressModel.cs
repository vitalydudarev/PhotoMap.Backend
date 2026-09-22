namespace PhotoMap.Api.Hubs.Models;

public record HubProgressModel(long SourceId, string Status, int Processed, int Failed, int Total);
