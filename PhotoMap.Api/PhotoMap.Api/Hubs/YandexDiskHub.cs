using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhotoMap.Api.Handlers;

namespace PhotoMap.Api.Hubs
{
    // TODO: use one hub for both Dropbox and Yandex.Disk
    public class YandexDiskHub : Hub
    {
        private readonly Dictionary<long, HashSet<string>> _map = new Dictionary<long, HashSet<string>>();

        public void RegisterClient(long userId)
        {
            var connectionId = Context.ConnectionId;

            if (_map.TryGetValue(userId, out var connectionsIds))
                connectionsIds.Add(connectionId);
            else
                _map.Add(userId, new HashSet<string> { connectionId });
        }

        public async Task SendErrorAsync(long userId, string errorText)
        {
            if (_map.TryGetValue(userId, out var connectionIds))
            {
                await Clients.Clients(connectionIds.ToList()).SendAsync("YandexDiskError", errorText);
            }
        }

        public async Task SendProgressAsync(long userId, Progress processed)
        {
            if (_map.TryGetValue(userId, out var connectionIds))
            {
                await Clients.Clients(connectionIds.ToList()).SendAsync("YandexDiskProgress", processed);
            }
        }
    }
}
