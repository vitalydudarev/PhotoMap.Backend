namespace PhotoMap.Shared.Yandex.Disk
{
    /// <summary>
    /// The server does not send the range of the file asked for: it sends the file from its start, or refuses the
    /// range. The file is then only had by downloading all of it; asking again would go the same way.
    /// </summary>
    public class RangeNotSupportedException : Exception
    {
        public RangeNotSupportedException(string message) : base(message)
        {
        }
    }
}
