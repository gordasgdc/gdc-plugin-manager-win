using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GDCPluginManager.Client.Converters;

/// Visible cand valoarea NU e null/goala — util pentru bannere de eroare/update
/// care apar doar cand exista un mesaj de afisat.
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// Inversul de mai sus - Visible cand valoarea E null/goala. Folosit pentru
/// simbolul generic de fallback (Aplicatiile Mele) cand extragerea iconitei
/// reale a esuat.
public sealed class NullToVisibilityInverseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;
        return hasValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
