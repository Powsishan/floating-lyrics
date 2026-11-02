using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FloatingLyrics.Platform.Overlay;
using Xunit;

namespace FloatingLyrics.Platform.Tests;

public class OverlayInteropTests
{
    [Fact]
    public async Task LayeredOverlayPassesHitTestingThroughWhenLockedAndReceivesItWhenUnlocked()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            Window? underneath = null;
            Window? overlay = null;
            try
            {
                underneath = new Window { Title = "Floating Lyrics native test target", Width = 340, Height = 220, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Background = Brushes.White, Topmost = true };
                underneath.Show();
                OverlayInterop.Place(underneath, 150, 150);
                overlay = new Window { Title = "Floating Lyrics native test overlay", Width = 340, Height = 220, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false, ShowActivated = false, Content = new Border { Background = Brushes.Black } };
                overlay.Show();
                OverlayInterop.Place(overlay, 150, 150);
                OverlayInterop.Configure(overlay, locked: true, topmost: true);
                var target = underneath;
                var layer = overlay;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        var handle = new WindowInteropHelper(layer).Handle;
                        var targetHandle = new WindowInteropHelper(target).Handle;
                        var styles = GetWindowLongPtr(handle, -20).ToInt64();
                        Assert.True((styles & 0x80000) != 0, "Transparent WPF window must use WS_EX_LAYERED.");
                        Assert.True((styles & 0x08000000) != 0, "WS_EX_NOACTIVATE must prevent focus stealing.");
                        Assert.True((styles & 0x80) != 0, "WS_EX_TOOLWINDOW must keep the overlay out of Alt+Tab.");
                        Assert.True((styles & 0x8) != 0, "Topmost style must be present.");
                        var position = OverlayInterop.GetPosition(layer);
                        var point = new NativePoint((int)position.X + 30, (int)position.Y + 30);
                        Assert.Equal(targetHandle, WindowFromPoint(point));
                        OverlayInterop.Configure(layer, locked: false, topmost: true);
                        Assert.Equal(handle, WindowFromPoint(point));
                        OverlayInterop.Configure(layer, locked: true, topmost: false);
                        Assert.Equal(0, GetWindowLongPtr(handle, -20).ToInt64() & 0x8);
                        OverlayInterop.Place(layer, -100000, -100000);
                        position = OverlayInterop.GetPosition(layer);
                        Assert.True(position.X > -100000 && position.Y > -100000, "Off-screen placements must recover to a connected monitor.");
                        completion.TrySetResult();
                    }
                    catch (Exception exception) { completion.TrySetException(exception); }
                    finally { layer.Close(); target.Close(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
                };
                timer.Start();
                Dispatcher.Run();
            }
            catch (Exception exception) { overlay?.Close(); underneath?.Close(); completion.TrySetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [StructLayout(LayoutKind.Sequential)] private readonly record struct NativePoint(int X, int Y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
}
