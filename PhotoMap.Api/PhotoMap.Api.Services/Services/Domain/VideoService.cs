using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Database.Repositories;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class VideoService : IVideoService
{
    private readonly PhotoMapContext _context;
    private readonly VideoDuplicatesSignal _duplicatesSignal;
    private readonly GeocodingSignal _geocodingSignal;

    public VideoService(PhotoMapContext context, VideoDuplicatesSignal duplicatesSignal,
        GeocodingSignal geocodingSignal)
    {
        _context = context;
        _duplicatesSignal = duplicatesSignal;
        _geocodingSignal = geocodingSignal;
    }

    /// <summary>
    /// Saves the videos not saved yet: one already saved, or given twice, is saved once, rather than fail the
    /// others on the unique index of the IDs the photo source assigned.
    /// </summary>
    public async Task AddRangeAsync(IReadOnlyCollection<Video> videos)
    {
        var videosToSave = new List<Video>();

        foreach (var sourceVideos in videos.GroupBy(a => (a.UserId, a.PhotoSourceId)))
        {
            var (userId, photoSourceId) = sourceVideos.Key;
            var savedExternalIds = await GetSavedExternalIdsAsync(userId, photoSourceId, sourceVideos.Select(a => a.ExternalId));

            videosToSave.AddRange(sourceVideos
                .Where(a => !savedExternalIds.Contains(a.ExternalId))
                .DistinctBy(a => a.ExternalId));
        }

        if (videosToSave.Count == 0)
        {
            return;
        }

        var entities = videosToSave.Select(a => new VideoEntity
        {
            UserId = a.UserId,
            PhotoSourceId = a.PhotoSourceId,
            ExternalId = a.ExternalId,
            FileName = a.FileName,
            FolderPath = a.FolderPath,
            MimeType = a.MimeType,
            Size = a.Size,
            // timestamptz columns only accept UTC
            DateTimeTaken = a.DateTimeTaken.ToUniversalTime(),
            ExifDateTime = a.ExifDateTime?.ToUniversalTime(),
            ExifString = a.ExifString,
            Latitude = a.Latitude,
            Longitude = a.Longitude,
            PreviewFilePath = a.PreviewFilePath,
            PreviewContentType = a.PreviewContentType,
            AddedOn = a.AddedOn.ToUniversalTime()
        }).ToList();

        _context.Videos.AddRange(entities);

        try
        {
            await _context.SaveChangesAsync();
        }
        finally
        {
            // a run saves page after page with one context, which must not keep tracking every video it has saved,
            // nor, when the save has failed, try to save them again with the next page
            foreach (var entity in entities)
            {
                _context.Entry(entity).State = EntityState.Detached;
            }
        }

        // the videos are saved in no group of duplicates, until the duplicates are looked for again
        _duplicatesSignal.Notify();

        // and in no location, until the geocoding gets to the ones with GPS
        if (videosToSave.Any(a => a.Latitude != null && a.Longitude != null))
        {
            _geocodingSignal.Notify();
        }
    }

    public async Task<Video?> GetAsync(long id)
    {
        var entity = await _context.Videos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

        return entity == null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<Video>> GetByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths,
        IReadOnlyCollection<int> years, LocationFilter locations, int top, int skip, PhotoSortOrder sortOrder)
    {
        var videos = GetUserVideos(userId, folderPaths, years, locations).AsNoTracking();

        var ordered = sortOrder == PhotoSortOrder.Desc
            ? videos.OrderByDescending(a => a.DateTimeTaken).ThenByDescending(a => a.Id)
            : videos.OrderBy(a => a.DateTimeTaken).ThenBy(a => a.Id);

        var entities = await ordered.Skip(skip).Take(top).ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    /// <summary>
    /// What the videos the filter takes can still be narrowed down to, with how many videos each option takes: a query
    /// for each filter, which leaves that filter out.
    /// </summary>
    public async Task<VideoFacets> GetFacetsAsync(long userId, IReadOnlyCollection<string> folderPaths,
        IReadOnlyCollection<int> years, LocationFilter locations, bool withGps = false)
    {
        IQueryable<VideoEntity> Videos(IReadOnlyCollection<string> folderPaths, IReadOnlyCollection<int> years,
            LocationFilter locations)
        {
            var videos = GetUserVideos(userId, folderPaths, years, locations);

            return withGps ? videos.Where(a => a.Latitude != null && a.Longitude != null) : videos;
        }

        var folders = await Videos([], years, locations)
            .Where(a => a.FolderPath != null)
            .GroupBy(a => a.FolderPath!)
            .Select(a => new FacetCount<string>(a.Key, a.Count()))
            .ToListAsync();

        var facetYears = await Videos(folderPaths, [], locations)
            .GroupBy(a => a.DateTimeTaken.Year)
            .Select(a => new FacetCount<int>(a.Key, a.Count()))
            .ToListAsync();

        var byLocation = Videos(folderPaths, years, LocationFilter.All);
        var facetLocations = await byLocation
            .SelectMany(a => a.Locations.Select(l => l.LocationId))
            .GroupBy(a => a)
            .Select(a => new FacetCount<long>(a.Key, a.Count()))
            .ToListAsync();
        var unknownLocationCount = await byLocation.CountAsync(a => !a.Locations.Any());

        return new VideoFacets(folders, facetYears, facetLocations, unknownLocationCount);
    }

    public Task<int> GetTotalCountByUserIdAsync(long userId, IReadOnlyCollection<string> folderPaths,
        IReadOnlyCollection<int> years, LocationFilter locations)
    {
        return GetUserVideos(userId, folderPaths, years, locations).CountAsync();
    }

    public async Task<IReadOnlyList<Location>> GetLocationsAsync(long userId)
    {
        // a location is left behind when its last video is deleted, and offered again once a video is taken in it
        var counts = await _context.VideoLocations
            .Where(a => a.Video!.UserId == userId)
            .GroupBy(a => a.LocationId)
            .Select(a => new { LocationId = a.Key, VideoCount = a.Count() })
            .ToListAsync();

        // a video is geocoded all at once, so a location is in the larger ones its videos were taken in too
        var parents = await _context.VideoLocations
            .Where(a => a.Video!.UserId == userId)
            .Join(_context.VideoLocations, a => a.VideoId, b => b.VideoId,
                (a, b) => new { a.LocationId, a.Location!.Level, ParentId = b.LocationId, ParentLevel = b.Location!.Level })
            .Where(a => a.ParentLevel < a.Level)
            .Select(a => new { a.LocationId, a.ParentId })
            .Distinct()
            .ToListAsync();

        return await LocationStore.GetAsync(_context, counts.ToDictionary(a => a.LocationId, a => a.VideoCount),
            parents.Select(a => new LocationParent(a.LocationId, a.ParentId)));
    }

    public async Task<IReadOnlyList<VideoGpsLocation>> GetNotGeocodedAsync(long afterId, int top)
    {
        var videos = await _context.Videos
            .AsNoTracking()
            .Where(a => a.Latitude != null && a.Longitude != null && a.Geocoding == null && a.Id > afterId)
            .OrderBy(a => a.Id)
            .Take(top)
            .Select(a => new { a.Id, a.Latitude, a.Longitude })
            .ToListAsync();

        return videos.Select(a => new VideoGpsLocation(a.Id, a.Latitude!.Value, a.Longitude!.Value)).ToList();
    }

    /// <summary>
    /// Saves the geocoding of the videos, a statement for each video, and their locations, all in one transaction.
    /// </summary>
    public async Task SetGeocodingsAsync(IReadOnlyDictionary<long, PhotoGeocoding> geocodingsByVideoId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        foreach (var (videoId, geocoding) in geocodingsByVideoId)
        {
            await _context.Videos
                .Where(a => a.Id == videoId)
                .ExecuteUpdateAsync(a => a.SetProperty(video => video.Geocoding, geocoding.Response));
        }

        var videoIds = geocodingsByVideoId.Keys.ToArray();

        await _context.VideoLocations.Where(a => videoIds.Contains(a.VideoId)).ExecuteDeleteAsync();

        var locationIds = await LocationStore.SaveAsync(_context,
            geocodingsByVideoId.Values.SelectMany(a => a.Locations));

        _context.VideoLocations.AddRange(geocodingsByVideoId.SelectMany(video => video.Value.Locations
            .Select(a => locationIds[(a.Level, a.Key)])
            .Distinct()
            .Select(locationId => new VideoLocationEntity { VideoId = video.Key, LocationId = locationId })));

        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<int>> GetYearsAsync(long userId)
    {
        return await _context.Videos
            .Where(a => a.UserId == userId)
            .Select(a => a.DateTimeTaken.Year)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<string>> GetFolderPathsAsync(long userId)
    {
        return await _context.Videos
            .Where(a => a.UserId == userId && a.FolderPath != null)
            .Select(a => a.FolderPath!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Video>> GetDuplicatesAsync(long userId)
    {
        var entities = await _context.Videos
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.DuplicateGroupId != null)
            .OrderByDescending(a => a.Size)
            .ThenBy(a => a.DuplicateGroupId)
            .ThenBy(a => a.Id)
            .ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<VideoDuplicateKey>> GetDuplicateKeysAsync()
    {
        return await _context.Videos
            .AsNoTracking()
            .Select(a => new VideoDuplicateKey(a.Id, a.UserId, a.FileName, a.Size, a.DuplicateGroupId))
            .ToListAsync();
    }

    public async Task SetDuplicateGroupsAsync(IReadOnlyDictionary<long, long?> duplicateGroupIdsByVideoId)
    {
        // a statement for each group rather than for each video
        foreach (var group in duplicateGroupIdsByVideoId.GroupBy(a => a.Value, a => a.Key))
        {
            var duplicateGroupId = group.Key;

            foreach (var videoIds in group.Chunk(1000))
            {
                await _context.Videos
                    .Where(a => videoIds.Contains(a.Id))
                    .ExecuteUpdateAsync(a => a.SetProperty(b => b.DuplicateGroupId, duplicateGroupId));
            }
        }
    }

    public async Task<IReadOnlySet<string>> GetSavedExternalIdsAsync(long userId, long photoSourceId,
        IEnumerable<string> externalIds)
    {
        var ids = externalIds.ToList();
        if (ids.Count == 0)
        {
            return new HashSet<string>();
        }

        var savedIds = await _context.Videos
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && ids.Contains(a.ExternalId))
            .Select(a => a.ExternalId)
            .ToListAsync();

        return savedIds.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<string, long>> GetIdsWithoutExifAsync(long userId, long photoSourceId)
    {
        return await _context.Videos
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId && a.ExifString == null)
            .ToDictionaryAsync(a => a.ExternalId, a => a.Id);
    }

    public async Task<IReadOnlyList<Video>> GetWithoutExifAsync(long afterId, int count)
    {
        var entities = await _context.Videos
            .AsNoTracking()
            .Where(a => a.ExifString == null && a.Id > afterId)
            .OrderBy(a => a.Id)
            .Take(count)
            .ToListAsync();

        return entities.Select(ToModel).ToList();
    }

    public async Task UpdateExifAsync(IReadOnlyDictionary<long, VideoExif> exifByVideoId)
    {
        if (exifByVideoId.Count == 0)
        {
            return;
        }

        var ids = exifByVideoId.Keys.ToList();
        var entities = await _context.Videos.Where(a => ids.Contains(a.Id)).ToListAsync();
        var moved = new List<long>();

        foreach (var entity in entities)
        {
            var (latitude, longitude) = (entity.Latitude, entity.Longitude);

            // the metadata decides what to keep of the video as it is
            var video = ToModel(entity);
            exifByVideoId[entity.Id].ApplyTo(video);

            entity.ExifString = video.ExifString;
            entity.ExifDateTime = video.ExifDateTime?.ToUniversalTime();
            entity.DateTimeTaken = video.DateTimeTaken.ToUniversalTime();
            entity.Latitude = video.Latitude;
            entity.Longitude = video.Longitude;

            // the geocoding of another place, or of none, tells nothing of where the video was taken
            if (entity.Latitude != latitude || entity.Longitude != longitude)
            {
                entity.Geocoding = null;
                moved.Add(entity.Id);
            }
        }

        try
        {
            await _context.SaveChangesAsync();

            if (moved.Count > 0)
            {
                await _context.VideoLocations.Where(a => moved.Contains(a.VideoId)).ExecuteDeleteAsync();
            }
        }
        finally
        {
            // a run updates chunk after chunk with one context, as it saves them
            foreach (var entity in entities)
            {
                _context.Entry(entity).State = EntityState.Detached;
            }
        }

        if (entities.Any(a => moved.Contains(a.Id) && a.Latitude != null && a.Longitude != null))
        {
            _geocodingSignal.Notify();
        }
    }

    public async Task<IReadOnlyCollection<string>> DeleteByPhotoSourceAsync(long userId, long photoSourceId)
    {
        var videos = _context.Videos.Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

        var previewPaths = await videos
            .Where(a => a.PreviewFilePath != null)
            .Select(a => a.PreviewFilePath!)
            .ToListAsync();

        await videos.ExecuteDeleteAsync();

        // a video left alone in its group of duplicates is no longer a duplicate
        _duplicatesSignal.Notify();

        return previewPaths;
    }

    public async Task<UserPhotoSourceStatus?> GetStatusAsync(long userId, long photoSourceId)
    {
        var entity = await _context.UserVideoSourcesStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.PhotoSourceId == photoSourceId);

        if (entity == null)
        {
            return null;
        }

        return new UserPhotoSourceStatus
        {
            UserId = entity.UserId,
            PhotoSourceId = entity.PhotoSourceId,
            Status = entity.Status,
            TotalCount = entity.TotalCount,
            ProcessedCount = entity.ProcessedCount,
            FailedCount = entity.FailedCount,
            LastUpdatedAt = entity.LastUpdatedAt
        };
    }

    public async Task UpdateStatusAsync(UserPhotoSourceStatus status)
    {
        var entity = await _context.UserVideoSourcesStatuses
            .FirstOrDefaultAsync(a => a.UserId == status.UserId && a.PhotoSourceId == status.PhotoSourceId);

        if (entity == null)
        {
            entity = new UserVideoSourceStatusEntity { UserId = status.UserId, PhotoSourceId = status.PhotoSourceId };
            _context.UserVideoSourcesStatuses.Add(entity);
        }

        entity.Status = status.Status;
        entity.TotalCount = status.TotalCount;
        entity.ProcessedCount = status.ProcessedCount;
        entity.FailedCount = status.FailedCount;
        entity.LastUpdatedAt = status.LastUpdatedAt;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteStatusAsync(long userId, long photoSourceId)
    {
        await _context.UserVideoSourcesStatuses
            .Where(a => a.UserId == userId && a.PhotoSourceId == photoSourceId)
            .ExecuteDeleteAsync();
    }

    public async Task<int> PauseInProgressAsync()
    {
        var entities = await _context.UserVideoSourcesStatuses
            .Where(a => a.Status == PhotoSourceStatus.InProgress)
            .ToListAsync();

        foreach (var entity in entities)
        {
            entity.Status = PhotoSourceStatus.Paused;
            entity.LastUpdatedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();

        return entities.Count;
    }

    private IQueryable<VideoEntity> GetUserVideos(long userId, IReadOnlyCollection<string> folderPaths,
        IReadOnlyCollection<int> years, LocationFilter locations)
    {
        var videos = _context.Videos.Where(a => a.UserId == userId);

        if (folderPaths.Count > 0)
        {
            videos = videos.Where(a => a.FolderPath != null && folderPaths.Contains(a.FolderPath));
        }

        if (years.Count > 0)
        {
            videos = videos.Where(a => years.Contains(a.DateTimeTaken.Year));
        }

        if (locations.FiltersByLocations)
        {
            var locationIds = locations.LocationIds.ToArray();
            var unknownLocation = locations.UnknownLocation;

            videos = videos.Where(a => a.Locations.Any(l => locationIds.Contains(l.LocationId)) ||
                                       (unknownLocation && !a.Locations.Any()));
        }

        return videos;
    }

    private static Video ToModel(VideoEntity entity)
    {
        return new Video
        {
            Id = entity.Id,
            UserId = entity.UserId,
            PhotoSourceId = entity.PhotoSourceId,
            ExternalId = entity.ExternalId,
            FileName = entity.FileName,
            FolderPath = entity.FolderPath,
            MimeType = entity.MimeType,
            Size = entity.Size,
            DateTimeTaken = entity.DateTimeTaken,
            ExifDateTime = entity.ExifDateTime,
            ExifString = entity.ExifString,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            PreviewFilePath = entity.PreviewFilePath,
            PreviewContentType = entity.PreviewContentType,
            AddedOn = entity.AddedOn,
            DuplicateGroupId = entity.DuplicateGroupId
        };
    }
}
