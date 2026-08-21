using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al PartnerStoreCard din ContentView.swift — magazin partener de
/// echipament foto-video. Doar nume/descriere + buton Viziteaza -> Url.
public sealed partial class PartnerStoreViewModel : ObservableObject
{
    public PartnerStore Store { get; }

    public PartnerStoreViewModel(PartnerStore store)
    {
        Store = store;
    }

    public string Name => Store.Name;
    public string Description => Store.Description;

    [RelayCommand]
    private void Visit() => Process.Start(new ProcessStartInfo(Store.Url) { UseShellExecute = true });
}
