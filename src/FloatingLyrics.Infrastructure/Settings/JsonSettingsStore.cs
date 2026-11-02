using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FloatingLyrics.Infrastructure.Settings;

public sealed class JsonSettingsStore(string directory, ILogger<JsonSettingsStore> logger)
{
    private readonly string _path = Path.Combine(directory, "settings.json");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string DirectoryPath => directory;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path)) return new();
            await using var stream = File.OpenRead(_path);
            return (await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken) ?? new()).Validate();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning("Settings could not be loaded ({ErrorType}); defaults will be used.", ex.GetType().Name);
            return new();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(directory);
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(settings.Validate(), Options), cancellationToken);
            File.Move(temporary, _path, true);
        }
        finally { _gate.Release(); }
    }
}
