using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

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
    }

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
}
