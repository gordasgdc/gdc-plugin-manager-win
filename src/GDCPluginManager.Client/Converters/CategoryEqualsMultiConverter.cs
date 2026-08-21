using System.Globalization;
using System.Windows.Data;
using GDCPluginManager.Client.ViewModels;

namespace GDCPluginManager.Client.Converters;

/// Compara SelectedCategory curent (values[0]) cu categoria acestui item
/// din lista (values[1]) — folosit pentru IsChecked pe RadioButton-urile
/// din bara laterala. ConverterParameter nu poate fi el insusi un Binding
/// in WPF, de-asta MultiBinding in loc de un singur converter cu parametru.
public sealed class CategoryEqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Al treilea binding (CurrentPage), optional: daca e prezent si nu
        // e "Catalog", niciun radiobutton de categorie nu trebuie bifat —
        // userul e pe pagina Cursuri/Aplicatii/Licenta.
        if (values.Length >= 3 && values[2] is SidebarPage page && page != SidebarPage.Catalog)
        {
            return false;
        }
        if (values is [CategoryFilter selected, CategoryFilter current, ..])
        {
            return selected == current;
        }
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
