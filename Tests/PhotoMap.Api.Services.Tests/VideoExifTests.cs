using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Tests;

public class VideoExifTests
{
    /// <summary>
    /// The movie header of the files: 2023-07-16 17:31:31 UTC, in seconds since 1904.
    /// </summary>
    private static readonly DateTimeOffset MovieCreated = new(2023, 7, 16, 17, 31, 31, TimeSpan.Zero);

    [Fact]
    public void Read_ShouldReadTheMetadataOfAnIPhoneVideo()
    {
        // Arrange
        var file = QuickTimeFile.Create(mdatSize: 1000, new Dictionary<string, string>
        {
            ["com.apple.quicktime.location.ISO6709"] = "+53.9095+027.5202+224.391/",
            ["com.apple.quicktime.make"] = "Apple",
            ["com.apple.quicktime.model"] = "iPhone 15 Pro",
            ["com.apple.quicktime.creationdate"] = "2026-09-17T19:07:43+0300"
        });

        // Act
        var exif = VideoExifReader.Read(new MemoryStream(file.ToArray()));

        // Assert
        Assert.Equal(53.9095, exif.Latitude);
        Assert.Equal(27.5202, exif.Longitude);
        Assert.Equal(new DateTimeOffset(2026, 9, 17, 16, 7, 43, TimeSpan.Zero), exif.DateTimeTaken);

        using var json = JsonDocument.Parse(exif.ExifString);
        var metadataHeader = json.RootElement.GetProperty("QuickTime Metadata Header");
        Assert.Equal("Apple", metadataHeader.GetProperty("Make").GetString());
        Assert.Equal("iPhone 15 Pro", metadataHeader.GetProperty("Model").GetString());
        Assert.Equal("+53.9095+027.5202+224.391/", metadataHeader.GetProperty("GPS Location").GetString());
        Assert.Equal("2026-09-17T16:07:43.0000000Z", metadataHeader.GetProperty("Creation Date").GetString());

        var movieHeader = json.RootElement.GetProperty("QuickTime Movie Header");
        Assert.Equal("2023-07-16T17:31:31.0000000Z", movieHeader.GetProperty("Created").GetString());
        Assert.Equal(25.5, movieHeader.GetProperty("Duration").GetDouble());
    }

    [Fact]
    public void Read_ShouldTakeWhenTheMovieWasCreated_WhenTheCameraHasNotWrittenTheDate()
    {
        // Arrange: a camera other than a phone writes the location only
        var file = QuickTimeFile.Create(mdatSize: 1000, new Dictionary<string, string>
        {
            ["com.apple.quicktime.location.ISO6709"] = "+38.6319+34.8161"
        });

        // Act
        var exif = VideoExifReader.Read(new MemoryStream(file.ToArray()));

        // Assert
        Assert.Equal(MovieCreated, exif.DateTimeTaken);
        Assert.Equal(38.6319, exif.Latitude);
        Assert.Equal(34.8161, exif.Longitude);
    }

    [Fact]
    public void Read_ShouldBeEmpty_ForAFileOfAFormatThatIsNotKnown()
    {
        var exif = VideoExifReader.Read(new MemoryStream(Encoding.ASCII.GetBytes("not a video, not at all")));

        Assert.Same(VideoExif.Empty, exif);
    }

    [Theory]
    [InlineData("+53.9095+027.5202+224.391/", 53.9095, 27.5202)]
    [InlineData("+38.6319+34.8161", 38.6319, 34.8161)]
    [InlineData("+38.6319+34.8161\0", 38.6319, 34.8161)]
    [InlineData("-33.8688+151.2093/", -33.8688, 151.2093)]
    [InlineData("+40.7128-074.0060+010.000CRSWGS_84/", 40.7128, -74.006)]
    public void ParseIso6709_ShouldReadTheDegrees(string location, double latitude, double longitude)
    {
        Assert.Equal((latitude, longitude), VideoExifReader.ParseIso6709(location));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("+0000.0000+00000.0000/")]
    [InlineData("+5354.57+02731.21/")]
    [InlineData("+00.0000+000.0000/")]
    [InlineData("+95.0000+027.0000/")]
    public void ParseIso6709_ShouldBeNull_ForWhatIsNotALocationInDegrees(string? location)
    {
        Assert.Equal((null, null), VideoExifReader.ParseIso6709(location));
    }

