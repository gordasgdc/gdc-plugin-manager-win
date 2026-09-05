using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.ViewModels;

/// O adresă suplimentară (sediu/magazin) afișată ca rând separat, cu
/// propriul buton hartă — Multi-Locație (2026-09-05), folosit de
/// `PartnerStoreViewModel.AdditionalAddresses`/`ServiceCenterViewModel.
/// AdditionalAddresses`. Port 1:1 al `ForEach(store.additionalAddresses)`
/// din ContentView.swift (Mac).
public sealed partial class AddressLinkViewModel : ObservableObject
{
    public string Address { get; }

    public AddressLinkViewModel(string address)
    {
        Address = address;
    }

    public bool HasMaps => MapsUrl is not null;
    private System.Uri? MapsUrl => MapsLink.Url(Address);

    [RelayCommand]
    private void OpenMaps()
    {
        if (MapsUrl is not { } url) return;
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }
}
