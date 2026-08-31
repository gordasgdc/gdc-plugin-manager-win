using System.Windows.Threading;

namespace GDCPluginManager.Client.Services;

/// Ceas partajat pentru toate badge-urile de countdown de pe carduri
/// (ProductViewModel, CourseViewModel, etc.) — port 1:1 al
/// `Timer.publish(every: 60...)` din CountdownBadge (Mac, ContentView.swift).
/// Un singur DispatcherTimer pentru toata aplicatia, nu cate unul per card —
/// zeci de carduri simultan nu au nevoie de zeci de timere identice.
public static class CountdownRefreshTimer
{
    public static event Action? Tick;

    private static readonly DispatcherTimer Timer = new()
    {
        Interval = TimeSpan.FromSeconds(60),
    };

    static CountdownRefreshTimer()
    {
        Timer.Tick += (_, _) => Tick?.Invoke();
        Timer.Start();
    }
}
