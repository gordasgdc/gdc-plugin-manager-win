using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al PartnerOfferCard din ContentView.swift (Etapa 4, 2026-08-29) —
/// o ofertă de la un brand PARTENER.
///
/// SINGURUL card din tot clientul unde limbajul de "discount"/procent e
/// permis: e o relatie comerciala cu un tert, nu continut propriu GDC
/// (Regula 3, Partea 1, acopera doar produsele/resursele proprii — acolo
/// suma ramane strict "sustinere", niciodata "reducere").
public sealed partial class PartnerOfferViewModel : ObservableObject
{
    public PartnerOffer Offer { get; }

    public PartnerOfferViewModel(PartnerOffer offer)
    {
        Offer = offer;
        Cover = new CoverViewModel(offer.CoverImageUrl, offer.BrandName);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => Offer.Scheduling?.CountdownText;

    public CoverViewModel Cover { get; }

    public string BrandName => Offer.BrandName;
    public string Description => Offer.Description;

    /// Badge rosu de discount — afisat DOAR daca furnizorul a completat textul.
    /// Text liber (nu procent numeric), ca sa acopere si "2 la pret de 1".
    public string? DiscountText => Offer.DiscountText;
    public bool HasDiscount => !string.IsNullOrWhiteSpace(Offer.DiscountText);

    public string? CouponCode => Offer.CouponCode;
    public bool HasCoupon => !string.IsNullOrWhiteSpace(Offer.CouponCode);

    public bool HasYoutube => !string.IsNullOrWhiteSpace(Offer.YoutubeURL);
    public bool HasFacebook => !string.IsNullOrWhiteSpace(Offer.SocialLinks?.FacebookURL);
    public bool HasSocialYoutube => !string.IsNullOrWhiteSpace(Offer.SocialLinks?.YoutubeURL);
    public bool HasInstagram => !string.IsNullOrWhiteSpace(Offer.SocialLinks?.InstagramURL);
    public bool HasTiktok => !string.IsNullOrWhiteSpace(Offer.SocialLinks?.TiktokURL);

    [RelayCommand]
    private void Open() => OpenIfPresent(Offer.Url);

    /// Copiaza codul de cupon — ca sa poata fi lipit direct in cosul
    /// magazinului partener, fara sa fie retinut manual.
    [RelayCommand]
    private void CopyCoupon()
    {
        if (string.IsNullOrWhiteSpace(Offer.CouponCode)) return;
        try { Clipboard.SetText(Offer.CouponCode); }
        catch { /* clipboard-ul poate fi blocat de alt proces — nu e fatal */ }
    }

    [RelayCommand]
    private void OpenTutorial() => OpenIfPresent(Offer.YoutubeURL);

    [RelayCommand]
    private void OpenFacebook() => OpenIfPresent(Offer.SocialLinks?.FacebookURL);

    [RelayCommand]
    private void OpenSocialYoutube() => OpenIfPresent(Offer.SocialLinks?.YoutubeURL);

    [RelayCommand]
    private void OpenInstagram() => OpenIfPresent(Offer.SocialLinks?.InstagramURL);

    [RelayCommand]
    private void OpenTiktok() => OpenIfPresent(Offer.SocialLinks?.TiktokURL);

    private static void OpenIfPresent(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
