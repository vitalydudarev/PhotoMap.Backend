using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using PhotoMap.Api.Services.Interfaces;

namespace PhotoMap.Api.Services.Implementations
{
    public class ConvertedImageHolder : IConvertedImageHolder
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<byte[]>> _holder = new();

        public void Add(Guid id, byte[] bytes)
        {
            GetOrAdd(id).TrySetResult(bytes);
        }

        public async Task<byte[]?> WaitAsync(Guid id, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var completionSource = GetOrAdd(id);

            try
            {
                return await completionSource.Task.WaitAsync(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                return null;
            }
            finally
            {
                // the image is handed over to a single waiter, so it is not kept any longer
                _holder.TryRemove(id, out _);
            }
        }

        private TaskCompletionSource<byte[]> GetOrAdd(Guid id)
        {
            return _holder.GetOrAdd(id, _ => new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }
}
