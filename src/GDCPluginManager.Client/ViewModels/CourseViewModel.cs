using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// O optiune de pret pe un curs, cu butonul ei propriu de contact WhatsApp
/// (mesajul include si optiunea aleasa, nu doar numele cursului).
public sealed partial class CourseOptionViewModel : ObservableObject
{
    public CourseOption Option { get; }
    private readonly string _courseName;

    public CourseOptionViewModel(CourseOption option, string courseName)
    {
        Option = option;
        _courseName = courseName;
    }

    public string Label => Option.Label;
    public string PriceDisplay => Option.PriceDisplay;

    [RelayCommand]
    private void Contact()
    {
        // Acelasi format ca "courses.contact.message" din Localization.swift.
        var text = $"Salut! Vreau sa rezerv cursul {_courseName} — {Option.Label} ({Option.PriceDisplay}).";
        var url = $"https://wa.me/34643109970?text={Uri.EscapeDataString(text)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}

/// Port 1:1 al CourseCard din ContentView.swift — un curs bookabil, fara
/// fisiere/instalare/licenta, doar optiuni de pret + contact WhatsApp.
///
/// Convertit din clasa simpla la `ObservableObject` (2026-09-01) - avea
/// nevoie de notificare de schimbare pentru descrierea colapsabila noua
/// (`IsDescriptionExpanded`), care altfel n-ar reflecta toggle-ul in UI.
public sealed partial class CourseViewModel : ObservableObject
{
    public Course Course { get; }
    public IReadOnlyList<CourseOptionViewModel> Options { get; }

    public CourseViewModel(Course course)
    {
        Course = course;
        // Dupa atribuirea de mai sus: `Course` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp.
        Cover = new CoverViewModel(course.CoverImageUrl, course.Name);
        Options = course.Options.Select(o => new CourseOptionViewModel(o, course.Name)).ToList();
    }

    /// Badge "Mai sunt Xz Yh" pentru continut cu valabilitate temporala
    /// si countdown activat de Furnizor - vezi Scheduling.CountdownText.
    public string? CountdownText => Course.Scheduling?.CountdownText;

    public string Name => Course.Name;

    /// Coperta cardului + actiunea de marire. Vezi CoverViewModel:
    /// o singura implementare, folosita de toate cele cinci tipuri de card.
    /// Se creeaza o data, in constructor, nu la fiecare acces — altfel WPF
    /// ar primi un obiect nou la fiecare redesenare si ar reincarca imaginea.
    public CoverViewModel Cover { get; }
    public string Description => Course.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    [ObservableProperty]
    private bool _isDescriptionExpanded;

    [RelayCommand]
    private void ToggleDescription() => IsDescriptionExpanded = !IsDescriptionExpanded;

    // Model de acces + detalii desfasurare/valabilitate — Etapa 2026-09-03.
    // Port 1:1 al badge-ului din CourseCard (ContentView.swift, Mac).
    public string AccessTypeLabel => Course.EffectiveAccessType.Label();

    /// Aceleasi culori ca pe Mac (verde/albastru/mov/portocaliu) — vezi
    /// BadgeBrush din DownloadResourceViewModel/ProductViewModel pentru
    /// tiparul deja stabilit (Brushes.* direct, fara resursa noua).
    public Brush AccessTypeBrush => Course.EffectiveAccessType switch
    {
        CourseAccessType.Free => Brushes.MediumSeaGreen,
        CourseAccessType.Subscription => Brushes.MediumPurple,
        CourseAccessType.LiveMentoring => Brushes.DarkOrange,
        _ => Brushes.DodgerBlue,
    };

    public string? FormatLabel => Course.FormatLabel;
    public bool HasFormatLabel => !string.IsNullOrWhiteSpace(FormatLabel);
    public string ValidityLabel => Course.Validity?.Label ?? "Acces pe viață";
    public bool HasAccessLink => !string.IsNullOrWhiteSpace(Course.AccessLink);

    [RelayCommand]
    private void OpenAccessLink()
    {
        if (string.IsNullOrWhiteSpace(Course.AccessLink)) return;
        Process.Start(new ProcessStartInfo(Course.AccessLink) { UseShellExecute = true });
    }
}
