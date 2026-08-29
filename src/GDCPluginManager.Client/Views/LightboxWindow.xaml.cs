using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GDCPluginManager.Core.Services;

namespace GDCPluginManager.Client.Views;

/// Previewul marit al unei coperti, deschis la click pe imaginea din card.
/// Perechea de pe Mac e `ImageLightbox` din CoverImageViews.swift — zoom cu
/// rotita/dublu-click, pan cand e marita, Escape inchide.
public partial class LightboxWindow : Window
{
    private const double MinZoom = 1.0;
    private const double MaxZoom = 5.0;
    private const double ZoomStep = 1.15;

    // Pozitia de unde a inceput tragerea si offsetul de la acel moment.
    // Fara sa retinem offsetul "asezat", fiecare tragere noua ar sari
    // inapoi la 0 in loc sa continue de unde s-a oprit.
    private Point _dragStart;
    private double _committedX;
    private double _committedY;
    private bool _isDragging;

    private LightboxWindow()
    {
        InitializeComponent();
    }

    /// Deschide previewul pentru `url`, ca fereastra modala peste cea
    /// principala.
    ///
    /// NOTE: `Owner` face fereastra sa se centreze peste aplicatie si sa se
    /// inchida odata cu ea. Verificam ca fereastra principala chiar e
    /// incarcata — la pornire, `MainWindow` poate fi inca null, iar
    /// atribuirea ar arunca.
    public static void ShowFor(Uri url, string title)
    {
        var window = new LightboxWindow();
        window.TitleText.Text = title;

        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsLoaded && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.LoadImage(url);
        window.ShowDialog();
    }

    private static readonly HttpClient Http = HttpClientFactory.Create();

    /// Incarca imaginea de la URL.
    ///
    /// [2026-08-29] Acelasi fix ca in CoverViewModel.cs: `bitmap.UriSource =
    /// url` trece prin WinINet pe Windows, un stack de retea separat de
    /// `HttpClient` — daca WinINet e blocat/restrictionat la nivel de sistem,
    /// TOATE imaginile esueaza (inclusiv previewul marit), in timp ce
    /// catalogul (HttpClient) merge normal. Descarcam bytes-ii cu acelasi
    /// HttpClient dovedit functional, apoi construim BitmapImage dintr-un
    /// MemoryStream — WinINet nu mai e implicat deloc.
    private async void LoadImage(Uri url)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var bytes = await Http.GetByteArrayAsync(url);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = new MemoryStream(bytes);
                bitmap.EndInit();
                if (bitmap.CanFreeze) bitmap.Freeze();
                PreviewImage.Source = bitmap;
                DiagnosticLog.Write("LightboxWindow", $"OK, {bytes.Length} bytes (incercarea {attempt}): {url}");
                return;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write("LightboxWindow", $"Esuat la incercarea {attempt} pentru {url}: {ex.GetType().Name}: {ex.Message}");
                if (attempt == 1) await Task.Delay(800);
            }
        }
        ShowFailure();
    }

    private void ShowFailure()
    {
        PreviewImage.Visibility = Visibility.Collapsed;
        FailedText.Visibility = Visibility.Visible;
    }

    // ---- Zoom -----------------------------------------------------------

    private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        SetZoom(ImageScale.ScaleX * factor);
        e.Handled = true;
    }

    private void SetZoom(double value)
    {
        var zoom = Math.Clamp(value, MinZoom, MaxZoom);
        ImageScale.ScaleX = zoom;
        ImageScale.ScaleY = zoom;

        // La marime normala nu mai are sens niciun offset: imaginea incape
        // intreaga in cadru, deci o recentram si ascundem butonul de reset.
        if (Math.Abs(zoom - MinZoom) < 0.001)
        {
            ResetTranslation();
            ResetButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ResetButton.Visibility = Visibility.Visible;
        }
    }

    private void ResetTranslation()
    {
        ImageTranslate.X = 0;
        ImageTranslate.Y = 0;
        _committedX = 0;
        _committedY = 0;
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e) => SetZoom(MinZoom);

    // ---- Pan ------------------------------------------------------------

    private void Image_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Dublu-click comuta intre marime normala si 2x, ca pe Mac.
        if (e.ClickCount == 2)
        {
            SetZoom(ImageScale.ScaleX > MinZoom ? MinZoom : 2.0);
            return;
        }

        // Mutarea are sens doar cand imaginea e marita — la 1x nu e nimic
        // in afara cadrului de tras.
        if (ImageScale.ScaleX <= MinZoom) return;

        _isDragging = true;
        _dragStart = e.GetPosition(this);
        ((UIElement)sender).CaptureMouse();
    }

    private void Image_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var current = e.GetPosition(this);
        ImageTranslate.X = _committedX + (current.X - _dragStart.X);
        ImageTranslate.Y = _committedY + (current.Y - _dragStart.Y);
    }

    private void Image_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _committedX = ImageTranslate.X;
        _committedY = ImageTranslate.Y;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    // ---- Inchidere ------------------------------------------------------

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    /// Escape inchide fereastra — reflexul obisnuit pentru un preview.
    /// `OnKeyDown` pe fereastra prinde tasta indiferent ce element are
    /// focus inauntru (butonul de reset, imaginea, nimic).
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
