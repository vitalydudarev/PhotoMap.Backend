namespace PhotoMap.Api.Services.Services;

/// <summary>
/// A read-only stream of a remote file that downloads only the parts of it that are read, a block at a time, and
/// keeps them. Made for the metadata of videos, which is a few kilobytes at the start and at the end of a file of
/// hundreds of megabytes: the reader seeks past the rest, which is never downloaded.
/// </summary>
/// <remarks>
/// The reader of the metadata reads synchronously, so the downloads are waited for: the stream is only read on
/// the threads of a background run.
/// </remarks>
public sealed class RangeReadStream : Stream
{
    /// <summary>
    /// How much is downloaded at a time: the metadata of a video of a phone, at the end of the file, takes one
    /// block.
    /// </summary>
    public const int DefaultBlockSize = 64 * 1024;

    private readonly Func<long, int, CancellationToken, Task<byte[]>> _downloadRange;
    private readonly long _length;
    private readonly int _blockSize;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// The blocks downloaded, by where they start, without overlaps.
    /// </summary>
    private readonly SortedList<long, byte[]> _blocks = new();

    private long _position;

    /// <param name="downloadRange">Downloads the bytes of the file from the offset given, up to the count given.</param>
    /// <param name="length">The size of the file.</param>
    public RangeReadStream(Func<long, int, CancellationToken, Task<byte[]>> downloadRange, long length,
        CancellationToken cancellationToken, int blockSize = DefaultBlockSize)
    {
        _downloadRange = downloadRange;
        _length = length;
        _blockSize = blockSize;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// How many times a part of the file has been downloaded.
    /// </summary>
    public int DownloadCount { get; private set; }

    /// <summary>
    /// The error a download has failed with, which the reader of the metadata may have caught and taken as the end
    /// of the file: what it has read is then not all there is.
    /// </summary>
    public Exception? DownloadError { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => _position = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        var total = 0;

        while (total < buffer.Length && _position < _length)
        {
            var (blockStart, block) = GetBlock(_position);
            var offsetInBlock = (int)(_position - blockStart);
            var count = Math.Min(buffer.Length - total, block.Length - offsetInBlock);

            if (count <= 0)
            {
                // the server has sent less of the file than its size
                break;
            }

            block.AsSpan(offsetInBlock, count).CopyTo(buffer[total..]);
            total += count;
            _position += count;
        }

        return total;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        return _position;
    }

    /// <summary>
    /// The block the position is in, downloaded when it has not been: from the position, up to the block after it.
    /// </summary>
    private (long Start, byte[] Block) GetBlock(long position)
    {
        var index = FindBlockAtOrBefore(position);
        if (index >= 0)
        {
            var start = _blocks.Keys[index];
            var block = _blocks.Values[index];

            if (position < start + block.Length)
            {
                return (start, block);
            }
        }

        // up to the next block downloaded, so that blocks do not overlap
        var end = Math.Min(position + _blockSize, _length);
        if (index + 1 < _blocks.Count)
        {
            end = Math.Min(end, _blocks.Keys[index + 1]);
        }

        var downloaded = Download(position, (int)(end - position));
        _blocks.Add(position, downloaded);

        return (position, downloaded);
    }

    private byte[] Download(long position, int count)
    {
        DownloadCount++;

        try
        {
            return _downloadRange(position, count, _cancellationToken).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            DownloadError = e;
            throw;
        }
    }

    /// <summary>
    /// The index of the last block that starts at or before the position, -1 when there is none.
    /// </summary>
    private int FindBlockAtOrBefore(long position)
    {
        var keys = _blocks.Keys;
        int low = 0, high = keys.Count - 1, found = -1;

        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (keys[middle] <= position)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
