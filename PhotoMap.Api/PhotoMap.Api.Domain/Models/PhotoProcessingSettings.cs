namespace PhotoMap.Api.Domain.Models;

public class PhotoProcessingSettings
{
    public int[] Sizes { get; set; } = null!;

    /// <summary>
    /// How many processed photos are saved to the database at a time.
    /// </summary>
    public int SaveBatchSize { get; set; } = 50;

    /// <summary>
    /// How long a processed photo waits for others to be saved with it. The page it belongs to is finished only
    /// when it has been saved, so this also holds up the last page of a run by as much.
    /// </summary>
    public TimeSpan SaveBatchMaxWait { get; set; } = TimeSpan.FromSeconds(1);
}