using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Core.Models;
using GDCPluginManager.Client.Services;

namespace GDCPluginManager.Client.ViewModels;

/// Port 1:1 al TutorialCard din ContentView.swift — video YouTube embedded,
/// cu thumbnail, titlu, descriere colapsabilă și taguri.
public sealed partial class TutorialViewModel : ObservableObject
{
    public Tutorial Tutorial { get; }

    public TutorialViewModel(Tutorial tutorial)
    {
        Tutorial = tutorial;
        Cover = new CoverViewModel(tutorial.ThumbnailUri, tutorial.Title);
        CountdownRefreshTimer.Tick += () => OnPropertyChanged(nameof(CountdownText));
    }

    public string? CountdownText => Tutorial.Scheduling?.CountdownText;
    public string Title => Tutorial.Title;
    public string Description => Tutorial.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Tutorial.Description);
    public string Category => Tutorial.Category;
    public IReadOnlyList<string> Tags => Tutorial.Tags;
    public bool HasTags => Tutorial.Tags.Count > 0;
    public CoverViewModel Cover { get; }

    [ObservableProperty]
    private bool _isDescriptionExpanded;

    [ObservableProperty]
    private bool _isTagsExpanded;

    public string TagsToggleLabel => $"Taguri ({Tutorial.Tags.Count})";

    [RelayCommand]
    private void ToggleDescription() => IsDescriptionExpanded = !IsDescriptionExpanded;

    [RelayCommand]
    private void ToggleTags() => IsTagsExpanded = !IsTagsExpanded;

    [RelayCommand]
    private void Watch() => Process.Start(new ProcessStartInfo(Tutorial.YoutubeURL) { UseShellExecute = true });
}
