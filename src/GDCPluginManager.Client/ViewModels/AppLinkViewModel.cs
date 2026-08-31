using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al AppCard din ContentView.swift — link catre o alta aplicatie
/// GDC (DataMover, CursorPro etc.), fara descriere in model (AppLink.swift
/// nu are camp de descriere — doar id/name/url/youtubeURL), doar nume +
/// buton Deschide + iconita optionala de tutorial.
public sealed partial class AppLinkViewModel : ObservableObject
{
    public AppLink App { get; }

    public AppLinkViewModel(AppLink app)
    {
        App = app;
        // Dupa atribuirea de mai sus: `App` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp
        // (acelasi pattern ca PartnerStoreViewModel).
        Cover = new CoverViewModel(app.CoverImageUrl, app.Name);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => App.Scheduling?.CountdownText;

    public string Name => App.Name;
    public bool HasYoutube => !string.IsNullOrWhiteSpace(App.YoutubeURL);

    /// Coperta cardului + acțiunea de mărire. Vezi CoverViewModel: o
    /// singură implementare, folosită de toate tipurile de card.
    public CoverViewModel Cover { get; }

    [RelayCommand]
    private void Open() => Process.Start(new ProcessStartInfo(App.Url) { UseShellExecute = true });

    [RelayCommand]
    private void OpenTutorial()
    {
        if (!string.IsNullOrWhiteSpace(App.YoutubeURL))
        {
            Process.Start(new ProcessStartInfo(App.YoutubeURL) { UseShellExecute = true });
        }
    }
}
