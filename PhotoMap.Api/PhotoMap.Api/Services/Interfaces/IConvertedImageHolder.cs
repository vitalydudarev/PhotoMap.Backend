using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoMap.Api.Services.Interfaces
{
    public interface IConvertedImageHolder
    {
        void Add(Guid id, byte[] bytes);

        /// <summary>
        /// Waits for the image with the given ID to be converted. Returns null if it does not
        /// arrive within the timeout.
        /// </summary>
        Task<byte[]?> WaitAsync(Guid id, TimeSpan timeout, CancellationToken cancellationToken = default);
    }
}
