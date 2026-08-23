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
        // Dupa atribuirea de mai sus: `Course` e inca null la intrarea in
        // constructor, deci coperta se citeste din parametru, nu din camp.
        Cover = new CoverViewModel(course.CoverImageUrl, course.Name);
        Options = course.Options.Select(o => new CourseOptionViewModel(o, course.Name)).ToList();
    }

    public string Name => Course.Name;

    /// Coperta cardului + actiunea de marire. Vezi CoverViewModel:
    /// o singura implementare, folosita de toate cele cinci tipuri de card.
    /// Se creeaza o data, in constructor, nu la fiecare acces — altfel WPF
    /// ar primi un obiect nou la fiecare redesenare si ar reincarca imaginea.
    public CoverViewModel Cover { get; }
    public string Description => Course.Description;
}
