using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace GDCPluginManager.Client.Services;

/// Genereaza codul QR pentru pagina Android — port 1:1 al
/// `AndroidPane.qrImage(from:)` (Mac, CoreImage). Lipsea intentional pe
/// Windows (vezi comentariul vechi din MainWindow.xaml: "WPF nu are un
/// generator inclus si nu merita o dependinta noua") — QRCoder rezolva
/// asta cu o singura dependinta mica, MIT, pur managed (PngByteQRCode nu
/// foloseste System.Drawing.Common, deci nu are nevoie de niciun runtime
/// grafic suplimentar pe Windows).
public static class QrCodeImageGenerator
{
    /// nil daca `content` e gol — acelasi comportament ca varianta Mac
    /// (`qrImage(from:)` intoarce nil, panoul pur si simplu nu arata QR-ul).
    public static BitmapImage? Generate(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var pngQrCode = new PngByteQRCode(qrData);
        // pixelsPerModule 10 — acelasi factor de scalare ca pe Mac
        // (CGAffineTransform scaleX/Y: 10), ca modulele sa ramana cu
        // muchii drepte, nu neclare, la afisarea la 168x168.
        var pngBytes = pngQrCode.GetGraphic(pixelsPerModule: 10);

        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(pngBytes);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        if (bitmap.CanFreeze) bitmap.Freeze();
        return bitmap;
    }
}
