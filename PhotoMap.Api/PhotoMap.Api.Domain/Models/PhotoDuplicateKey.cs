namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What tells the copies of a photo, and the group of duplicates it is in now.
/// </summary>
/// <param name="ContentHash">SHA-256 of the file contents, null for a photo saved before it was taken.</param>
public record PhotoDuplicateKey(long Id, long UserId, string? ContentHash, bool IsDeleted, long? DuplicateGroupId);
