#if DEBUG
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GDCPluginManager.Core.Models;

namespace GDCPluginManager.Client.Services;

/// DEBUG: campanie de test + grafică generată local (fără rețea), pentru capturile din laboratorul Windows.
internal static class PromoBannerFixtures
{
    public static (PromoBannerCampaign, PromoBannerBitmaps) Make(string raw)
    {
        var mode = raw switch { "imageText" => PromoBannerMode.ImageText, "image" => PromoBannerMode.Image, _ => PromoBannerMode.Text };
        var campaign = new PromoBannerCampaign
        {
            Id = "fixture-" + raw, Name = "Crăciun 2026", Mode = mode, ImagePath = mode == PromoBannerMode.Text ? null : "fixture.png",
            Texts = new() { ["ro"] = new PromoBannerText { Top = "CRĂCIUN", Main = "Pachete festive pentru coloriști — susține proiectul cu o donație" } },
        };
        var images = mode switch
        {
            PromoBannerMode.ImageText => new PromoBannerBitmaps { Light = Draw(1200, 400, false, false), Dark = Draw(1200, 400, true, false) },
            PromoBannerMode.Image => new PromoBannerBitmaps
            {
                Light = Draw(2400, 400, false, true), Dark = Draw(2400, 400, true, true),
                WideLight = Draw(4800, 400, false, true), WideDark = Draw(4800, 400, true, true),
            },
            _ => new PromoBannerBitmaps(),
        };
        return (campaign, images);
    }

    private static BitmapImage Draw(int w, int h, bool dark, bool withText)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var a = dark ? Color.FromRgb(0x29, 0x1A, 0x0D) : Color.FromRgb(0xFA, 0xEB, 0xD4);
            var b = dark ? Color.FromRgb(0x73, 0x45, 0x1A) : Color.FromRgb(0xE6, 0xBA, 0x80);
            dc.DrawRectangle(new LinearGradientBrush(a, b, 20), null, new Rect(0, 0, w, h));
            var dot = new SolidColorBrush(Color.FromArgb((byte)(dark ? 26 : 115), 255, 255, 255));
            for (var k = 0; k < w / 220; k++)
            {
                double r = 40 + (k * 37) % 70;
                dc.DrawEllipse(dot, null, new Point(k * 220 + 30 + r, h / 2.0 + (k * 53) % 90 - 45), r, r);
            }
            if (withText)
            {
                var ft = new FormattedText("CRĂCIUN · PACHETE FESTIVE", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal), h * 0.22,
                    new SolidColorBrush(dark ? Color.FromRgb(0xF2, 0xAD, 0x52) : Color.FromRgb(0x73, 0x40, 0x0D)), 1.0);
                dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
            }
        }
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);   // 96: desenul e în pixeli (la 192 ar ieși doar un sfert)
        bmp.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new System.IO.MemoryStream();
        enc.Save(ms);
        var img = new BitmapImage();
        img.BeginInit(); img.CacheOption = BitmapCacheOption.OnLoad; img.StreamSource = new System.IO.MemoryStream(ms.ToArray()); img.EndInit(); img.Freeze();
        return img;
    }
}
#endif
