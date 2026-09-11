using System.Windows;
using System.Windows.Controls;
using GDCPluginManager.Client.ViewModels;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.Views;

public partial class CatalogFilterBar : UserControl
{
    public static readonly DependencyProperty FiltersProperty =
        DependencyProperty.Register(nameof(Filters), typeof(CatalogFilterViewModel),
            typeof(CatalogFilterBar), new PropertyMetadata(null, OnFiltersChanged));

    public CatalogFilterViewModel? Filters
    {
        get => (CatalogFilterViewModel?)GetValue(FiltersProperty);
        set => SetValue(FiltersProperty, value);
    }

    public CatalogFilterBar() => InitializeComponent();

    private static void OnFiltersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CatalogFilterBar bar) bar.RebuildDynamicLists();
    }

    /// Grupurile si etichetele vin din continutul REAL al sectiunii, deci
    /// lista se reface de fiecare data cand ViewModel-ul se schimba.
    private void RebuildDynamicLists()
    {
        if (Filters is null) return;

        GroupBox.Items.Clear();
        GroupBox.Items.Add("Toate");
        foreach (var g in Filters.AvailableGroups) GroupBox.Items.Add(g.Label());
        GroupBox.SelectedIndex = 0;

        TagBox.Items.Clear();
        TagBox.Items.Add("Toate tipurile");
        foreach (var t in Filters.AvailableTags) TagBox.Items.Add(t);
        TagBox.SelectedIndex = 0;
    }

    private void OnPriceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Filters is null) return;
        Filters.Price = (AccessPriceFilter)PriceBox.SelectedIndex;
    }

    private void OnOSChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Filters is null) return;
        Filters.OS = (AccessOSFilter)OSBox.SelectedIndex;
    }

    private void OnGroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Filters is null || GroupBox.SelectedIndex < 0) return;
        // Indexul 0 e „Toate" — restul urmeaza ordinea din AvailableGroups.
        Filters.Group = GroupBox.SelectedIndex == 0
            ? null
            : Filters.AvailableGroups[GroupBox.SelectedIndex - 1];
    }

    private void OnTagChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Filters is null || TagBox.SelectedIndex < 0) return;
        Filters.Tag = TagBox.SelectedIndex == 0 ? "" : Filters.AvailableTags[TagBox.SelectedIndex - 1];
    }
}
