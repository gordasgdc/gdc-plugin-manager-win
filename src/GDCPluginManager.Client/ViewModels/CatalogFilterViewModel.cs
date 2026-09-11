using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.ViewModels;

// Port 1:1 al CatalogFilterState/CatalogFilterBar din Swift (Regula 31).
// Filtreaza EXCLUSIV pe ResolvedAccess, deci regula „ce inseamna gratuit"
// se aplica o singura data, in Core — nicio sectiune nu o reinterpreteaza.

public enum AccessPriceFilter { All, Free, Paid }
public enum AccessOSFilter { All, Mac, Windows }

public sealed class CatalogFilterViewModel : INotifyPropertyChanged
{
    private AccessPriceFilter _price = AccessPriceFilter.All;
    private AccessOSFilter _os = AccessOSFilter.All;
    private CatalogGroup? _group;
    private string _tag = "";

    public AccessPriceFilter Price { get => _price; set => Set(ref _price, value); }
    public AccessOSFilter OS { get => _os; set => Set(ref _os, value); }
    public CatalogGroup? Group { get => _group; set => Set(ref _group, value); }
    public string Tag { get => _tag; set => Set(ref _tag, value ?? ""); }

    /// Etichetele si grupurile prezente REAL in sectiunea curenta — un filtru
    /// fara nicio valoare nu se afiseaza deloc (vezi HasTags/HasGroups).
    public ObservableCollection<string> AvailableTags { get; } = [];
    public ObservableCollection<CatalogGroup> AvailableGroups { get; } = [];

    public bool HasTags => AvailableTags.Count > 0;
    public bool HasGroups => AvailableGroups.Count > 0;

    public bool Matches(ResolvedAccess access)
    {
        // isFree == null inseamna NECUNOSCUT, nu „platit": un element fara
        // informatie de pret apare doar la „Toate", niciodata clasificat gresit.
        var priceOk = Price switch
        {
            AccessPriceFilter.Free => access.IsFree == true,
            AccessPriceFilter.Paid => access.IsFree == false,
            _ => true
        };

        // Platforma necunoscuta trece prin orice filtru (fail-open).
        var osOk = access.SupportedOS is not { } os || OS switch
        {
            AccessOSFilter.Mac => os is SupportedOS.MacOS or SupportedOS.CrossPlatform,
            AccessOSFilter.Windows => os is SupportedOS.Windows or SupportedOS.CrossPlatform,
            _ => true
        };

        var groupOk = Group is null || access.Group == Group;
        var tagOk = string.IsNullOrEmpty(Tag) || access.Tags.Contains(Tag);

        return priceOk && osOk && groupOk && tagOk;
    }

    public IEnumerable<T> Filter<T>(IEnumerable<T> items) where T : IAccessDescribing =>
        items.Where(i => Matches(i.ResolvedAccess));

    /// Reincarca valorile disponibile din continutul real al sectiunii.
    public void RefreshFacets<T>(IEnumerable<T> items) where T : IAccessDescribing
    {
        var list = items.ToList();

        AvailableTags.Clear();
        foreach (var tag in list.SelectMany(i => i.ResolvedAccess.Tags).Distinct().OrderBy(t => t))
            AvailableTags.Add(tag);

        AvailableGroups.Clear();
        var present = list.Select(i => i.ResolvedAccess.Group).Where(g => g is not null).Select(g => g!.Value).ToHashSet();
        foreach (var g in Enum.GetValues<CatalogGroup>().Where(present.Contains))
            AvailableGroups.Add(g);

        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(HasGroups));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// Ridicat la orice schimbare de filtru — sectiunea isi reface lista.
    public event EventHandler? FiltersChanged;
}

/// Badge-ul de status, ca date pentru binding (WPF nu are echivalent direct
/// de View mica reutilizabila fara UserControl; asta tine textul+culoarea).
public static class AccessBadgeInfo
{
    public static string? Text(ResolvedAccess access) => access.KindKey switch
    {
        "access.kind.free" => "GRATUIT",
        "access.kind.paid" => "LICENȚĂ",
        "access.kind.trial" => "PROBĂ",
        "access.kind.external" => "EXTERN",
        _ => null
    };

    /// Culoare SEMANTICA (verde = gratuit, albastru = proba), separata de
    /// accentul aplicatiei — aceleasi valori ca badge-urile deja existente
    /// din DownloadResourceViewModel, ca sa nu apara doua verzuri diferite.
    public static Brush BadgeBrush(ResolvedAccess access) => access.KindKey switch
    {
        "access.kind.free" => Brushes.MediumSeaGreen,
        "access.kind.trial" => Brushes.DodgerBlue,
        "access.kind.external" => Brushes.SlateGray,
        _ => Brushes.DarkOrange
    };
}
