using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GDCPluginManager.Client.Converters;

/// Bold pentru butonul Toate/Gratuite/Premium curent selectat — doar
/// cosmetic, filtrarea reala se face prin SetPriceFilterCommand
/// (MainViewModel), nu prin acest converter.
public sealed class PriceFilterWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? FontWeights.Bold : FontWeights.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
