using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using Photo = PhotoMap.Api.Domain.Models.Photo;

namespace PhotoMap.Api.Database.Repositories;

public class PhotoRepository : IPhotoRepository
{
    private readonly PhotoMapContext _context;

    public PhotoRepository(PhotoMapContext context)
    {
        _context = context;
    }
    
    /// <summary>
    /// Saves the photos in one go: they are all saved, or none of them is.
    /// </summary>
    public async Task AddRangeAsync(IReadOnlyCollection<Photo> photos)
    {
        await _context.Photos.AddRangeAsync(photos.Select(ModelToEntity));
        await _context.SaveChangesAsync();
    }
    
    public async Task<Photo?> GetAsync(long id)
    {
        var photoEntity = await _context.Photos.FindAsync(id);
        
        return photoEntity != null ? EntityToModel(photoEntity) : null;
    }

    /// <summary>
    /// Of the given files of a photo source, the ones already saved for the user.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId, IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToArray();

        var savedExternalIds = await _context.Photos
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.ExternalId != null &&
                        ids.Contains(a.ExternalId))
            .Select(a => a.ExternalId!)
            .ToListAsync();

        return savedExternalIds.ToHashSet();
    }

    public async Task<IEnumerable<Photo>> GetByUserIdAsync(long userId, PhotoFilter filter, int top, int skip,
        PhotoSortOrder sortOrder)
    {
        var photos = GetUserPhotos(userId, filter);

        // the ID orders photos taken within the same second, which would otherwise be free to swap places
        // between one page and the next
        var sortedPhotos = sortOrder == PhotoSortOrder.Desc
            ? photos.OrderByDescending(a => a.DateTimeTaken).ThenByDescending(a => a.Id)
            : photos.OrderBy(a => a.DateTimeTaken).ThenBy(a => a.Id);

        var page = await sortedPhotos
            .Skip(skip)
            .Take(top)
            .Include(a => a.Tags)
            .ThenInclude(a => a.Tag)
            .AsSplitQuery()
            .ToListAsync();

        return page.Select(EntityToModel);
    }

    public async Task<int> GetTotalCountByUserIdAsync(long userId, PhotoFilter filter)
    {
        return await GetUserPhotos(userId, filter).CountAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetGeotaggedAsync(long userId)
    {
        // every one of them at once, so the EXIF and the rest are left in the database
        return await _context.Photos
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.HasGps && a.DeletedOn == null)
            .OrderBy(a => a.DateTimeTaken)
            .ThenBy(a => a.Id)
            .Select(a => new Photo
            {
                Id = a.Id,
                UserId = a.UserId,
                FileName = a.FileName,
                Path = a.Path,
                DateTimeTaken = a.DateTimeTaken,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                HorizontalPositioningError = a.HorizontalPositioningError,
                HasGps = a.HasGps
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetYearsAsync(long userId)
    {
        return await _context.Photos
            .Where(a => a.UserId == userId)
            .Select(a => a.DateTimeTaken.Year)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// The photos not yet put in their categories by the given version of the rules, in the order they were saved.
    /// </summary>
    public async Task<IReadOnlyList<Photo>> GetNotCategorizedAsync(int version, int top)
    {
        var photos = await _context.Photos
            .Where(a => a.CategoriesVersion < version)
            .OrderBy(a => a.Id)
            .Take(top)
            .ToListAsync();

        return photos.Select(EntityToModel).ToList();
    }

    /// <summary>
    /// Replaces the categories of the photos, and records the version of the rules they were put in them by. The
    /// photos are all updated, or none of them is.
    /// </summary>
    public async Task SetCategoriesAsync(IReadOnlyDictionary<long, IReadOnlyCollection<PhotoCategory>> categoriesByPhotoId,
        int version)
    {
        var photoIds = categoriesByPhotoId.Keys.ToArray();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.PhotoCategories.Where(a => photoIds.Contains(a.PhotoId)).ExecuteDeleteAsync();

        await _context.PhotoCategories.AddRangeAsync(categoriesByPhotoId.SelectMany(a =>
            a.Value.Select(category => new PhotoCategoryEntity { PhotoId = a.Key, Category = category })));
        await _context.SaveChangesAsync();

        await _context.Photos
            .Where(a => photoIds.Contains(a.Id))
            .ExecuteUpdateAsync(a => a.SetProperty(photo => photo.CategoriesVersion, version));

        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<Photo>> GetDuplicatesAsync(long userId)
    {
        var photos = await _context.Photos
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.DuplicateGroupId != null)
            .ToListAsync();

        // the groups are ordered by their first photo, which the database cannot do without a join of its own
        return photos
            .GroupBy(a => a.DuplicateGroupId)
            .OrderBy(a => a.Min(b => b.DateTimeTaken))
            .ThenBy(a => a.Key)
            .SelectMany(a => a.OrderBy(b => b.Id))
            .Select(EntityToModel)
            .ToList();
    }

    public async Task<IReadOnlyList<PhotoDuplicateKey>> GetDuplicateKeysAsync()
    {
        return await _context.Photos
            .AsNoTracking()
            .Select(a => new PhotoDuplicateKey(a.Id, a.UserId, a.ContentHash, a.DeletedOn != null, a.DuplicateGroupId))
            .ToListAsync();
    }

    public async Task SetDuplicateGroupsAsync(IReadOnlyDictionary<long, long?> duplicateGroupIdsByPhotoId)
    {
        // a statement for each group rather than for each photo
        foreach (var group in duplicateGroupIdsByPhotoId.GroupBy(a => a.Value, a => a.Key))
        {
            var duplicateGroupId = group.Key;

            foreach (var photoIds in group.Chunk(1000))
            {
                await _context.Photos
                    .Where(a => photoIds.Contains(a.Id))
                    .ExecuteUpdateAsync(a => a.SetProperty(b => b.DuplicateGroupId, duplicateGroupId));
            }
        }
    }

    /// <summary>
    /// Marks a photo of the user as deleted, or no longer deleted when <paramref name="deletedOn"/> is null.
    /// </summary>
    /// <returns>false if the user has no such photo.</returns>
    public async Task<bool> SetDeletedOnAsync(long userId, long photoId, DateTimeOffset? deletedOn)
    {
        var updated = await _context.Photos
            .Where(a => a.UserId == userId && a.Id == photoId)
            .ExecuteUpdateAsync(a => a.SetProperty(photo => photo.DeletedOn, deletedOn));

        return updated > 0;
    }

    public async Task<IReadOnlyList<PhotoTag>> GetTagsAsync(long userId)
    {
        // a tag taken off its last photo is left behind, unused, and offered again once a photo has it
        var tags = await _context.Tags
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Photos.Any())
            .Select(a => new PhotoTag(a.Id, a.Name, a.Photos.Count))
            .ToListAsync();

        return tags.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Replaces the tags of a photo of the user, in one go: the new tags are made, and the photo given them, or
    /// nothing is.
    /// </summary>
    public async Task<IReadOnlyList<string>?> SetTagsAsync(long userId, long photoId, IReadOnlyCollection<string> names)
    {
        if (!await _context.Photos.AnyAsync(a => a.UserId == userId && a.Id == photoId))
        {
            return null;
        }

        var normalizedNames = names.Select(TagNames.Normalize).ToArray();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var tags = await _context.Tags
            .Where(a => a.UserId == userId && normalizedNames.Contains(a.NormalizedName))
            .ToListAsync();

        var newTags = names
            .Where(name => tags.All(tag => tag.NormalizedName != TagNames.Normalize(name)))
            .Select(name => new TagEntity { UserId = userId, Name = name, NormalizedName = TagNames.Normalize(name) })
            .ToList();

        await _context.Tags.AddRangeAsync(newTags);
        await _context.SaveChangesAsync();
        tags.AddRange(newTags);

        // only what changed, the tags the photo keeps stay as they are
        var tagIds = tags.Select(a => a.Id).ToHashSet();
        var photoTags = await _context.PhotoTags.Where(a => a.PhotoId == photoId).ToListAsync();

        _context.PhotoTags.RemoveRange(photoTags.Where(a => !tagIds.Contains(a.TagId)));
        await _context.PhotoTags.AddRangeAsync(tagIds
            .Where(id => photoTags.All(a => a.TagId != id))
            .Select(id => new PhotoTagEntity { PhotoId = photoId, TagId = id }));
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return tags.Select(a => a.Name).OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Adds tags to photos of the user, in one go: the new tags are made, and the photos given them, or nothing is.
    /// </summary>
    public async Task<AddedTags> AddTagsAsync(long userId, IReadOnlyCollection<long> photoIds,
        IReadOnlyCollection<string> names)
    {
        var found = await _context.Photos.CountAsync(a => a.UserId == userId && photoIds.Contains(a.Id));
        if (found != photoIds.Count)
        {
            return AddedTags.PhotoNotFound;
        }

        var normalizedNames = names.Select(TagNames.Normalize).ToArray();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var tags = await _context.Tags
            .Where(a => a.UserId == userId && normalizedNames.Contains(a.NormalizedName))
            .ToListAsync();

        var newTags = names
            .Where(name => tags.All(tag => tag.NormalizedName != TagNames.Normalize(name)))
            .Select(name => new TagEntity { UserId = userId, Name = name, NormalizedName = TagNames.Normalize(name) })
            .ToList();

        var photoTags = await _context.PhotoTags
            .Where(a => photoIds.Contains(a.PhotoId))
            .Select(a => new { a.PhotoId, a.TagId })
            .ToListAsync();
        var tagIdsByPhoto = photoIds.ToDictionary(id => id,
            id => photoTags.Where(a => a.PhotoId == id).Select(a => a.TagId).ToHashSet());
        var existingTagIds = tags.Select(a => a.Id).ToList();

        // the new tags are on no photo yet, so each of them is one more on every photo
        if (tagIdsByPhoto.Values.Any(ids => ids.Union(existingTagIds).Count() + newTags.Count > TagNames.MaxPerPhoto))
        {
            return AddedTags.TooManyTags;
        }

        await _context.Tags.AddRangeAsync(newTags);
        await _context.SaveChangesAsync();
        tags.AddRange(newTags);

        await _context.PhotoTags.AddRangeAsync(tagIdsByPhoto.SelectMany(photo => tags
            .Where(tag => !photo.Value.Contains(tag.Id))
            .Select(tag => new PhotoTagEntity { PhotoId = photo.Key, TagId = tag.Id })));
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        var photoNames = await _context.Photos
            .AsNoTracking()
            .Where(a => photoIds.Contains(a.Id))
            .Select(a => new { a.Id, Names = a.Tags.Select(b => b.Tag!.Name).ToList() })
            .ToListAsync();

        return AddedTags.Added(photoNames.ToDictionary(a => a.Id,
            a => (IReadOnlyList<string>)a.Names.OrderBy(b => b, StringComparer.CurrentCultureIgnoreCase).ToList()));
    }

    public async Task<IReadOnlyList<TaggedPhoto>> GetTaggedPhotosAsync(long userId, PhotoNeighborhood neighborhood,
        int top)
    {
        var from = neighborhood.From;
        var to = neighborhood.To;
        var tagged = _context.Photos
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.Tags.Any());

        var photos = await ToTaggedPhotos(tagged
                .Where(a => a.DateTimeTaken >= from && a.DateTimeTaken <= to)
                .OrderBy(a => a.Id)
                .Take(top))
            .ToListAsync();

        if (neighborhood.Box is { } box)
        {
            var latitude = (box.MinLatitude + box.MaxLatitude) / 2;
            var longitude = (box.MinLongitude + box.MaxLongitude) / 2;

            // a place visited again and again would otherwise bring every photo ever taken there
            var nearby = await ToTaggedPhotos(tagged
                    .Where(a => a.HasGps &&
                                a.Latitude >= box.MinLatitude && a.Latitude <= box.MaxLatitude &&
                                a.Longitude >= box.MinLongitude && a.Longitude <= box.MaxLongitude)
                    .OrderBy(a => (a.Latitude!.Value - latitude) * (a.Latitude!.Value - latitude) +
                                  (a.Longitude!.Value - longitude) * (a.Longitude!.Value - longitude))
                    .Take(top))
                .ToListAsync();

            photos.AddRange(nearby.Where(a => photos.All(b => b.Id != a.Id)));
        }

        return photos;
    }

    private static IQueryable<TaggedPhoto> ToTaggedPhotos(IQueryable<PhotoEntity> photos)
    {
        return photos.Select(a => new TaggedPhoto(a.Id, a.DateTimeTaken, a.Latitude, a.Longitude,
            a.Tags.Select(b => b.Tag!.Name).ToList()));
    }

    /// <summary>
    /// Deletes the photos a photo source was the origin of, for one user.
    /// </summary>
    /// <returns>The thumbnail files of the deleted photos, which the caller removes from the storage.</returns>
    public Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId)
    {
        return DeleteAsync(_context.Photos.Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId));
    }

    public Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId,
        bool fromOtherUploads)
    {
        return DeleteAsync(_context.Photos.Where(a =>
            a.UserId == userId && a.PhotoSourceId == photoSourceId && a.FromOtherUploads == fromOtherUploads));
    }

    private static async Task<IReadOnlyCollection<string>> DeleteAsync(IQueryable<PhotoEntity> photos)
    {
        var thumbnailPaths = await photos
            .Select(a => new { a.ThumbnailSmallFilePath, a.ThumbnailLargeFilePath })
            .ToListAsync();

        await photos.ExecuteDeleteAsync();

        return thumbnailPaths
            .SelectMany(a => new[] { a.ThumbnailSmallFilePath, a.ThumbnailLargeFilePath })
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(a => a!)
            .ToList();
    }

    private IQueryable<PhotoEntity> GetUserPhotos(long userId, PhotoFilter filter)
    {
        var photos = _context.Photos.Where(a => a.UserId == userId);

        if (!filter.IncludeOtherUploads)
        {
            photos = photos.Where(a => !a.FromOtherUploads);
        }

        if (filter.PhotoSourceIds.Count > 0)
        {
            photos = photos.Where(a => filter.PhotoSourceIds.Contains(a.PhotoSourceId));
        }

        if (filter.Years.Count > 0)
        {
            photos = photos.Where(a => filter.Years.Contains(a.DateTimeTaken.Year));
        }

        if (filter.Categories.Count > 0)
        {
            var categories = filter.Categories
                .Where(a => a != PhotoCategory.Other && a != PhotoCategory.Deleted)
                .ToArray();
            var other = filter.Categories.Contains(PhotoCategory.Other);
            var deleted = filter.Categories.Contains(PhotoCategory.Deleted);

            // a photo marked as deleted is in the deleted ones only, whatever its other categories
            photos = photos.Where(a => (a.DeletedOn == null &&
                                        (a.Categories.Any(c => categories.Contains(c.Category)) ||
                                         (other && !a.Categories.Any()))) ||
                                       (deleted && a.DeletedOn != null));
        }
        else
        {
            photos = photos.Where(a => a.DeletedOn == null);
        }

        if (filter.HasGps != null)
        {
            photos = photos.Where(a => a.HasGps == filter.HasGps.Value);
        }

        if (filter.FiltersByTags)
        {
            var tagIds = filter.TagIds?.ToArray() ?? [];
            var untagged = filter.Untagged;

            photos = photos.Where(a => a.Tags.Any(t => tagIds.Contains(t.TagId)) || (untagged && !a.Tags.Any()));
        }

        return photos;
    }

    private static Photo EntityToModel(PhotoEntity photoEntity)
    {
        return new Photo
        {
            Id = photoEntity.Id,
            UserId = photoEntity.UserId,
            ThumbnailSmallFilePath = photoEntity.ThumbnailSmallFilePath,
            ThumbnailLargeFilePath = photoEntity.ThumbnailLargeFilePath,
            FileName = photoEntity.FileName,
            DateTimeTaken = photoEntity.DateTimeTaken,
            Latitude = photoEntity.Latitude,
            Longitude = photoEntity.Longitude,
            HorizontalPositioningError = photoEntity.HorizontalPositioningError,
            HasGps = photoEntity.HasGps,
            ExifString = photoEntity.ExifString,
            Path = photoEntity.Path,
            ExternalId = photoEntity.ExternalId,
            ContentHash = photoEntity.ContentHash,
            AddedOn = photoEntity.AddedOn,
            PhotoSourceId = photoEntity.PhotoSourceId,
            DeletedOn = photoEntity.DeletedOn,
            DuplicateGroupId = photoEntity.DuplicateGroupId,
            FromOtherUploads = photoEntity.FromOtherUploads,
            // only loaded along with the photos of the gallery
            Tags = photoEntity.Tags
                .Where(a => a.Tag != null)
                .Select(a => a.Tag!.Name)
                .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase)
                .ToList()
        };
    }

    private static PhotoEntity ModelToEntity(Photo photo)
    {
        return new PhotoEntity
        {
            UserId = photo.UserId,
            ThumbnailSmallFilePath = photo.ThumbnailSmallFilePath,
            ThumbnailLargeFilePath = photo.ThumbnailLargeFilePath,
            FileName = photo.FileName,
            DateTimeTaken = photo.DateTimeTaken,
            Latitude = photo.Latitude,
            Longitude = photo.Longitude,
            HorizontalPositioningError = photo.HorizontalPositioningError,
            HasGps = photo.HasGps,
            ExifString = photo.ExifString,
            Path = photo.Path,
            ExternalId = photo.ExternalId,
            ContentHash = photo.ContentHash,
            AddedOn = photo.AddedOn,
            PhotoSourceId = photo.PhotoSourceId,
            FromOtherUploads = photo.FromOtherUploads
        };
    }
}
