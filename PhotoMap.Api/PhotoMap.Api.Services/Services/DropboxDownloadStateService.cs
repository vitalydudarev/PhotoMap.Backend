using Microsoft.Extensions.Logging;

namespace PhotoMap.Api.Services.Services;

public class DropboxDownloadStateService : IDropboxDownloadStateService
{
    private const string FileName = "dropbox-data.json";
    private readonly Dictionary<long, DropboxDownloadState> _map = new();
    private readonly ILogger<DropboxDownloadStateService> _logger;

    public DropboxDownloadStateService(ILogger<DropboxDownloadStateService> logger)
    {
        _logger = logger;

        _logger.LogInformation("Directory: {Directory}", Directory.GetCurrentDirectory());

        if (File.Exists(FileName))
        {
            var fileContents = File.ReadAllText(FileName);
            var list = System.Text.Json.JsonSerializer.Deserialize<List<DropboxDownloadState>>(fileContents);

            if (list != null)
            {
                _map = list.ToDictionary(a => a.UserId, b => b);
            }
        }
    }

    public DropboxDownloadState? GetState(long userId)
    {
        _map.TryGetValue(userId, out var data);

        return data;
    }

    public void SaveState(DropboxDownloadState state)
    {
        if (!_map.TryGetValue(state.UserId, out _))
        {
            state.LastAccessTime = DateTimeOffset.UtcNow;
            _map.Add(state.UserId, state);
        }

        var values = _map.Values.ToList();

        // TODO: make async
        var fileContents = System.Text.Json.JsonSerializer.Serialize(values);
        
        File.WriteAllText(FileName, fileContents);

        _logger.LogInformation("Directory: {Directory}", Directory.GetCurrentDirectory());
        _logger.LogInformation("Saved");
    }
}