    [Fact]
    public void ApplyTo_ShouldFillInWhatThePhotoSourceHasNotRead()
    {
        // Arrange
        var video = CreateVideo();
        var exif = new VideoExif("{\"a\":{}}", MovieCreated, 53.9, 27.5);

        // Act
        exif.ApplyTo(video);

        // Assert
        Assert.Equal("{\"a\":{}}", video.ExifString);
        Assert.Equal(MovieCreated, video.ExifDateTime);
        Assert.Equal(MovieCreated, video.DateTimeTaken);
        Assert.Equal(53.9, video.Latitude);
        Assert.Equal(27.5, video.Longitude);
    }

    [Fact]
    public void ApplyTo_ShouldKeepWhatThePhotoSourceHasRead()
    {
        // Arrange
        var sourceDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var video = CreateVideo();
        video.ExifDateTime = sourceDate;
        video.DateTimeTaken = sourceDate;
        video.Latitude = 1;
        video.Longitude = 2;

        // Act
        new VideoExif("{}", MovieCreated, 53.9, 27.5).ApplyTo(video);

        // Assert
        Assert.Equal("{}", video.ExifString);
        Assert.Equal(sourceDate, video.DateTimeTaken);
        Assert.Equal(1, video.Latitude);
        Assert.Equal(2, video.Longitude);
    }

    [Fact]
    public async Task ReadExifAsync_ShouldDownloadOnlyThePartsOfTheFileWithTheMetadata()
    {
        // Arrange: the metadata is after 200 MB of the video itself, as phones write it
        var file = QuickTimeFile.Create(mdatSize: 200L * 1024 * 1024, new Dictionary<string, string>
        {
            ["com.apple.quicktime.location.ISO6709"] = "+53.9095+027.5202+224.391/"
        });
        var handler = new RangeHandler(file);
        var videoSource = new YandexDiskVideoSource(new ApiClient("token", new HttpClient(handler)), NullLogger.Instance);

        // Act
        var exif = await videoSource.ReadExifAsync(CreateResource(file.Length), CancellationToken.None);

        // Assert
        Assert.Equal(53.9095, exif.Latitude);
        Assert.Equal("disk:/Camera Uploads/trip.mov", handler.DownloadUrlPath);
        Assert.Equal(1, handler.DownloadUrlCount);
        Assert.InRange(handler.Ranges.Count, 1, 3);
        Assert.True(handler.Ranges.Sum(a => a.To - a.From + 1) <= 3 * RangeReadStream.DefaultBlockSize);
    }

    [Fact]
    public async Task ReadExifAsync_ShouldFail_WhenThePartOfTheFileWithTheMetadataCannotBeDownloaded()
    {
        // Arrange
        var file = QuickTimeFile.Create(mdatSize: 10L * 1024 * 1024, new Dictionary<string, string>
        {
            ["com.apple.quicktime.location.ISO6709"] = "+53.9095+027.5202+224.391/"
        });
        var handler = new RangeHandler(file) { FailFrom = file.Length - file.Moov.Length };
        var videoSource = new YandexDiskVideoSource(new ApiClient("token", new HttpClient(handler)), NullLogger.Instance);

        // Act & Assert: rather than the metadata of the start of the file only, which would be kept as all there is
        await Assert.ThrowsAsync<YandexDiskException>(() =>
            videoSource.ReadExifAsync(CreateResource(file.Length), CancellationToken.None));
    }

    [Fact]
    public async Task ReadExifAsync_ShouldDownloadTheWholeFile_WhenTheServerDoesNotSendParts()
    {
        // Arrange
        var file = QuickTimeFile.Create(mdatSize: 1024 * 1024, new Dictionary<string, string>
        {
            ["com.apple.quicktime.location.ISO6709"] = "+53.9095+027.5202+224.391/"
        });
        var handler = new RangeHandler(file) { IgnoresRanges = true };
        var videoSource = new YandexDiskVideoSource(new ApiClient("token", new HttpClient(handler)), NullLogger.Instance);

        // Act
        var exif = await videoSource.ReadExifAsync(CreateResource(file.Length), CancellationToken.None);

        // Assert: the start of the file comes whole at the first range already, the end of it does not
        Assert.Equal(53.9095, exif.Latitude);
        Assert.Equal(27.5202, exif.Longitude);
        Assert.Equal(3, handler.WholeDownloadCount);
    }

