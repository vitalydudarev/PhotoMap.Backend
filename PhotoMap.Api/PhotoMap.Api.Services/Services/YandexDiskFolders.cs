using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The listing of the folders of a Yandex.Disk, one folder at a time.
/// </summary>
public static class YandexDiskFolders
{
    /// <summary>
    /// The most files and folders a page of a folder holds; most folders take a single page.
    /// </summary>
    public const int PageSize = 10000;

    private const string SortOrder = "created";

    /// <summary>
    /// The files and folders directly within the folder, a page at a time.
    /// </summary>
    /// <exception cref="YandexDiskException">The path is not a folder.</exception>
    public static async IAsyncEnumerable<Resource> ListFolderAsync(ApiClient apiClient, ILogger logger, string folder,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var offset = 0;

        while (true)
        {
            var resource = await YandexDiskApiCalls.WrapAsync(logger,
                () => apiClient.GetResourceAsync(folder, cancellationToken, offset, PageSize, SortOrder), cancellationToken);
            var page = resource.Embedded ?? throw new YandexDiskException($"{folder} is not a folder.");
            var items = page.Items ?? [];

            foreach (var item in items)
            {
                yield return item;
            }

            offset += items.Length;

            if (items.Length < PageSize || offset >= page.Total)
            {
                yield break;
            }
        }
    }
}
