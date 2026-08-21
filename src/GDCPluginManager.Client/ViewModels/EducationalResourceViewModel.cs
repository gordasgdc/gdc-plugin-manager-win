using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

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
    }

    public string Name => Resource.Name;
    public string Description => Resource.Description;
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
