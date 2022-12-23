namespace PhotoMap.Api.Domain.Models;

public class FileInfo
{
    public long Id { get; set; }
    public string FileName { get; set; }
    public long Size { get; set; }
    public DateTimeOffset AddedOn { get; set; }
}
