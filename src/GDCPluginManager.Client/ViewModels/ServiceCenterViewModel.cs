using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al ServiceCenterCard din ContentView.swift — partener de
/// service/reparatii echipament foto-video (drone/camere/optica/urgente).
public sealed partial class ServiceCenterViewModel : ObservableObject
{
    public ServiceCenter Center { get; }

    public ServiceCenterViewModel(ServiceCenter center)
    {
        Center = center;
        Cover = new CoverViewModel(center.CoverImageUrl, center.Name);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => Center.Scheduling?.CountdownText;

    public string Name => Center.Name;
    public string Specialization => Center.Specialization;
    public ServiceCategory Category => Center.Category;
    public CoverViewModel Cover { get; }

    [RelayCommand]
    private void Contact() => Process.Start(new ProcessStartInfo(Center.ContactURL) { UseShellExecute = true });

    public bool HasWebsite => !string.IsNullOrEmpty(Center.WebsiteURL);

    [RelayCommand]
    private void OpenWebsite()
    {
        if (Center.WebsiteURL is { } url) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ---- Buton harta (Etapa 5, 2026-08-29) -------------------------------
    // MapsLink returneaza null pentru adrese goale SAU non-fizice ("Online",
    // "Webinar", "la distanță"...), deci butonul pur si simplu NU se randeaza
    // in acele cazuri — nu apare dezactivat. Port 1:1 al deciziei de pe Mac.
    public bool HasMaps => Center.MapsUrl is not null;

    [RelayCommand]
    private void OpenMaps()
    {
        if (Center.MapsUrl is not { } url) return;
        // AbsoluteUri, NU ToString(): ToString() intoarce forma
        // DEZESCAPATA (spatii brute, diacritice ne-encodate), care ar
        // ajunge asa la ShellExecute. AbsoluteUri pastreaza
        // percent-encoding-ul corect. Verificat direct.
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }
}
