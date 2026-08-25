using System.Text.Json;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Core.Services;

/// Optiunile JSON partajate de toate fetch-urile din Core, pentru ca fiecare
/// decode al catalogului sa foloseasca aceiasi converteri custom.
public static class CatalogJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new PluginTypeJsonConverter(),
            new SupportedOSJsonConverter(),
            new PluginItemJsonConverter(),
        },
    };
}
