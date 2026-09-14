using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using Wpf.Ui.Controls;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al CommunityChannelCard din CommunityView.swift — un canal de
/// comunitate sau suport (2026-09-14).
///
/// DIFERENTA DE RANDARE FATA DE MAC, deliberata: Mac-ul deseneaza logo-urile
/// de brand din SVG-uri inline (SocialIconKind). WPF nu randeaza SVG nativ,
/// iar aducerea unui randator doar pentru cinci iconite n-ar merita. Aici
/// fiecare brand da CULOAREA pastilei, iar simbolul din interior vine din
/// `kind` — se recunoaste la fel de usor, fara nicio dependinta noua si fara
/// niciun fisier de resursa care poate lipsi dintr-un build.
public sealed partial class CommunityChannelViewModel : ObservableObject
{
    public CommunityChannel Channel { get; }

    public CommunityChannelViewModel(CommunityChannel channel) => Channel = channel;

    public string Title => Channel.Title;
    public string Description => Channel.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Channel.Description);
    public string Url => Channel.Url;

    /// Eticheta butonului, derivata din Kind — NU din JSON, exact ca pe Mac.
    /// Clientul Windows e deocamdata doar in romana (nu are sistemul L.t din
    /// client-ul Mac); cand va capata unul, doar textele de aici se schimba.
    public string ActionLabel => Channel.Kind switch
    {
        CommunityKind.Chat => "Deschide chat-ul",
        CommunityKind.Video => "Deschide canalul",
        CommunityKind.Docs => "Deschide ghidul",
        CommunityKind.Feedback => "Raporteaza o problema",
        _ => "Intra in grup",
    };

    /// Tipat, nu string: asa greselile de simbol sunt prinse de compilator,
    /// nu descoperite ca iconita lipsa la rulare, pe Windows, unde nu pot
    /// verifica direct de aici.
    public SymbolRegular IconSymbol => Channel.Kind switch
    {
        CommunityKind.Chat => SymbolRegular.ChatBubblesQuestion24,
        CommunityKind.Video => SymbolRegular.Play24,
        CommunityKind.Docs => SymbolRegular.Book24,
        CommunityKind.Feedback => SymbolRegular.Info24,
        _ => SymbolRegular.People24,
    };

    /// Culorile oficiale ale brandurilor, aceleasi cu cele din SVG-urile de
    /// pe Mac. O cheie necunoscuta da gri neutru, niciodata o eroare.
    public Brush IconBackground => new SolidColorBrush(Channel.Icon.Trim().ToLowerInvariant() switch
    {
        "facebook" or "fb" => Color.FromRgb(0x18, 0x77, 0xF2),
        "whatsapp" or "wa" => Color.FromRgb(0x25, 0xD3, 0x66),
        "youtube" or "yt" => Color.FromRgb(0xFF, 0x00, 0x00),
        "discord" => Color.FromRgb(0x58, 0x65, 0xF2),
        "telegram" => Color.FromRgb(0x2A, 0xAB, 0xEE),
        "instagram" or "ig" => Color.FromRgb(0xD6, 0x29, 0x76),
        "tiktok" => Color.FromRgb(0x01, 0x01, 0x01),
        "linkedin" => Color.FromRgb(0x0A, 0x66, 0xC2),
        "github" or "git" => Color.FromRgb(0x18, 0x17, 0x17),
        _ => Color.FromRgb(0x6E, 0x77, 0x81),
    });

    /// Deschide in browserul implicit al sistemului. `UseShellExecute = true`
    /// e obligatoriu pe .NET Core: fara el, Process.Start cu un URL arunca
    /// Win32Exception in loc sa deschida ceva.
    [RelayCommand]
    private void Open()
    {
        if (Channel.Destination is not { } uri) return;
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
