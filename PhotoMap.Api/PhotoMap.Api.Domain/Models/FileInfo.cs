namespace PhotoMap.Api.Domain.Models;

public class FileInfo
{
    public long Id { get; set; }
    public string FileName { get; set; } = null!;
    public long Size { get; set; }
    public DateTimeOffset AddedOn { get; set; }
}
