namespace GDCPluginManager.Core.Services;

/// Port 1:1 al `ProductActionState` (Mac, Core/State): starea de acțiune a unui produs, DERIVATĂ din
/// compatibilitate, licență, operația în curs și versiunea instalată. Aceeași ordine de prioritate ca pe Mac.
/// Stările cu progres (downloading/paused) sunt amânate și aici: InstallManager nu expune progres/reluare.
public abstract record ProductActionState
{
    public sealed record Incompatible : ProductActionState;
    public sealed record LicenseRequired : ProductActionState;
    public sealed record Installing : ProductActionState;
    public sealed record UpdateAvailable(string InstalledVersion, string Latest) : ProductActionState;
    public sealed record Installed(string Version) : ProductActionState;
    public sealed record NotInstalled : ProductActionState;
    public sealed record Failed(bool IsUpdate) : ProductActionState;
    public sealed record Offline : ProductActionState;

    public static ProductActionState Derive(bool isCompatible, bool isUnlocked, bool isBusy, string? installedVersion,
                                            string catalogVersion, bool lastInstallFailed = false, bool isOffline = false)
    {
        if (!isCompatible) return new Incompatible();
        if (!isUnlocked) return new LicenseRequired();
        if (isBusy) return new Installing();
        if (lastInstallFailed && installedVersion != catalogVersion) return new Failed(installedVersion is not null);
        if (installedVersion is null) return isOffline ? new Offline() : new NotInstalled();
        return installedVersion != catalogVersion ? new UpdateAvailable(installedVersion, catalogVersion) : new Installed(installedVersion);
    }
}
