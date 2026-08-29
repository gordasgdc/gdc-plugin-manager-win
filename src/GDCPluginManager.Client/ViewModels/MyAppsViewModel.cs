using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GDCPluginManager.Client.Services;
using GDCPluginManager.Core.Services;
using Microsoft.Win32;

namespace GDCPluginManager.Client.ViewModels;

/// Un card din "Aplicatiile Mele" — o aplicatie GDC gasita instalata.
[SupportedOSPlatform("windows")]
public sealed partial class InstalledAppViewModel : ObservableObject
{
    public InstalledGdcApp Installed { get; }

    [ObservableProperty]
    private string? _latestVersion;

    public InstalledAppViewModel(InstalledGdcApp installed) => Installed = installed;

    public string Name => Installed.App.Name;
    public string InstalledVersionDisplay => $"v{Installed.InstalledVersion}";

    /// Badge "ACTUALIZARE" doar cand chiar stim ca versiunea publicata e mai
    /// noua. Daca verificarea a esuat (LatestVersion == null) NU aratam nimic
    /// — mai bine tacere decat un badge fals pe o informatie pur optionala.
    public bool HasUpdate =>
        !string.IsNullOrWhiteSpace(LatestVersion)
        && VersionCompare.IsNewer(LatestVersion!, Installed.InstalledVersion);

    public string UpdateTooltip => $"Versiune disponibila: v{LatestVersion}";

    partial void OnLatestVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(UpdateTooltip));
    }

    [RelayCommand]
    private void Launch()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Installed.ExecutablePath) { UseShellExecute = true });
        }
        catch
        {
            // Executabilul poate fi sters intre detectare si lansare — nu e
            // fatal pentru restul paginii.
        }
    }

    /// Deschide pagina aplicatiei de pe gordas.dev (de unde se descarca
    /// versiunea noua). NU deschide GitHub direct — vezi Regula 20.
    [RelayCommand]
    private void OpenSite() =>
        Process.Start(new ProcessStartInfo(Installed.App.SiteUrl) { UseShellExecute = true });
}

/// O scurtatura personalizata (orice .exe ales de user).
[SupportedOSPlatform("windows")]
public sealed partial class CustomLauncherViewModel : ObservableObject
{
    public CustomLauncher Launcher { get; }
    private readonly Action<CustomLauncherViewModel> _remove;

    public CustomLauncherViewModel(CustomLauncher launcher, Action<CustomLauncherViewModel> remove)
    {
        Launcher = launcher;
        _remove = remove;
    }

    public string Name => Launcher.Name;
    public string Path => Launcher.Path;

    [RelayCommand]
    private void Launch()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Launcher.Path) { UseShellExecute = true });
        }
        catch
        {
            // Vezi InstalledAppViewModel.Launch.
        }
    }

    [RelayCommand]
    private void Remove() => _remove(this);
}

/// Port al `MyAppsLauncher.swift` (Etapa 3) — pagina "Aplicatiile Mele".
///
/// NOTA DE ARHITECTURA, portata 1:1 de pe Mac: "detinerea" unei aplicatii GDC
/// NU se determina prin licenta (fiecare aplicatie GDC isi tine activarea
/// separat, local — GDCPluginManager n-are acces la ea), ci prin PREZENTA
/// aplicatiei instalate. Aproximare corecta in practica, exact ca pe Mac.
[SupportedOSPlatform("windows")]
public sealed partial class MyAppsViewModel : ObservableObject
{
    public ObservableCollection<InstalledAppViewModel> InstalledApps { get; } = [];
    public ObservableCollection<CustomLauncherViewModel> CustomLaunchers { get; } = [];

    [ObservableProperty]
    private bool _isChecking;

    public bool HasNoApps => InstalledApps.Count == 0;

    private readonly HttpClient _http = HttpClientFactory.Create();

    public MyAppsViewModel()
    {
        ReloadCustomLaunchers();
    }

    /// Redetecteaza aplicatiile instalate (Registry, sincron si local), apoi
    /// verifica versiunile publicate in fundal. Cele doua sunt separate
    /// deliberat: lista apare instant, badge-urile "ACTUALIZARE" apar cand/daca
    /// raspunde reteaua.
    [RelayCommand]
    public async Task RefreshAsync()
    {
        InstalledApps.Clear();
        foreach (var installed in MyAppsService.DetectInstalled())
        {
            InstalledApps.Add(new InstalledAppViewModel(installed));
        }
        OnPropertyChanged(nameof(HasNoApps));

        IsChecking = true;
        try
        {
            foreach (var vm in InstalledApps)
            {
                vm.LatestVersion = await MyAppsService.FetchLatestVersionAsync(vm.Installed.App, _http);
            }
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// Adauga o scurtatura personalizata — orice .exe de pe disc (DaVinci
    /// Resolve, Premiere, Photoshop...). Perechea de pe Mac e `fileImporter`.
    [RelayCommand]
    private void AddCustomLauncher()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Alege o aplicatie (.exe)",
            Filter = "Aplicatii (*.exe)|*.exe",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() != true) return;

        var path = dialog.FileName;
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (CustomLaunchers.Any(l => string.Equals(l.Path, path, StringComparison.OrdinalIgnoreCase))) return;

        CustomLaunchers.Add(new CustomLauncherViewModel(new CustomLauncher(name, path), RemoveCustomLauncher));
        PersistCustomLaunchers();
    }

    private void RemoveCustomLauncher(CustomLauncherViewModel vm)
    {
        CustomLaunchers.Remove(vm);
        PersistCustomLaunchers();
    }

    private void PersistCustomLaunchers() =>
        CustomLauncherStore.Save(CustomLaunchers.Select(l => l.Launcher));

    private void ReloadCustomLaunchers()
    {
        CustomLaunchers.Clear();
        foreach (var launcher in CustomLauncherStore.Load())
        {
            CustomLaunchers.Add(new CustomLauncherViewModel(launcher, RemoveCustomLauncher));
        }
    }
}
