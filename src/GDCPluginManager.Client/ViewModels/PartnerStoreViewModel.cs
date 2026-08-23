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
        // Dupa atribuirea de mai sus: `Store` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp.
        Cover = new CoverViewModel(store.CoverImageUrl, store.Name);
    }

    public string Name => Store.Name;

    /// Coperta cardului + actiunea de marire. Vezi CoverViewModel:
    /// o singura implementare, folosita de toate cele cinci tipuri de card.
    /// Se creeaza o data, in constructor, nu la fiecare acces — altfel WPF
    /// ar primi un obiect nou la fiecare redesenare si ar reincarca imaginea.
    public CoverViewModel Cover { get; }
    public string Description => Store.Description;

    [RelayCommand]
    private void Visit() => Process.Start(new ProcessStartInfo(Store.Url) { UseShellExecute = true });
}
