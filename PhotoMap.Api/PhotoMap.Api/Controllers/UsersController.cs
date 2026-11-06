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
            [FromQuery] bool untagged = false)
        {
            // a number binds to the enum whether it names a category or not
            if (category != null && !category.All(Enum.IsDefined))
            {
                ModelState.AddModelError(nameof(category), "The category is not one of the photo categories.");

                return ValidationProblem(ModelState);
            }

            var filter = new PhotoFilter(source ?? [], year ?? [], category ?? [], gps, allFolders, tag ?? [], untagged);

            var userPhotos = await _photoService.GetByUserIdAsync(id, filter, top, skip, sort);
            var totalPhotosCount = await _photoService.GetTotalCountByUserIdAsync(id, filter);
            
            var values = userPhotos.Select(ToDto).ToArray();

            var response = new PagedResponse<PhotoDto> { Values = values, Limit = top, Offset = skip, Total = totalPhotosCount };
            
            return Ok(response);
        }

        /// <summary>
        /// The photos of the user with a GPS location, all of them, oldest first, for the map. The deleted photos are
        /// left out.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        [HttpGet("{id:long}/photos/geotagged")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PhotoDto[]))]
        public async Task<IActionResult> GetUserGeotaggedPhotos([FromRoute] long id)
        {
            var photos = await _photoService.GetGeotaggedAsync(id);

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
        /// The tags of the user that a photo has at least, by name, with how many photos have each.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        [HttpGet("{id:long}/photos/tags")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PhotoTagDto[]))]
        public async Task<IActionResult> GetUserPhotoTags([FromRoute] long id)
        {
            var tags = await _photoService.GetTagsAsync(id);

            return Ok(tags.Select(a => new PhotoTagDto { Id = a.Id, Name = a.Name, PhotoCount = a.PhotoCount }).ToArray());
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

        private PhotoDto ToDto(Photo photo)
        {
            var url = _hostInfo.GetUrl() + "api";

            return new PhotoDto
            {
                DateTimeTaken = photo.DateTimeTaken.UtcDateTime,
                FileName = photo.FileName,
                Path = photo.Path,
                Id = photo.Id,
                Latitude = photo.Latitude,
                Longitude = photo.Longitude,
                HorizontalPositioningError = photo.HorizontalPositioningError,
                PhotoUrl = $"{url}/photos/{photo.Id}",
                ThumbnailLargeUrl = $"{url}/photos/{photo.Id}/thumb/large",
                ThumbnailSmallUrl = $"{url}/photos/{photo.Id}/thumb/small",
                DeletedOn = photo.DeletedOn?.UtcDateTime,
                Tags = photo.Tags.ToArray()
            };
        }
    }
}
