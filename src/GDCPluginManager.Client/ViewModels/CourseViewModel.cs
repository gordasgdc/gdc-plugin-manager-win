using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;

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
public sealed class CourseViewModel
{
    public Course Course { get; }
    public IReadOnlyList<CourseOptionViewModel> Options { get; }

    public CourseViewModel(Course course)
    {
        Course = course;
        Options = course.Options.Select(o => new CourseOptionViewModel(o, course.Name)).ToList();
    }

    public string Name => Course.Name;
    public string Description => Course.Description;
}
