using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al EventCard din ContentView.swift — anunt de comunitate
/// (workshop, curs, festival). Buton principal spre ExternalURL
/// (detalii/inscriere).
public sealed partial class EventViewModel : ObservableObject
{
    public Event Event { get; }

    public EventViewModel(Event ev)
    {
        Event = ev;
    }

    public string Title => Event.Title;
    public string Description => Event.Description;
    public string DateAndLocation => $"{Event.DateDisplay} · {Event.Location}";
    public bool HasYoutube => !string.IsNullOrWhiteSpace(Event.YoutubeURL);

    [RelayCommand]
    private void OpenDetails() => Process.Start(new ProcessStartInfo(Event.ExternalURL) { UseShellExecute = true });

    [RelayCommand]
    private void OpenTutorial()
    {
        if (!string.IsNullOrWhiteSpace(Event.YoutubeURL))
        {
            Process.Start(new ProcessStartInfo(Event.YoutubeURL) { UseShellExecute = true });
        }
    }
}
