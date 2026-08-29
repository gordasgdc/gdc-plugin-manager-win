using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GDCPluginManager.Client.Converters;

/// Randul de cautari recente (Etapa 1) e vizibil DOAR cand exista istoric
/// (values[0] = RecentSearches.Count > 0) SI campul de cautare e gol
/// (values[1] = IsSearching == false).
///
/// De ce ascuns in timpul cautarii: cand userul tasteaza, rezultatele live
/// sunt deja randate imediat sub bara — un rand de sugestii intre cele doua
/// ar concura vizual cu ele si ar impinge rezultatele in jos la fiecare
/// tasta. Pe Mac echivalentul e un dropdown care se inchide la fel.
///
/// MultiBinding (nu doua conditii in XAML) pentru ca WPF nu permite un
/// Binding ca ConverterParameter — acelasi motiv ca la
/// CategoryEqualsMultiConverter.
public sealed class RecentSearchesVisibleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var hasHistory = values.Length > 0 && values[0] is int count && count > 0;
        var isSearching = values.Length > 1 && values[1] is bool b && b;
        return hasHistory && !isSearching ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
