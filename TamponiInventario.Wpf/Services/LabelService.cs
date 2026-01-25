using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using ZXing;
using ZXing.Common;

namespace TamponiInventario.Wpf.Services;

public sealed class LabelService
{
    private const double BarcodeModuleWidthMm = 0.30;
    private const double BarcodeModuleHeightMm = 9.0;
    private const double BarcodeQuietZoneMm = 6.0;
    private const double OutputDpi = 300.0;

    public string EnsureLabel(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU non valido.", nameof(sku));
        }

        var safeSku = SanitizeSku(sku.Trim());
        var labelsDir = Path.Combine(AppContext.BaseDirectory, "labels");
        Directory.CreateDirectory(labelsDir);

        var outputPath = Path.Combine(labelsDir, $"{safeSku}.png");
        if (!File.Exists(outputPath))
        {
            GenerateLabel(sku.Trim(), outputPath);
        }

        return outputPath;
    }

    public void OpenLabel(string sku)
    {
        var labelPath = EnsureLabel(sku);
        Process.Start(new ProcessStartInfo(labelPath) { UseShellExecute = true });
    }

    private static void GenerateLabel(string sku, string outputPath)
    {
        var moduleWidthPx = Math.Max(1, (int)Math.Round(BarcodeModuleWidthMm * OutputDpi / 25.4));
        var moduleHeightPx = Math.Max(1, (int)Math.Round(BarcodeModuleHeightMm * OutputDpi / 25.4));
        var quietZonePx = Math.Max(1, (int)Math.Round(BarcodeQuietZoneMm * OutputDpi / 25.4));

        var modulesCount = EstimateCode128Modules(sku);
        var barcodeWidthPx = Math.Max(1, modulesCount * moduleWidthPx);
        var totalWidthPx = barcodeWidthPx + (quietZonePx * 2);

        var writer = new BarcodeWriter
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Height = moduleHeightPx,
                Width = totalWidthPx,
                Margin = 0,
                PureBarcode = true
            }
        };

        using var bitmap = writer.Write(sku);
        bitmap.SetResolution((float)OutputDpi, (float)OutputDpi);
        bitmap.Save(outputPath, ImageFormat.Png);
    }

    private static int EstimateCode128Modules(string sku)
    {
        var length = sku.Length;
        return ((length + 2) * 11) + 13;
    }

    private static string SanitizeSku(string sku)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = sku;
        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }

        return sanitized;
    }
}
