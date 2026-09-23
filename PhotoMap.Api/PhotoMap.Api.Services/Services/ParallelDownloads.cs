using System.Runtime.CompilerServices;
using System.Threading.Channels;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

public static class ParallelDownloads
{
    /// <summary>
    /// Downloads files several at a time and hands each one over as it arrives, so that the caller can pass it on
    /// while the rest are still downloading. The files come in the order they finish downloading, not the order
    /// they were given in.
    /// </summary>
    /// <param name="files">The files to download.</param>
    /// <param name="maxDegreeOfParallelism">How many of them to download at a time.</param>
    /// <param name="downloadAsync">Downloads one file, returning null for a file to skip.</param>
    public static async IAsyncEnumerable<DownloadedFile> RunAsync<T>(
        IReadOnlyCollection<T> files,
        int maxDegreeOfParallelism,
        Func<T, CancellationToken, Task<DownloadedFile?>> downloadAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // bounded: with the files being downloaded, that many downloaded files at most are held in memory,
        // and the downloads pause while the caller catches up
        var downloadedFiles = Channel.CreateBounded<DownloadedFile>(
            new BoundedChannelOptions(maxDegreeOfParallelism) { FullMode = BoundedChannelFullMode.Wait });

        using var downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var downloadTask = DownloadAsync(files, maxDegreeOfParallelism, downloadAsync, downloadedFiles.Writer,
            downloadCancellation.Token);

        try
        {
            await foreach (var downloadedFile in downloadedFiles.Reader.ReadAllAsync(cancellationToken))
            {
                yield return downloadedFile;
            }

            // the downloads have finished, this reports the error they ended with, if any
            await downloadTask;
        }
        finally
        {
            // the caller may have stopped reading, the downloads must not go on in the background
            await downloadCancellation.CancelAsync();

            try
            {
                await downloadTask;
            }
            catch (Exception)
            {
                // the caller has been told already, or it stopped reading
            }
        }
    }

    private static async Task DownloadAsync<T>(
        IReadOnlyCollection<T> files,
        int maxDegreeOfParallelism,
        Func<T, CancellationToken, Task<DownloadedFile?>> downloadAsync,
        ChannelWriter<DownloadedFile> downloadedFiles,
        CancellationToken cancellationToken)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = cancellationToken
        };

        try
        {
            await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
            {
                var downloadedFile = await downloadAsync(file, token);
                if (downloadedFile != null)
                {
                    await downloadedFiles.WriteAsync(downloadedFile, token);
                }
            });
        }
        finally
        {
            // the caller reads what has been downloaded so far, then awaits this task for the error
            downloadedFiles.Complete();
        }
    }
}
