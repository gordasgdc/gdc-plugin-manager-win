using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al AudioCard din ContentView.swift — element din sectiunea
/// "Audio", modelat pe AppLinkViewModel dar cu Description in plus
/// (AudioTrack.swift/.cs are camp de descriere, spre deosebire de AppLink).
public sealed partial class AudioTrackViewModel : ObservableObject
{
    public AudioTrack Track { get; }

    public AudioTrackViewModel(AudioTrack track)
    {
        Track = track;
        Cover = new CoverViewModel(track.CoverImageUrl, track.Name);
    }

    public string Name => Track.Name;
    public string Description => Track.Description;
    public bool HasYoutube => !string.IsNullOrWhiteSpace(Track.YoutubeURL);

    /// Coperta cardului + acțiunea de mărire — aceeași implementare unică
    /// folosită de toate tipurile de card.
    public CoverViewModel Cover { get; }

    [RelayCommand]
    private void Open() => Process.Start(new ProcessStartInfo(Track.Url) { UseShellExecute = true });

    [RelayCommand]
    private void OpenTutorial()
    {
        if (!string.IsNullOrWhiteSpace(Track.YoutubeURL))
        {
            Process.Start(new ProcessStartInfo(Track.YoutubeURL) { UseShellExecute = true });
        }
    }
}
