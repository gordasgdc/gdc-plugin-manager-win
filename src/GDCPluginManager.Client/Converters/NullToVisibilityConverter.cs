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
