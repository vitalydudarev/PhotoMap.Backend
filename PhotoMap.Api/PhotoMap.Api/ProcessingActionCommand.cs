namespace PhotoMap.Api;

public class ProcessingActionCommand
{
    public required long UserId { get; set; }
    public required long PhotoSourceId { get; set; }
}