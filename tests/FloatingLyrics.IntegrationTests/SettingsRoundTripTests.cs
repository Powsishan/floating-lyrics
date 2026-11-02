using System.Text.Json;
using FloatingLyrics.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloatingLyrics.IntegrationTests;

public sealed class SettingsRoundTripTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "FloatingLyrics.Tests", Guid.NewGuid().ToString("N"));
    private JsonSettingsStore Store() => new(directory, NullLogger<JsonSettingsStore>.Instance);

    [Fact]
    public async Task FreshInstallHasSafeDefaultsAndNoInventedProviderIdentity()
    {
        var settings = await Store().LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(settings.Locked);
        Assert.True(settings.AlwaysOnTop);
        Assert.Equal("", settings.LrcLibClientUrl);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task SettingsAndPlacementSurviveNewStoreInstance()
    {
        var expected = new AppSettings
        {
            Locked = false, FontSize = 41, FontFamily = "Segoe UI", TextColor = "#FFCCDDEE",
            TextOpacity = .65, Left = -1000, Top = 725, OverlayWidth = 740, TimingOffsetMs = -350,
            PreferredSourceId = "synthetic-test-player", LrcLibClientUrl = ""
        };
        await Store().SaveAsync(expected, TestContext.Current.CancellationToken);
        var actual = await Store().LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, actual);
        Assert.False(File.Exists(Path.Combine(directory, "settings.json.tmp")));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "settings.json"), TestContext.Current.CancellationToken));
        Assert.Equal(41, json.RootElement.GetProperty("FontSize").GetDouble());
    }

    [Fact]
    public async Task CorruptJsonRecoversToDefaults()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"), "{broken", TestContext.Current.CancellationToken);
        Assert.Equal(new AppSettings(), await Store().LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PersistedOutOfRangeValuesAreNormalizedOnLoad()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "settings.json"),
            """{"FontSize":10000,"OverlayWidth":1,"TextOpacity":0,"TimingOffsetMs":100000,"TextColor":"invalid"}""",
            TestContext.Current.CancellationToken);
        var loaded = await Store().LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(96, loaded.FontSize);
        Assert.Equal(240, loaded.OverlayWidth);
        Assert.Equal(.2, loaded.TextOpacity);
        Assert.Equal(30000, loaded.TimingOffsetMs);
        Assert.Equal("#FFFFFFFF", loaded.TextColor);
        Assert.Equal("", loaded.LrcLibClientUrl);
    }

    [Fact]
    public async Task ConcurrentWritesProduceOneCompleteValidatedDocument()
    {
        var store = Store();
        await Task.WhenAll(Enumerable.Range(20, 30).Select(size => store.SaveAsync(new AppSettings { FontSize = size }, TestContext.Current.CancellationToken)));
        var loaded = await Store().LoadAsync(TestContext.Current.CancellationToken);
        Assert.InRange(loaded.FontSize, 20, 49);
        Assert.False(File.Exists(Path.Combine(directory, "settings.json.tmp")));
    }

    public void Dispose()
    {
        // This unique fixture directory is the only deletion target; no user settings are touched.
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
