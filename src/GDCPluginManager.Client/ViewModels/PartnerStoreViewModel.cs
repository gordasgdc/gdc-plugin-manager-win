using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al PartnerStoreCard din ContentView.swift — magazin partener de
/// echipament foto-video. Doar nume/descriere + buton Viziteaza -> Url.
public sealed partial class PartnerStoreViewModel : ObservableObject
{
    public PartnerStore Store { get; }

    public PartnerStoreViewModel(PartnerStore store)
    {
        Store = store;
        // Dupa atribuirea de mai sus: `Store` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp.
        Cover = new CoverViewModel(store.CoverImageUrl, store.Name);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => Store.Scheduling?.CountdownText;

    public string Name => Store.Name;

    /// Coperta cardului + actiunea de marire. Vezi CoverViewModel:
    /// o singura implementare, folosita de toate cele cinci tipuri de card.
    /// Se creeaza o data, in constructor, nu la fiecare acces — altfel WPF
    /// ar primi un obiect nou la fiecare redesenare si ar reincarca imaginea.
    public CoverViewModel Cover { get; }
    public string Description => Store.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    [ObservableProperty]
    private bool _isDescriptionExpanded;

    [RelayCommand]
    private void ToggleDescription() => IsDescriptionExpanded = !IsDescriptionExpanded;

    [RelayCommand]
    private void Visit() => Process.Start(new ProcessStartInfo(Store.Url) { UseShellExecute = true });

    // ---- Buton harta (Etapa 5, 2026-08-29) -------------------------------
    // MapsLink returneaza null pentru adrese goale SAU non-fizice ("Online",
    // "Webinar", "la distanță"...), deci butonul pur si simplu NU se randeaza
    // in acele cazuri — nu apare dezactivat. Port 1:1 al deciziei de pe Mac.
    public bool HasMaps => Store.MapsUrl is not null;

    [RelayCommand]
    private void OpenMaps()
    {
        if (Store.MapsUrl is not { } url) return;
        // AbsoluteUri, NU ToString(): ToString() intoarce forma
        // DEZESCAPATA (spatii brute, diacritice ne-encodate), care ar
        // ajunge asa la ShellExecute. AbsoluteUri pastreaza
        // percent-encoding-ul corect. Verificat direct.
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }
}
