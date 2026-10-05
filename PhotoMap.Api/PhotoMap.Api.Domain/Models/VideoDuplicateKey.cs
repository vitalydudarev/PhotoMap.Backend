namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What tells the duplicates of a video, and the group of duplicates it is in now.
/// </summary>
public record VideoDuplicateKey(long Id, long UserId, string FileName, long Size, long? DuplicateGroupId);
