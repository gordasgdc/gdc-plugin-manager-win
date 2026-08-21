using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

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
    }

    public string Name => App.Name;
    public bool HasYoutube => !string.IsNullOrWhiteSpace(App.YoutubeURL);

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
