using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GDCPluginManager.Client.Converters;

/// PITFALL FIXED 2026-08-24 (bug critic pre-release: coperile nu apareau
/// deloc pe Windows, pe orice masina): `Image.Source="{Binding Cover.Url}"`
/// (unde `Cover.Url` e de tip `Uri`) NU functioneaza fara acest converter —
/// `ImageSourceConverter` (folosit implicit de WPF cand tipul sursei nu se
/// potriveste cu `ImageSource`) stie sa converteasca doar dintr-un
/// `string`, NU dintr-un `System.Uri`. Fara conversie explicita, binding-ul
/// esueaza silentios (eroare doar in Output/debug console, invizibila la
/// `dotnet build` sau fara debugger atasat) — `Image.Source` ramane null,
/// deci NICIO coperta nu se vede pe NICIO categorie, pe NICIO masina
/// Windows, indiferent de catalog/cache/versiune. Perechea de pe Mac
/// (`AsyncImage(url:)`, SwiftUI) nu are aceasta problema — acolo tipul
/// `URL` e gestionat nativ de `AsyncImage`, nu printr-un `TypeConverter`.
///
/// `BitmapCacheOption.OnLoad`: bitmap-ul se descarca si decodeaza complet
/// la creare, apoi conexiunea/handle-ul se elibereaza — evita sa tinem
/// conexiuni HTTP deschise pentru fiecare card vizibil simultan.
public sealed class UriToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Uri uri) return null;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = uri;
        bitmap.EndInit();
        if (bitmap.CanFreeze) bitmap.Freeze();
        return bitmap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
