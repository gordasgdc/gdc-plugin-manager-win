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
        // Dupa atribuirea de mai sus: `Event` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp.
        Cover = new CoverViewModel(ev.CoverImageUrl, ev.Title);
    }

    public string Title => Event.Title;

    /// Coperta cardului + actiunea de marire. Vezi CoverViewModel:
    /// o singura implementare, folosita de toate cele cinci tipuri de card.
    /// Se creeaza o data, in constructor, nu la fiecare acces — altfel WPF
    /// ar primi un obiect nou la fiecare redesenare si ar reincarca imaginea.
    public CoverViewModel Cover { get; }
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

    // ---- Buton harta (Etapa 5, 2026-08-29) -------------------------------
    // MapsLink returneaza null pentru adrese goale SAU non-fizice ("Online",
    // "Webinar", "la distanță"...), deci butonul pur si simplu NU se randeaza
    // in acele cazuri — nu apare dezactivat. Port 1:1 al deciziei de pe Mac.
    public bool HasMaps => Event.MapsUrl is not null;

    [RelayCommand]
    private void OpenMaps()
    {
        if (Event.MapsUrl is not { } url) return;
        // AbsoluteUri, NU ToString(): ToString() intoarce forma
        // DEZESCAPATA (spatii brute, diacritice ne-encodate), care ar
        // ajunge asa la ShellExecute. AbsoluteUri pastreaza
        // percent-encoding-ul corect. Verificat direct.
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }
}
