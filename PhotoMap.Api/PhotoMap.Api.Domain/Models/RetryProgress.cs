namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// How far a run retrying the files of a photo source that failed has got, apart from the counters of the source.
/// </summary>
/// <param name="Total">The failed files the run retries, 0 until it has got them.</param>
/// <param name="Retried">The files retried so far, saved or failed again.</param>
/// <param name="Recovered">The files retried that have been saved this time.</param>
public record RetryProgress(int Total, int Retried, int Recovered);
