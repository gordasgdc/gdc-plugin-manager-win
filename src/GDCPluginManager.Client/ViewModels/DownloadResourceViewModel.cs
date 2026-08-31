using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Core.Services;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al DownloadResourceCard din ContentView.swift (Etapa 2,
/// 2026-08-29) — un card din grilele LUT-uri/Efecte Audio/Efecte Video/
/// Plugin-uri.
///
/// Modelat pe AudioTrackViewModel (descarcare directa, fara auto-instalare in
/// Resolve), dar cu LICENTIERE completa ca la ProductViewModel: badge
/// Gratuit/Proba/Licenta, buton WhatsApp cu ID masina cand e blocata, si
/// "Descarca" doar dupa deblocare. Reutilizeaza acelasi LicenseManager
/// (cheiat generic dupa ID de produs) — nicio infrastructura noua.
public sealed partial class DownloadResourceViewModel : ObservableObject
{
    public DownloadableResource Resource { get; }

    public DownloadResourceViewModel(DownloadableResource resource)
    {
        Resource = resource;
        // Ca la toate celelalte carduri: coperta se citeste din parametru,
        // nu din camp (campul e inca null la intrarea in constructor).
        Cover = new CoverViewModel(resource.CoverImageUrl, resource.Name);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => Resource.Scheduling?.CountdownText;

    public CoverViewModel Cover { get; }

    public string Name => Resource.Name;
    public string Description => Resource.Description;
    public string CategoryLabel => Resource.Category.Label();
    public string CategorySymbol => Resource.Category.Symbol();

    /// Badge GRATUIT (verde) / PROBA (albastru) / LICENTA (portocaliu) —
    /// identic cu ProductViewModel, ca sa citeasca la fel pe tot ecranul.
    public string BadgeText => Resource.IsFree ? (Resource.IsTrial ? "PROBĂ" : "GRATUIT") : "LICENȚĂ";
    public Brush BadgeBrush => Resource.IsFree
        ? (Resource.IsTrial ? Brushes.DodgerBlue : Brushes.MediumSeaGreen)
        : Brushes.DarkOrange;
    public bool ShowPrice => !Resource.IsFree;
    /// Suma AFISATA acum — cea promotionala cat timp promotia e activa
    /// (Etapa 4), altfel cea normala.
    public string PriceLabel => Resource.EffectivePriceDisplay;

    /// Vezi ProductViewModel: pe continut PROPRIU GDC badge-ul spune
    /// "Susținere promoțională", niciodata "reducere"/"discount".
    public bool IsPromoActive => Resource.IsPromoActive && !Resource.IsFree;
    public string OriginalPriceLabel => Resource.PriceDisplay;
    public string PromoBadgeText => "Susținere promoțională";

    public string OSBadgeSymbol => Resource.SupportedOS.BadgeSymbol();

    public bool HasYoutube => !string.IsNullOrWhiteSpace(Resource.YoutubeURL);
    public bool HasPurchase => !string.IsNullOrWhiteSpace(Resource.PurchaseURL);
    public bool HasDemo => !string.IsNullOrWhiteSpace(Resource.DemoURL);
    public bool HasFacebook => !string.IsNullOrWhiteSpace(Resource.SocialLinks?.FacebookURL);
    public bool HasSocialYoutube => !string.IsNullOrWhiteSpace(Resource.SocialLinks?.YoutubeURL);
    public bool HasInstagram => !string.IsNullOrWhiteSpace(Resource.SocialLinks?.InstagramURL);
    public bool HasTiktok => !string.IsNullOrWhiteSpace(Resource.SocialLinks?.TiktokURL);

    /// Deblocarea foloseste ACELASI store de licente ca produsele — un cod
    /// lipit in panoul Licenta valideaza si pentru resursele astea (vezi
    /// MainViewModel, unde ID-urile lor sunt adaugate la candidati).
    public bool IsUnlocked => LicenseManager.Shared.IsUnlocked(Resource);

    /// Recalculeaza starea derivata din LicenseManager — apelata de
    /// MainViewModel dupa orice activare/dezactivare din panoul Licenta.
    public void Refresh() => OnPropertyChanged(nameof(IsUnlocked));

    // ---- Unde l-ai salvat? (Etapa 5, 2026-08-29) --------------------------
    // Stare pur LOCALA, per resursa (vezi DownloadLocationStore) — nu are
    // nicio legatura cu catalogul. Randul apare doar pe resurse DEBLOCATE, ca
    // pe Mac.

    /// Folderul retinut, sau null daca userul n-a ales inca unul (sau daca
    /// folderul a fost sters intre timp — store-ul verifica existenta).
    public string? SavedFolder => DownloadLocationStore.Get(Resource.Id);
    public bool HasSavedFolder => SavedFolder is not null;

    /// Alege (sau schimba) folderul in care userul si-a salvat resursa.
    /// OpenFolderDialog e disponibil nativ din .NET 8 (WPF) — nu mai e nevoie
    /// de FolderBrowserDialog din WinForms, deci nu adaugam o referinta la
    /// Windows Forms doar pentru asta.
    [RelayCommand]
    private void ChooseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = $"Unde ai salvat „{Resource.Name}”?",
            Multiselect = false,
        };
        if (dialog.ShowDialog() != true) return;

        DownloadLocationStore.Set(Resource.Id, dialog.FolderName);
        RefreshFolder();
    }

    /// Deschide folderul in Explorer.
    [RelayCommand]
    private void OpenFolder()
    {
        if (SavedFolder is not { } folder) return;
        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch
        {
            // Folderul poate fi sters intre afisare si click — nu e fatal.
        }
    }

    [RelayCommand]
    private void ForgetFolder()
    {
        DownloadLocationStore.Clear(Resource.Id);
        RefreshFolder();
    }

    private void RefreshFolder()
    {
        OnPropertyChanged(nameof(SavedFolder));
        OnPropertyChanged(nameof(HasSavedFolder));
    }

    [RelayCommand]
    private void Download()
    {
        if (!IsUnlocked) return; // butonul e "Deblocheaza" in starea asta.
        Process.Start(new ProcessStartInfo(Resource.Url) { UseShellExecute = true });
    }

    /// Acelasi tipar WhatsApp ca ProductViewModel.Buy() — mesaj specific
    /// resursei (nume + suma) + ID calculator pre-completat.
    [RelayCommand]
    private void Buy()
    {
        var text = $"Salut! Vreau sa deblochez {Resource.Name} cu o donatie de {Resource.EffectivePriceDisplay}. ID calculator: {MachineID.Display}";
        var url = $"https://wa.me/34643109970?text={Uri.EscapeDataString(text)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenTutorial() => OpenIfPresent(Resource.YoutubeURL);

    [RelayCommand]
    private void OpenPurchase() => OpenIfPresent(Resource.PurchaseURL);

    [RelayCommand]
    private void OpenDemo() => OpenIfPresent(Resource.DemoURL);

    [RelayCommand]
    private void OpenFacebook() => OpenIfPresent(Resource.SocialLinks?.FacebookURL);

    [RelayCommand]
    private void OpenSocialYoutube() => OpenIfPresent(Resource.SocialLinks?.YoutubeURL);

    [RelayCommand]
    private void OpenInstagram() => OpenIfPresent(Resource.SocialLinks?.InstagramURL);

    [RelayCommand]
    private void OpenTiktok() => OpenIfPresent(Resource.SocialLinks?.TiktokURL);

    private static void OpenIfPresent(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