    [Fact]
    public async Task DownloadRangeAsync_ShouldFail_WhenTheServerSendsTheFileFromItsStart()
    {
        // Arrange
        var apiClient = new ApiClient("token", new HttpClient(new IgnoringRangeHandler()));

        // Act & Assert
        await Assert.ThrowsAsync<RangeNotSupportedException>(() =>
            apiClient.DownloadRangeAsync("https://downloader.test/trip.mov", 100, 10, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadRangeAsync_ShouldTakeTheStartOfTheFile_WhenTheServerSendsAllOfIt()
    {
        // Arrange
        var apiClient = new ApiClient("token", new HttpClient(new IgnoringRangeHandler()));

        // Act
        var bytes = await apiClient.DownloadRangeAsync("https://downloader.test/trip.mov", 0, 10, CancellationToken.None);

        // Assert
        Assert.Equal(Enumerable.Range(0, 10).Select(a => (byte)a), bytes);
    }

    [Fact]
    public void RangeReadStream_ShouldDownloadAPartOnce()
    {
        // Arrange
        var bytes = Enumerable.Range(0, 1000).Select(a => (byte)a).ToArray();
        var stream = new RangeReadStream((offset, count, _) => Task.FromResult(bytes.AsSpan((int)offset, count).ToArray()),
            bytes.Length, CancellationToken.None, blockSize: 100);
        var buffer = new byte[250];

        // Act
        stream.Position = 150;
        var read = stream.Read(buffer, 0, 250);
        stream.Position = 160;
        stream.ReadExactly(buffer, 0, 50);
        var readAgain = buffer[..50];
        stream.Position = 990;
        var readAtTheEnd = stream.Read(buffer, 0, 250);

        // Assert
        Assert.Equal(250, read);
        Assert.Equal(bytes[160..210], readAgain);
        Assert.Equal(10, readAtTheEnd);
        Assert.Equal(4, stream.DownloadCount);
    }

    private static Video CreateVideo()
    {
        return new Video
        {
            UserId = 1,
            PhotoSourceId = 1,
            ExternalId = "r1",
            FileName = "trip.mov",
            DateTimeTaken = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }

    private static Resource CreateResource(long size)
    {
        return new Resource
        {
            ResourceId = "r1",
            Name = "trip.mov",
            Path = "disk:/Camera Uploads/trip.mov",
            Type = "file",
            Size = size
        };
    }

    /// <summary>
    /// A QuickTime file as phones write it: the file type, the video itself, then the movie with the metadata.
    /// The video itself is zeros, which are not kept, so that a file can be as large as a real one.
    /// </summary>
    internal sealed class QuickTimeFile
    {
        private QuickTimeFile(byte[] head, long mdatSize, byte[] moov)
        {
            Head = head;
            MdatSize = mdatSize;
            Moov = moov;
        }

        public byte[] Head { get; }
        public long MdatSize { get; }
        public byte[] Moov { get; }
        public long Length => Head.Length + MdatSize + Moov.Length;

        public static QuickTimeFile Create(long mdatSize, IReadOnlyDictionary<string, string> metadata)
        {
            var ftyp = Box("ftyp", [.. Encoding.ASCII.GetBytes("qt  "), 0, 0, 0, 0, .. Encoding.ASCII.GetBytes("qt  ")]);
            var mdatHeader = new byte[16];
            BinaryPrimitives.WriteUInt32BigEndian(mdatHeader, 1);
            Encoding.ASCII.GetBytes("mdat").CopyTo(mdatHeader, 4);
            BinaryPrimitives.WriteUInt64BigEndian(mdatHeader.AsSpan(8), (ulong)(16 + mdatSize));

            var moov = Box("moov", [.. MovieHeader(), .. Meta(metadata)]);

            return new QuickTimeFile([.. ftyp, .. mdatHeader], mdatSize, moov);
        }

        public byte[] ToArray()
        {
            return [.. Head, .. new byte[MdatSize], .. Moov];
        }

        public byte[] GetRange(long from, long to)
        {
            var result = new byte[to - from + 1];
            var mdatEnd = Head.Length + MdatSize;

            for (var i = from; i <= to; i++)
            {
                result[i - from] = i < Head.Length ? Head[i] : i < mdatEnd ? (byte)0 : Moov[i - mdatEnd];
            }

            return result;
        }

        private static byte[] MovieHeader()
        {
            var payload = new byte[100];
            var created = (uint)(MovieCreated - new DateTimeOffset(1904, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds;
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4), created);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(8), created);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(12), 1000);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(16), 25500);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(20), 0x00010000);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(24), 0x0100);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(96), 2);

