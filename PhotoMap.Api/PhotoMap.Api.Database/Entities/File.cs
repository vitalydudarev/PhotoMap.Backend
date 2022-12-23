namespace PhotoMap.Api.Database.Entities;

public class File
{
    public long Id { set; get; }
    public string FileName { set; get; }
    public string FullPath { set; get; }
    public DateTimeOffset AddedOn { set; get; }
    public long Size { set; get; }
}
