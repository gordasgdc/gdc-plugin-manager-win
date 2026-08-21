using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.ViewModels;

/// O licenta detinuta, afisata in "Licentele mele" — nume produs + buton
/// de dezactivare.
public sealed partial class OwnedLicenseViewModel : ObservableObject
{
    public string ProductId { get; }
    public string ProductName { get; }
    private readonly Action<string> _deactivate;

    public OwnedLicenseViewModel(string productId, string productName, Action<string> deactivate)
    {
        ProductId = productId;
        ProductName = productName;
        _deactivate = deactivate;
    }

    [RelayCommand]
    private void Deactivate() => _deactivate(ProductId);
}

/// Port 1:1 al LicensePane.swift — destinatia "Licenta" din sidebar. Aici
/// (nu pe fiecare card) se introduce un cod deja cumparat — codul se
/// valideaza fata de TOATE produsele din catalog (vezi LicenseManager.Activate),
/// nu fata de un produs anume. Fiecare card are propriul buton "Cumpara"
/// (vezi ProductViewModel.BuyCommand); acesta e punctul de contact general,
/// generic, plus locul unde se vede ce ai deja deblocat.
public sealed partial class LicensePaneViewModel : ObservableObject
{
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private bool _justActivated;

    public ObservableCollection<OwnedLicenseViewModel> OwnedLicenses { get; } = [];

    public string MachineIdDisplay => MachineID.Display;

    public bool IsLicensed => LicenseManager.Shared.IsLicensed;
    public int LicensedCount => LicenseManager.Shared.LicensedProducts.Count;
    public string? ActivationError => LicenseManager.Shared.ActivationError;

    private readonly Func<IReadOnlyList<string>> _allProductIds;
    private readonly Func<string, string> _productName;

    public LicensePaneViewModel(Func<IReadOnlyList<string>> allProductIds, Func<string, string> productName)
    {
        _allProductIds = allProductIds;
        _productName = productName;
        RebuildOwnedLicenses();
    }

    public void RebuildOwnedLicenses()
    {
        OwnedLicenses.Clear();
        foreach (var productId in LicenseManager.Shared.LicensedProducts.Keys.OrderBy(id => id))
        {
            OwnedLicenses.Add(new OwnedLicenseViewModel(productId, _productName(productId), DeactivateProduct));
        }
        OnPropertyChanged(nameof(IsLicensed));
        OnPropertyChanged(nameof(LicensedCount));
    }

    private void DeactivateProduct(string productId)
    {
        LicenseManager.Shared.Deactivate(productId);
        RebuildOwnedLicenses();
    }

    [RelayCommand(CanExecute = nameof(CanActivate))]
    private void Activate()
    {
        JustActivated = LicenseManager.Shared.Activate(Code, _allProductIds());
        OnPropertyChanged(nameof(ActivationError));
        if (JustActivated)
        {
            Code = string.Empty;
            RebuildOwnedLicenses();
        }
    }

    private bool CanActivate() => !string.IsNullOrWhiteSpace(Code) && _allProductIds().Count > 0;

    partial void OnCodeChanged(string value) => ActivateCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void CopyMachineId() => Clipboard.SetText(MachineIdDisplay);

    [RelayCommand]
    private void OpenWhatsApp()
    {
        var text = $"Salut! Vreau sa deblochez un produs GDC Plugin Manager printr-o donatie. ID calculator: {MachineIdDisplay}";
        var url = $"https://wa.me/34643109970?text={Uri.EscapeDataString(text)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
