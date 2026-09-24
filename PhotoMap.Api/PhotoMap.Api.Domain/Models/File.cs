namespace PhotoMap.Api.Domain.Models;

public class File
{
    public long Id { set; get; }
    public string FileName { set; get; } = null!;
    public string FullPath { set; get; } = null!;
    public DateTimeOffset AddedOn { set; get; }
    public long Size { set; get; }
}
