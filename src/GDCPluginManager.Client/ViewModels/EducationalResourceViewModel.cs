using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al EducationalResourceCard din ContentView.swift — carte/curs
/// online/ghid vandut de o terta parte. Butonul principal duce direct la
/// ExternalURL (Cumpara), nu prin WhatsApp ca la Course.
public sealed partial class EducationalResourceViewModel : ObservableObject
{
    public EducationalResource Resource { get; }

    public EducationalResourceViewModel(EducationalResource resource)
    {
        Resource = resource;
        // Dupa atribuirea de mai sus: `Resource` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp.
        Cover = new CoverViewModel(resource.CoverImageUrl, resource.Name);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => Resource.Scheduling?.CountdownText;

    public string Name => Resource.Name;

    /// Coperta cardului + actiunea de marire. Vezi CoverViewModel:
    /// o singura implementare, folosita de toate cele cinci tipuri de card.
    /// Se creeaza o data, in constructor, nu la fiecare acces — altfel WPF
    /// ar primi un obiect nou la fiecare redesenare si ar reincarca imaginea.
    public CoverViewModel Cover { get; }
    public string Description => Resource.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    [ObservableProperty]
    private bool _isDescriptionExpanded;

    [RelayCommand]
    private void ToggleDescription() => IsDescriptionExpanded = !IsDescriptionExpanded;
    public string KindLabel => Resource.Kind.Label();
    public bool HasYoutube => !string.IsNullOrWhiteSpace(Resource.YoutubeURL);

    [RelayCommand]
    private void Buy() => Process.Start(new ProcessStartInfo(Resource.ExternalURL) { UseShellExecute = true });

    [RelayCommand]
    private void OpenTutorial()
    {
        if (!string.IsNullOrWhiteSpace(Resource.YoutubeURL))
        {
            Process.Start(new ProcessStartInfo(Resource.YoutubeURL) { UseShellExecute = true });
        }
    }
}
