using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GDCPluginManager.Client.Services;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.Views;

/// Bannerul unei campanii (Faza 6) — aceleași reguli ca PromoBannerView din Swift, construit nativ WPF:
/// Doar text = bandă 56; Imagine + text = panou 3:1 (UniformToFill, decupare centrală) + text separat;
/// Doar imagine = Uniform (integral vizibilă), 6:1 sau 12:1 de la 900 px logici, plafon 160.
public sealed class PromoBannerView : Border
{
    private readonly PromoBannerCampaign _campaign;
    private readonly PromoBannerBitmaps _images;
    private readonly Image? _imageOnly;

    public PromoBannerView(PromoBannerCampaign campaign, PromoBannerBitmaps images)
    {
        _campaign = campaign;
        _images = images;
        var dark = WindowsThemeManager.IsEffectiveDark;
        Background = new SolidColorBrush(dark ? Color.FromRgb(0x14, 0x14, 0x14) : Color.FromRgb(0xFC, 0xFC, 0xFC));
        BorderBrush = new SolidColorBrush(dark ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x22, 0, 0, 0));
        BorderThickness = new Thickness(0, 1, 0, 0);
        var text = campaign.TextFor("ro");   // clientul Windows e în română
        AutomationProperties.SetName(this, string.Join(". ", new[] { text?.Top, text?.Main }.Where(s => !string.IsNullOrEmpty(s))));

        switch (campaign.Mode)
        {
            case PromoBannerMode.Text:
                Height = PromoBannerSpec.TextBandHeight;
                Child = TextBlock(text, dark);
                break;
            case PromoBannerMode.ImageText:
                Height = PromoBannerSpec.SplitHeight;
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PromoBannerSpec.SplitHeight * PromoBannerSpec.SplitAspect) });
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var img = new Image { Source = Pick(images.Light, images.Dark, dark), Stretch = Stretch.UniformToFill, ClipToBounds = true };
                img.Focusable = false;   // decor: descrierea accesibilă e pe banner (Name)
                var holder = new Border { ClipToBounds = true, Child = img };
                grid.Children.Add(holder);
                var t = TextBlock(text, dark);
                Grid.SetColumn(t, 1);
                grid.Children.Add(t);
                Child = grid;
                break;
            default:
                _imageOnly = new Image { Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(_imageOnly, BitmapScalingMode.HighQuality);
                Child = _imageOnly;
                SizeChanged += (_, e) => { if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5) LayoutImageOnly(e.NewSize.Width); };
                Loaded += (_, _) => LayoutImageOnly(ActualWidth);
                Height = PromoBannerSpec.TextBandHeight;
                break;
        }

        if (Uri.TryCreate(campaign.LinkUrl, UriKind.Absolute, out var link) && link.Scheme == Uri.UriSchemeHttps)
        {
            Cursor = Cursors.Hand;
            MouseLeftButtonUp += (_, _) => Process.Start(new ProcessStartInfo(link.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private void LayoutImageOnly(double width)
    {
        if (_imageOnly is null || width <= 0) return;
        var dark = WindowsThemeManager.IsEffectiveDark;
        var hasWide = Pick(_images.WideLight, _images.WideDark, dark) is not null;
        var (useWide, height) = PromoBannerSpec.ImageOnlyLayout(width, hasWide);
        _imageOnly.Source = useWide ? Pick(_images.WideLight, _images.WideDark, dark) : Pick(_images.Light, _images.Dark, dark);
        Height = height;
    }

    private static BitmapSource? Pick(BitmapSource? light, BitmapSource? darkImage, bool dark) => dark ? darkImage ?? light : light;

    private static FrameworkElement TextBlock(PromoBannerText? text, bool dark)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(16, 0, 16, 0) };
        if (!string.IsNullOrEmpty(text?.Top))
            panel.Children.Add(new TextBlock
            {
                Text = text!.Top, FontSize = 11, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(dark ? Color.FromRgb(0xF2, 0xAD, 0x52) : Color.FromRgb(0x9A, 0x52, 0x00)),
            });
        if (!string.IsNullOrEmpty(text?.Main))
            panel.Children.Add(new TextBlock
            {
                Text = text!.Main, FontSize = 15, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap, MaxHeight = 42, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(dark ? Color.FromRgb(0xED, 0xEF, 0xF2) : Color.FromRgb(0x1D, 0x1D, 0x1F)),
            });
        return panel;
    }
}
