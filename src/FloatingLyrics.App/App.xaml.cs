using System.IO;
using System.Net.Http;
using System.Windows;
using FloatingLyrics.Infrastructure.Logging;
using FloatingLyrics.Infrastructure.Settings;
using FloatingLyrics.Media.WindowsSessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FloatingLyrics.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private Mutex? _instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new(true, "Local\\FloatingLyrics.Desktop.0.1", out var isNew);
        if (!isNew) { MessageBox.Show("Floating Lyrics is already running. Open Settings from its system tray icon.", "Floating Lyrics"); Shutdown(); return; }
        try
        {
            var dataIndex = Array.IndexOf(e.Args, "--data-dir");
            var directory = dataIndex >= 0 && dataIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[dataIndex + 1]) : e.Args.Contains("--portable") ? Path.Combine(AppContext.BaseDirectory, "Data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloatingLyrics");
            var collection = new ServiceCollection();
            collection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
            collection.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(Path.Combine(directory, "Logs")));
            collection.AddSingleton(sp => new JsonSettingsStore(directory, sp.GetRequiredService<ILogger<JsonSettingsStore>>()));
            collection.AddSingleton<WindowsMediaSessionService>();
            collection.AddSingleton(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
            _services = collection.BuildServiceProvider();
            _services.GetRequiredService<ILogger<App>>().LogInformation("Floating Lyrics 0.1.0 starting.");
            var settingsStore = _services.GetRequiredService<JsonSettingsStore>();
            var loaded = await settingsStore.LoadAsync();
            var controller = new ApplicationController(_services.GetRequiredService<WindowsMediaSessionService>(), settingsStore, _services.GetRequiredService<HttpClient>(), _services.GetRequiredService<ILogger<ApplicationController>>(), loaded);
            _controller = controller;
            await controller.StartAsync(e.Args.Contains("--smoke-test"));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Floating Lyrics could not start ({ex.GetType().Name}). Check the local logs and run the diagnostics tool.", "Floating Lyrics", MessageBoxButton.OK, MessageBoxImage.Error);
            await ShutdownAsync(1);
        }
    }
    private ApplicationController? _controller;
    private Task? _shutdownTask;

    internal Task ShutdownAsync(int exitCode = 0) => _shutdownTask ??= ShutdownCoreAsync(exitCode);

    private async Task ShutdownCoreAsync(int exitCode)
    {
        try
        {
            try { if (_controller is not null) await _controller.DisposeAsync(); }
            finally
            {
                if (_services is not null)
                {
                    _services.GetRequiredService<ILogger<App>>().LogInformation("Floating Lyrics shutdown completed.");
                    await _services.DisposeAsync();
                }
            }
        }
        finally { Shutdown(exitCode); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
