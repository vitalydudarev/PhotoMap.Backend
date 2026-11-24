using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        /// <summary>
        /// The most photos a page holds: the largest page the gallery offers.
        /// </summary>
        private const int MaxPageSize = 1000;

        private readonly IPhotoService _photoService;
        private readonly IUserService _dbUserService;
        private readonly HostInfo _hostInfo;

        public UsersController(IPhotoService photoService, IUserService dbUserService, HostInfo hostInfo)
        {
            _photoService = photoService;
            _dbUserService = dbUserService;
            _hostInfo = hostInfo;
        }

        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUser(long id)
        {
            var user = await _dbUserService.GetAsync(id);
            if (user != null)
            {
                return Ok(user);
            }

            return NotFound();
        }

        /// <summary>
        /// The photos of the user, by the date they were taken, oldest first unless asked for the other way round.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="top">How many photos to return, 1 to 1000. Required.</param>
        /// <param name="skip">How many photos to skip, 0 or more, 0 when not given.</param>
        /// <param name="sort">The order of the photos by the date they were taken.</param>
        /// <param name="source">The IDs of the photo sources to take the photos from, repeated for each one, such as
        /// <c>source=1&amp;source=2</c>. All the sources of the user when not given.</param>
        /// <param name="year">The years, in UTC, the photos were taken in, repeated for each one, such as
        /// <c>year=2016&amp;year=2019</c>. All the years when not given.</param>
        /// <param name="category">The categories the photos are in, any of them, repeated for each one, such as
        /// <c>category=Screenshot&amp;category=Other</c>; <c>Other</c> takes the photos in none of the categories.
        /// <c>Deleted</c> takes the photos marked as deleted, which every other category leaves out. Photos of every
        /// category but <c>Deleted</c> when not given.</param>
        /// <param name="gps">Whether the photos have a GPS location. The photos with and without one when not given.</param>
        /// <param name="allFolders">Whether the photos outside the folders the sources are processed from are taken
        /// too, the ones of the other uploads of Yandex.Disk. Only the photos of those folders when not given.</param>
        /// <param name="tag">The IDs of the tags the photos have, any of them, repeated for each one, such as
        /// <c>tag=4&amp;tag=9</c>. The photos with and without tags when neither this nor <c>untagged</c> is
        /// given.</param>
        /// <param name="untagged">Whether the photos with no tag are taken, along with the ones of <c>tag</c>; only
        /// they are when no tag is given.</param>
        /// <param name="area">The box of coordinates the photos were taken within, as <c>west,south,east,north</c> in
        /// degrees, such as <c>area=-9.23,38.69,-9.09,38.8</c>; the photos with no GPS location are left out. The
        /// photos taken anywhere, and those with no location, when not given.</param>
        /// <param name="location">The IDs of the locations the photos were taken in, any of them, repeated for each one,
        /// such as <c>location=12&amp;location=40</c>; see <c>photos/locations</c>. The photos not geocoded are in no
        /// location. The photos taken anywhere, and those with no location, when neither this nor
        /// <c>unknownLocation</c> is given.</param>
        /// <param name="unknownLocation">Whether the photos in no location are taken, along with the ones of
        /// <c>location</c>: those with no GPS, not geocoded yet, or where the geocoding found nothing. Only they are
        /// when no location is given.</param>
        /// <param name="folder">The folders the photos are in, without the file name, repeated for each one, such as
        /// <c>folder=disk:/Camera Uploads&amp;folder=disk:/Trips</c>; see <c>photos/folders</c>. A folder outside the
        /// ones the sources are processed from takes photos only along with <c>allFolders</c>. The photos of every
        /// folder when not given.</param>
        [HttpGet("{id:long}/photos")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResponse<PhotoDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserPhotos([FromRoute] long id,
            [FromQuery, BindRequired, Range(1, MaxPageSize)] int top,
            [FromQuery, Range(0, int.MaxValue)] int skip,
            [FromQuery] PhotoSortOrder sort = PhotoSortOrder.Asc,
            [FromQuery] long[]? source = null,
            [FromQuery] int[]? year = null,
            [FromQuery] PhotoCategory[]? category = null,
            [FromQuery] bool? gps = null,
            [FromQuery] bool allFolders = false,
            [FromQuery] long[]? tag = null,
            [FromQuery] bool untagged = false,
            [FromQuery] string? area = null,
            [FromQuery] long[]? location = null,
            [FromQuery] bool unknownLocation = false,
            [FromQuery] string[]? folder = null)
        {
            var filter = CreateFilter(source, year, category, gps, allFolders, tag, untagged, area, location,
                unknownLocation, folder);

            if (filter == null)
            {
                return ValidationProblem(ModelState);
            }

            var userPhotos = await _photoService.GetByUserIdAsync(id, filter, top, skip, sort);
            var totalPhotosCount = await _photoService.GetTotalCountByUserIdAsync(id, filter);
            
            var values = userPhotos.Select(ToDto).ToArray();

            var response = new PagedResponse<PhotoDto> { Values = values, Limit = top, Offset = skip, Total = totalPhotosCount };
            
            return Ok(response);
        }

        /// <summary>
        /// What the photos the filter takes can still be narrowed down to, for each of its filters: the values of the
        /// photos the filter takes once that filter itself is left out, such as the years of the photos of the folders
        /// picked, so that the options of each filter are those that take some photo. Every copy of a photo counts.
        /// The filter is the one of <c>photos</c>, see there.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="source">The IDs of the photo sources to take the photos from; all of them when not given.</param>
        /// <param name="year">The years, in UTC, the photos were taken in; all of them when not given.</param>
        /// <param name="category">The categories the photos are in, any of them; every one but <c>Deleted</c> when
        /// not given.</param>
        /// <param name="gps">Whether the photos have a GPS location; with and without one when not given.</param>
        /// <param name="allFolders">Whether the photos of the other uploads of Yandex.Disk are taken too.</param>
        /// <param name="tag">The IDs of the tags the photos have, any of them.</param>
        /// <param name="untagged">Whether the photos with no tag are taken, along with the ones of <c>tag</c>.</param>
        /// <param name="area">The box of coordinates the photos were taken within, as <c>west,south,east,north</c>
        /// in degrees.</param>
        /// <param name="location">The IDs of the locations the photos were taken in, any of them.</param>
        /// <param name="unknownLocation">Whether the photos in no location are taken, along with the ones of
        /// <c>location</c>.</param>
        /// <param name="folder">The folders the photos are in, without the file name, any of them.</param>
        [HttpGet("{id:long}/photos/facets")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PhotoFacets))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserPhotoFacets([FromRoute] long id,
            [FromQuery] long[]? source = null,
            [FromQuery] int[]? year = null,
            [FromQuery] PhotoCategory[]? category = null,
            [FromQuery] bool? gps = null,
            [FromQuery] bool allFolders = false,
            [FromQuery] long[]? tag = null,
            [FromQuery] bool untagged = false,
            [FromQuery] string? area = null,
            [FromQuery] long[]? location = null,
            [FromQuery] bool unknownLocation = false,
            [FromQuery] string[]? folder = null)
        {
            var filter = CreateFilter(source, year, category, gps, allFolders, tag, untagged, area, location,
                unknownLocation, folder);

            if (filter == null)
            {
                return ValidationProblem(ModelState);
            }

            return Ok(await _photoService.GetFacetsAsync(id, filter));
        }

        /// <summary>
        /// The photos of the user with a GPS location that the filter takes, all of them, oldest first, for the map. A
        /// photo with copies is taken once, as in <c>photos</c>. The filter is the one of <c>photos</c>, see there.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="source">The IDs of the photo sources to take the photos from; all of them when not given.</param>
        /// <param name="year">The years, in UTC, the photos were taken in; all of them when not given.</param>
        /// <param name="category">The categories the photos are in, any of them; every one but <c>Deleted</c> when
        /// not given.</param>
        /// <param name="gps">Whether the photos have a GPS location; only those with one are taken anyway.</param>
        /// <param name="allFolders">Whether the photos of the other uploads of Yandex.Disk are taken too.</param>
        /// <param name="tag">The IDs of the tags the photos have, any of them.</param>
        /// <param name="untagged">Whether the photos with no tag are taken, along with the ones of <c>tag</c>.</param>
        /// <param name="area">The box of coordinates the photos were taken within, as <c>west,south,east,north</c>
        /// in degrees.</param>
        /// <param name="location">The IDs of the locations the photos were taken in, any of them.</param>
        /// <param name="unknownLocation">Whether the photos in no location are taken, along with the ones of
        /// <c>location</c>: those not geocoded yet, or where the geocoding found nothing.</param>
        /// <param name="folder">The folders the photos are in, without the file name, any of them.</param>
        [HttpGet("{id:long}/photos/geotagged")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PhotoDto[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserGeotaggedPhotos([FromRoute] long id,
            [FromQuery] long[]? source = null,
            [FromQuery] int[]? year = null,
            [FromQuery] PhotoCategory[]? category = null,
            [FromQuery] bool? gps = null,
            [FromQuery] bool allFolders = false,
            [FromQuery] long[]? tag = null,
            [FromQuery] bool untagged = false,
            [FromQuery] string? area = null,
            [FromQuery] long[]? location = null,
            [FromQuery] bool unknownLocation = false,
            [FromQuery] string[]? folder = null)
        {
            var filter = CreateFilter(source, year, category, gps, allFolders, tag, untagged, area, location,
                unknownLocation, folder);

            if (filter == null)
            {
                return ValidationProblem(ModelState);
            }

            var photos = await _photoService.GetGeotaggedAsync(id, filter);

            return Ok(photos.Select(ToDto).ToArray());
        }

        /// <summary>
        /// The photos of the user that are copies of one another, with the same contents to the byte, by their
        /// groups, the groups of the photos taken first first. Photos marked as deleted are in no group. The groups
        /// are looked for in the background, a photo saved, deleted or restored only a moment ago may not be in its
        /// group yet.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        [HttpGet("{id:long}/photos/duplicates")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PhotoDuplicateGroupDto[]))]
        public async Task<IActionResult> GetUserPhotoDuplicates([FromRoute] long id)
        {
            var photos = await _photoService.GetDuplicatesAsync(id);

            // the photos come by their groups already
            var groups = photos
                .GroupBy(a => a.DuplicateGroupId!.Value)
                .Select(a => new PhotoDuplicateGroupDto { Id = a.Key, Photos = a.Select(ToDto).ToArray() })
                .ToArray();

            return Ok(groups);
        }

        /// <summary>
        /// Marks a photo of the user as deleted. It is kept, with its files, and only shows among the photos of the
        /// <c>Deleted</c> category until it is restored.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="photoId">The ID of the photo.</param>
        [HttpPost("{id:long}/photos/{photoId:long}/delete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkPhotoAsDeleted([FromRoute] long id, [FromRoute] long photoId)
        {
            return await _photoService.MarkAsDeletedAsync(id, photoId) ? NoContent() : NotFound();
        }

        /// <summary>
        /// Takes a photo of the user back from the deleted photos. Restoring a photo that is not deleted does nothing.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="photoId">The ID of the photo.</param>
        [HttpPost("{id:long}/photos/{photoId:long}/restore")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RestorePhoto([FromRoute] long id, [FromRoute] long photoId)
        {
            return await _photoService.RestoreAsync(id, photoId) ? NoContent() : NotFound();
        }

        /// <summary>
        /// The years, in UTC, the photos of the user were taken in, oldest first. They are cached, and kept up to date
        /// as photos are added and deleted.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        [HttpGet("{id:long}/photos/years")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int[]))]
        public async Task<IActionResult> GetUserPhotoYears([FromRoute] long id)
        {
            return Ok(await _photoService.GetYearsAsync(id));
        }

        /// <summary>
        /// The folders the photos of the user are in, the deleted ones too, without the file names, by name, such as
        /// <c>disk:/Camera Uploads</c>, and <c>disk:/</c> for the photos at the root of the disk.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        [HttpGet("{id:long}/photos/folders")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string[]))]
        public async Task<IActionResult> GetUserPhotoFolders([FromRoute] long id)
        {
            return Ok(await _photoService.GetFolderPathsAsync(id));
        }

        /// <summary>
        /// The tags of the user that a photo has at least, by name, with how many photos have each.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        [HttpGet("{id:long}/photos/tags")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PhotoTagDto[]))]
        public async Task<IActionResult> GetUserPhotoTags([FromRoute] long id)
        {
            var tags = await _photoService.GetTagsAsync(id);

            return Ok(tags.Select(a => new PhotoTagDto
            {
                Id = a.Id, Name = a.Name, PhotoCount = a.PhotoCount, VideoCount = a.VideoCount
            }).ToArray());
        }

        /// <summary>
        /// The locations the photos of the user were taken in, the deleted ones too, by level, from the countries down
        /// to the postcodes, and by name, with how many photos were taken in each. A photo is in a location once it is
        /// geocoded.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        [HttpGet("{id:long}/photos/locations")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LocationDto[]))]
        public async Task<IActionResult> GetUserPhotoLocations([FromRoute] long id)
        {
            var locations = await _photoService.GetLocationsAsync(id);

            return Ok(locations.Select(a => new LocationDto
            {
                Id = a.Id,
                Level = a.Level.ToString(),
                Name = a.Name,
                Description = a.Description,
                PhotoCount = a.Count,
                ParentIds = a.ParentIds.ToArray()
            }).ToArray());
        }

        /// <summary>
        /// Replaces the tags of a photo of the user. The names are trimmed, with single spaces between words, and a
        /// name the user has a tag of, whatever its case, takes that tag; the other names make new tags.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="photoId">The ID of the photo.</param>
        /// <param name="names">The names of the tags, none to take them all off.</param>
        /// <returns>The names of the tags of the photo, by name.</returns>
        [HttpPut("{id:long}/photos/{photoId:long}/tags")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetPhotoTags([FromRoute] long id, [FromRoute] long photoId,
            [FromBody] string[] names)
        {
            var cleanNames = names.Select(a => TagNames.Clean(a ?? "")).Where(a => a.Length > 0).ToArray();

            if (cleanNames.Any(a => a.Length > TagNames.MaxLength))
            {
                ModelState.AddModelError(nameof(names), $"A tag is {TagNames.MaxLength} characters long at most.");

                return ValidationProblem(ModelState);
            }

            if (cleanNames.DistinctBy(TagNames.Normalize).Count() > TagNames.MaxPerPhoto)
            {
                ModelState.AddModelError(nameof(names), $"A photo has {TagNames.MaxPerPhoto} tags at most.");

                return ValidationProblem(ModelState);
            }

            var tags = await _photoService.SetTagsAsync(id, photoId, cleanNames);

            return tags != null ? Ok(tags) : NotFound();
        }

        /// <summary>
        /// Adds tags to several photos of the user at once, keeping the tags they have. The names are cleaned as by
        /// <c>PUT photos/{photoId}/tags</c>, and a name the user has a tag of, whatever its case, takes that tag; the
        /// other names make new tags. The photos are all given the tags, or none of them is.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="request">The IDs of the photos, 1 to 1000 of them, and the names of the tags, one at least.</param>
        /// <returns>The tags of each photo once they were added.</returns>
        [HttpPost("{id:long}/photos/tags")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PhotoTagsDto[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddPhotoTags([FromRoute] long id, [FromBody] AddPhotoTagsDto request)
        {
            var photoIds = (request.PhotoIds ?? []).Distinct().ToArray();
            var cleanNames = (request.Tags ?? []).Select(a => TagNames.Clean(a ?? "")).Where(a => a.Length > 0)
                .ToArray();

            if (photoIds.Length is 0 or > MaxPageSize)
            {
                ModelState.AddModelError(nameof(request.PhotoIds), $"Give 1 to {MaxPageSize} photos.");
            }

            if (cleanNames.Length == 0)
            {
                ModelState.AddModelError(nameof(request.Tags), "Give a tag at least.");
            }
            else if (cleanNames.Any(a => a.Length > TagNames.MaxLength))
            {
                ModelState.AddModelError(nameof(request.Tags), $"A tag is {TagNames.MaxLength} characters long at most.");
            }
            else if (cleanNames.DistinctBy(TagNames.Normalize).Count() > TagNames.MaxPerPhoto)
            {
                ModelState.AddModelError(nameof(request.Tags), $"A photo has {TagNames.MaxPerPhoto} tags at most.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var added = await _photoService.AddTagsAsync(id, photoIds, cleanNames);

            switch (added.Outcome)
            {
                case AddedTagsOutcome.PhotoNotFound:
                    return NotFound();
                case AddedTagsOutcome.TooManyTags:
                    ModelState.AddModelError(nameof(request.Tags),
                        $"A photo has {TagNames.MaxPerPhoto} tags at most, and some of the photos would have more.");

                    return ValidationProblem(ModelState);
                default:
                    return Ok(added.PhotoTags
                        .Select(a => new PhotoTagsDto { PhotoId = a.Key, Tags = a.Value.ToArray() })
                        .ToArray());
            }
        }

        /// <summary>
        /// Suggests tags for a photo of the user: those of the photos taken within a week of it or within 5 km, the
        /// nearer the better, and the year and the season it was taken in. The tags the photo has are left out.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="photoId">The ID of the photo.</param>
        [HttpGet("{id:long}/photos/{photoId:long}/tags/suggestions")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TagSuggestionDto[]))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPhotoTagSuggestions([FromRoute] long id, [FromRoute] long photoId)
        {
            var suggestions = await _photoService.SuggestTagsAsync(id, photoId);
            if (suggestions == null)
            {
                return NotFound();
            }

            return Ok(suggestions
                .Select(a => new TagSuggestionDto { Name = a.Name, ByDate = a.ByDate, ByLocation = a.ByLocation })
                .ToArray());
        }

        /// <summary>
        /// The filter of the photos the query asks for, see <see cref="GetUserPhotos"/>.
        /// </summary>
        /// <returns>The filter, or null when the query does not make one, the errors of which are then in the model
        /// state.</returns>
        private PhotoFilter? CreateFilter(long[]? source, int[]? year, PhotoCategory[]? category, bool? gps,
            bool allFolders, long[]? tag, bool untagged, string? area, long[]? location, bool unknownLocation,
            string[]? folder)
        {
            // a number binds to the enum whether it names a category or not
            if (category != null && !category.All(Enum.IsDefined))
            {
                ModelState.AddModelError(nameof(category), "The category is not one of the photo categories.");

                return null;
            }

            GeoBox? box = null;

            if (area != null && !GeoBox.TryParse(area, out box))
            {
                ModelState.AddModelError(nameof(area),
                    "The area is not a box of coordinates written as west,south,east,north, in degrees.");

                return null;
            }

            return new PhotoFilter(source ?? [], year ?? [], category ?? [], gps, allFolders, tag ?? [], untagged,
                box, location ?? [], unknownLocation, folder?.Where(a => !string.IsNullOrEmpty(a)).ToArray() ?? []);
        }

        private PhotoDto ToDto(Photo photo)
        {
            var url = _hostInfo.GetUrl() + "api";

            return new PhotoDto
            {
                DateTimeTaken = photo.DateTimeTaken.UtcDateTime,
                FileName = photo.FileName,
                PhotoSourceId = photo.PhotoSourceId,
                Path = photo.Path,
                Id = photo.Id,
                Latitude = photo.Latitude,
                Longitude = photo.Longitude,
                HorizontalPositioningError = photo.HorizontalPositioningError,
                PhotoUrl = $"{url}/photos/{photo.Id}",
                PreviewUrl = $"{url}/photos/{photo.Id}/preview",
                ThumbnailLargeUrl = $"{url}/photos/{photo.Id}/thumb/large",
                ThumbnailSmallUrl = $"{url}/photos/{photo.Id}/thumb/small",
                DeletedOn = photo.DeletedOn?.UtcDateTime,
                Tags = photo.Tags.ToArray()
            };
        }
    }
}
