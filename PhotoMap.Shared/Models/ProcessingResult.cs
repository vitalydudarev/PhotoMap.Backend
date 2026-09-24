namespace PhotoMap.Shared.Models;

/// <summary>
/// How a downloaded file went through the processing pipeline.
/// </summary>
/// <param name="Succeeded">The photo was saved, or had already been saved.</param>
/// <param name="Error">Why processing failed, null when it succeeded.</param>
public sealed record ProcessingResult(bool Succeeded, string? Error)
{
    public static ProcessingResult Success { get; } = new(true, null);

    public static ProcessingResult Failed(string error) => new(false, error);
}
