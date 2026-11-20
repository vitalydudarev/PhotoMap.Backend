namespace PhotoMap.Api.Hubs.Models;

public record HubProgressModel(long SourceId, string Status, int Processed, int Failed, int Total)
{
    /// <summary>
    /// How far a run retrying the failed files has got, null for any other run.
    /// </summary>
    public HubRetryModel? Retry { get; init; }

    /// <summary>
    /// The files found so far while the run is loading the list of the files it takes, null once it has loaded it.
    /// </summary>
    public int? Listed { get; init; }
}
