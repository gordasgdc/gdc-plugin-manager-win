using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace GDCPluginManager.Client.Converters;

/// Converteste un nume de simbol Fluent (string, ex. "DesktopMac24" din
/// `SupportedOSExtensions.BadgeSymbol()`) in enum-ul `SymbolRegular` cerut
/// de `ui:SymbolIcon.Symbol`. Necesar pentru ca modelul (Core, fara
/// dependinta pe Wpf.Ui) expune simboluri ca string, nu ca enum WPF.
[ValueConversion(typeof(string), typeof(SymbolRegular))]
public sealed class SymbolNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string name && Enum.TryParse<SymbolRegular>(name, out var symbol) ? symbol : SymbolRegular.Circle24;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
