using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PhotoMap.Api.Hubs.Models;

namespace PhotoMap.Api.Hubs;

public class NotificationHub : Hub
{
    private readonly Dictionary<long, string> _userConnections = new();
    
    public override Task OnConnectedAsync()
    {
        var userIdQueryParameter = Context.GetHttpContext()?.Request.Query["userId"];
        if (!string.IsNullOrEmpty(userIdQueryParameter?.ToString()))
        {
            var userId = long.Parse(userIdQueryParameter.ToString() ?? string.Empty);
            var connectionId = Context.ConnectionId;

            _userConnections.TryAdd(userId, connectionId);
        }
        
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        var key = _userConnections.FirstOrDefault(a => a.Value == connectionId).Key;
        if (key != 0)
        {
            _userConnections.Remove(key);
        }
        
        return base.OnDisconnectedAsync(exception);
    }
    
    public async Task SendErrorAsync(long userId, long sourceId, string errorText)
    {
        if (_userConnections.TryGetValue(userId, out var connectionId))
        {
            var hubErrorModel = new HubErrorModel(sourceId, errorText);
            await Clients.Client(connectionId).SendAsync("Error", hubErrorModel);
        }
    }

    public async Task SendProgressAsync(long userId, long sourceId, int processed, int total)
    {
        if (_userConnections.TryGetValue(userId, out var connectionId))
        {
            var hubProgressModel = new HubProgressModel(sourceId, processed, total);
            await Clients.Client(connectionId).SendAsync("Progress", hubProgressModel);
        }
    }
}