            return Box("mvhd", payload);
        }

        /// <summary>
        /// The metadata of the keys given, as Apple writes it: a handler of mdta, the keys, then their values.
        /// </summary>
        private static byte[] Meta(IReadOnlyDictionary<string, string> metadata)
        {
            var hdlr = Box("hdlr", [0, 0, 0, 0, 0, 0, 0, 0, .. Encoding.ASCII.GetBytes("mdta"), .. new byte[12], 0]);

            var keys = new List<byte> { 0, 0, 0, 0 };
            keys.AddRange(UInt32(metadata.Count));
            var ilst = new List<byte>();
            var index = 1;

            foreach (var (key, value) in metadata)
            {
                var keyBytes = Encoding.ASCII.GetBytes(key);
                keys.AddRange(UInt32(8 + keyBytes.Length));
                keys.AddRange(Encoding.ASCII.GetBytes("mdta"));
                keys.AddRange(keyBytes);

                // a value of UTF-8 text, of the default locale
                var data = Box("data", [.. UInt32(1), .. UInt32(0), .. Encoding.UTF8.GetBytes(value)]);
                ilst.AddRange(UInt32(8 + data.Length));
                ilst.AddRange(UInt32(index++));
                ilst.AddRange(data);
            }

            return Box("meta", [.. hdlr, .. Box("keys", [.. keys]), .. Box("ilst", [.. ilst])]);
        }

        private static byte[] Box(string type, byte[] payload)
        {
            return [.. UInt32(8 + payload.Length), .. Encoding.ASCII.GetBytes(type), .. payload];
        }

        private static byte[] UInt32(int value)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);

            return bytes;
        }
    }

    /// <summary>
    /// The download URL of a path, and the ranges of the file by it, as Yandex.Disk serves them.
    /// </summary>
    internal sealed class RangeHandler(QuickTimeFile file) : HttpMessageHandler
    {
        public string? DownloadUrlPath { get; private set; }
        public int DownloadUrlCount { get; private set; }
        public List<(long From, long To)> Ranges { get; } = [];
        public List<string> DownloadUrlPaths { get; } = [];

        /// <summary>
        /// The ranges from it fail.
        /// </summary>
        public long? FailFrom { get; init; }

        /// <summary>
        /// The whole file is sent whatever range is asked for.
        /// </summary>
        public bool IgnoresRanges { get; init; }

        /// <summary>
        /// The token is not accepted.
        /// </summary>
        public bool Unauthorized { get; init; }

        public int WholeDownloadCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            if (Unauthorized)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"error\":\"UnauthorizedError\",\"description\":\"Unauthorized\"}")
                });
            }

            if (uri.Host == "downloader.test" && (IgnoresRanges || request.Headers.Range == null))
            {
                lock (Ranges)
                {
                    WholeDownloadCount++;
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(file.ToArray())
                });
            }

            if (uri.Host == "downloader.test")
            {
                var range = request.Headers.Range!.Ranges.Single();
                var from = range.From!.Value;
                var to = Math.Min(range.To!.Value, file.Length - 1);
                lock (Ranges)
                {
                    Ranges.Add((from, to));
                }

                if (to >= FailFrom)
                {
                    // a status that is not retried, so that the test does not wait for the retries
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("{}")
                    });
                }

                var content = new ByteArrayContent(file.GetRange(from, to));
                content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, file.Length);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content });
            }

            lock (Ranges)
            {
                DownloadUrlPath = HttpUtility.ParseQueryString(uri.Query)["path"];
                DownloadUrlPaths.Add(DownloadUrlPath!);
                DownloadUrlCount++;
            }

            var json = new StringContent(
                JsonSerializer.Serialize(new { href = "https://downloader.test/trip.mov", method = "GET", templated = false }),
                Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = json });
        }
    }

    /// <summary>
    /// A server that sends the whole file whatever range is asked for.
    /// </summary>
    private sealed class IgnoringRangeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Enumerable.Range(0, 200).Select(a => (byte)a).ToArray())
            });
        }
    }
}
