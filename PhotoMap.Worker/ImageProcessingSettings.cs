namespace PhotoMap.Worker;

public class ImageProcessingSettings
{
    /// <summary>
    /// How many images are decoded and resized at a time. Zero (the default) means one per processor:
    /// the work is CPU-bound, so that is as fast as the machine goes.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; }
}
