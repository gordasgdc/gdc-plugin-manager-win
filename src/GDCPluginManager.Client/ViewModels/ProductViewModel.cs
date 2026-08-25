using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Un card din grid-ul de produse — imbraca un PluginItem cu starea lui
/// live (instalat/licentiat/ocupat), citita din InstallManager/LicenseManager
/// la fiecare afisare in loc de a fi cache-uita local, ca UI-ul sa ramana
/// mereu adevarat dupa install/remove/activate.
///
/// Fluxul Cumpara/Instaleaza e port 1:1 al PluginCard din ContentView.swift:
/// un item neblocat NU are buton de "activeaza licenta" pe card — are buton
/// "Cumpara", care deschide direct WhatsApp cu mesaj specific produsului
/// (nume + pret). Introducerea unui cod deja cumparat se face separat, din
/// panoul "Licenta" al sidebarului (LicensePaneViewModel), nu de aici.
public sealed partial class ProductViewModel : ObservableObject
{
    public PluginItem Item { get; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    /// Esec de instalare pentru o resursa PLATITA (vezi
    /// PaidResourceInstallException / InstallManager.cs) — arata butonul de
    /// contact WhatsApp in loc de fisiere/instructiuni de instalare manuala.
    [ObservableProperty]
    private bool _showPaidResourceSupportError;

    public ProductViewModel(PluginItem item)
    {
        Item = item;
        // Dupa atribuirea de mai sus: `Item` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp.
        Cover = new CoverViewModel(item.CoverImageUrl, item.Name);
    }

    public string Name => Item.Name;

    /// Coperta cardului + actiunea de marire. Vezi CoverViewModel:
    /// o singura implementare, folosita de toate cele cinci tipuri de card.
    /// Se creeaza o data, in constructor, nu la fiecare acces — altfel WPF
    /// ar primi un obiect nou la fiecare redesenare si ar reincarca imaginea.
    public CoverViewModel Cover { get; }
    public string Description => Item.Description;
    public string TypeLabel => Item.Type.Label();
    public string VersionLabel => $"v{Item.Version}";
    public string PriceLabel => Item.IsFree ? (Item.IsTrial ? "Proba" : "Gratuit") : Item.PriceDisplay;

    /// Badge GRATUIT (verde) / PROBĂ (albastru) / LICENȚĂ (portocaliu) —
    /// cerut explicit 2026-08-24, ca sa nu creeze impresia de "reclama
    /// agresiva": eticheta e clara si scurta, mesajul complet de
    /// incredere apare doar la hover (vezi BadgeTooltip). Port 1:1 al
    /// BadgePill din ContentView.swift (Mac).
    public string BadgeText => Item.IsFree ? (Item.IsTrial ? "PROBĂ" : "GRATUIT") : "LICENȚĂ";
    public Brush BadgeBrush => Item.IsFree
        ? (Item.IsTrial ? Brushes.DodgerBlue : Brushes.MediumSeaGreen)
        : Brushes.DarkOrange;
    public string? BadgeTooltip => Item.IsFree
        ? null
        : "Dezvoltat și susținut de comunitate. Licență Lifetime la preț promoțional de lansare.";
    /// Pretul numeric se arata separat de badge DOAR pentru produsele
    /// platite — la cele gratuite badge-ul "GRATUIT"/"PROBĂ" e suficient,
    /// un pret de "0,00 €" alaturi ar fi confuz.
    public bool ShowPrice => !Item.IsFree;
    public bool HasYoutube => !string.IsNullOrWhiteSpace(Item.YoutubeURL);

    /// Vezi cerinta "Selector Compatibilitate OS": badge emoji pe card,
    /// vizibil pentru toate cele 3 stari, inclusiv CrossPlatform (2026-08-25:
    /// "Ambele" trebuie sa se vada explicit pe card, nu doar sa fie absenta
    /// unui badge - decizia initiala de a-l ascunde a fost o presupunere
    /// gresita despre asteptarile UX, corectata la cererea explicita).
    public string OSBadgeEmoji => Item.SupportedOS.BadgeEmoji();
    public bool IsCompatible => Item.SupportedOS.Allows(SupportedOSExtensions.Current);

    public bool IsInstalled => InstallManager.Shared.IsInstalled(Item);
    public bool HasUpdate => InstallManager.Shared.HasUpdate(Item);
    public bool IsUnlocked => LicenseManager.Shared.IsUnlocked(Item);

    /// Recalculeaza toate proprietatile derivate din InstallManager/LicenseManager —
    /// apelat de MainViewModel dupa orice actiune care le poate schimba
    /// (inclusiv dupa activarea unei licente din panoul separat).
    public void Refresh()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(IsUnlocked));
    }

    [RelayCommand]
    private void Buy()
    {
        // Acelasi format ca buyURL din PluginCard (ContentView.swift) —
        // mesaj specific produsului, nu generic ca cel din panoul Licenta.
        var text = $"Salut! Vreau sa deblochez {Item.Name} cu o donatie de {Item.PriceDisplay}. ID calculator: {MachineID.Display}";
        var url = $"https://wa.me/34643109970?text={Uri.EscapeDataString(text)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ContactSupport()
    {
        var text = $"Salut! A aparut o eroare la instalarea {Item.Name} — ma poti ajuta sa o instalez manual? ID calculator: {MachineID.Display}";
        var url = $"https://wa.me/34643109970?text={Uri.EscapeDataString(text)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenTutorial()
    {
        if (!string.IsNullOrWhiteSpace(Item.YoutubeURL))
        {
            Process.Start(new ProcessStartInfo(Item.YoutubeURL) { UseShellExecute = true });
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (!IsCompatible) return; // butonul e dezactivat/ascuns in starea asta (vezi IsCompatible in XAML).
        if (!IsUnlocked) return; // butonul e "Cumpara" in starea asta, InstallCommand nu ar trebui apelat.

        IsBusy = true;
        StatusMessage = null;
        ShowPaidResourceSupportError = false;
        try
        {
            var outcome = await InstallManager.Shared.InstallAsync(Item);
            StatusMessage = outcome.Kind switch
            {
                InstallOutcomeKind.Installed => "Instalat.",
                InstallOutcomeKind.InstalledToGallery => $"Adaugat automat in Gallery, albumul „{outcome.AlbumName}”.",
                InstallOutcomeKind.InstalledNeedsManualStep =>
                    $"Fisierele sunt verificate in {outcome.StagingFolder} — deschide Gallery-ul din Resolve si importa-le manual (album nou, PowerGrade -> Import).",
                _ => null,
            };
        }
        catch (PaidResourceInstallException ex)
        {
            // Mesaj generic, fara cale de fisier/instructiuni — vezi
            // InstallManager.cs. Butonul de contact apare separat (XAML,
            // legat de ShowPaidResourceSupportError).
            StatusMessage = ex.Message;
            ShowPaidResourceSupportError = true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }

    [RelayCommand]
    private void Remove()
    {
        IsBusy = true;
        StatusMessage = null;
        ShowPaidResourceSupportError = false;
        try
        {
            var outcome = InstallManager.Shared.Remove(Item);
            StatusMessage = outcome == RemoveOutcome.RemovedNeedsManualGalleryCleanup
                ? "Fisierele locale au fost sterse — elimina-le si din Gallery manual (Resolve inchis sau scripting indisponibil)."
                : "Eliminat.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }
}
