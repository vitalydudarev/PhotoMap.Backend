namespace PhotoMap.Api.Hubs.Models;

public record HubProgressModel(long SourceId, string Status, int Processed, int Failed, int Total)
{
    /// <summary>
    /// How far a run retrying the failed files has got, null for any other run.
    /// </summary>
    public HubRetryModel? Retry { get; init; }
}
