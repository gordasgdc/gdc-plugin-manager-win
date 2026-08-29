using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GDCPluginManager.Client.Converters;

/// Visible cand value.ToString() == parameter (string) — folosit pentru
/// a arata/ascunde o sectiune de continut in functie de MainViewModel.CurrentPage
/// (un enum), fara sa fie nevoie de un converter dedicat per pagina.
public sealed class EnumEqualsVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// La fel, dar returneaza bool in loc de Visibility — pentru IsChecked pe
/// butoanele de navigare din sidebar (Cursuri/Aplicatii/Licenta).
public sealed class EnumEqualsBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// Visible cand bool-ul e FALSE — pentru butonul "Cumpara" de pe un card
/// (vizibil doar cand IsUnlocked e false), fara sa mai fie nevoie de o
/// proprietate separata "IsLocked" in ProductViewModel.
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// Visible cand int-ul e 0 — pentru mesajul "gol" al unei liste (Cursuri/Aplicatii).
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// Inversul lui ZeroToVisibilityConverter: Visible doar cand int-ul e > 0.
/// Folosit de sectiunile din rezultatele cautarii globale (Etapa 1) — o
/// sectiune fara nicio potrivire nu se randa DELOC (nici titlu, nici mesaj
/// "gol"), exact ca `GlobalSearchResults` de pe Mac.
public sealed class NonZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
