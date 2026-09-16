using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using WinBitmapDecoder = Windows.Graphics.Imaging.BitmapDecoder;
using WinBitmapFrame   = Windows.Graphics.Imaging.BitmapFrame;
using WinSoftwareBitmap = Windows.Graphics.Imaging.SoftwareBitmap;
using WinBitmapPixelFormat = Windows.Graphics.Imaging.BitmapPixelFormat;
using WinBitmapAlphaMode   = Windows.Graphics.Imaging.BitmapAlphaMode;

namespace QuizHelper.Core;

/// <summary>
/// Wraps Windows.Media.Ocr to extract text from clipboard images (screenshots).
/// Uses the best available installed OCR language (es-ES preferred, en-US fallback).
/// All processing is local/offline — no network calls, no token consumption.
/// </summary>
public static class OcrHelper
{
    private static Windows.Media.Ocr.OcrEngine? _engine;

    private static Windows.Media.Ocr.OcrEngine? GetEngine()
    {
        if (_engine != null) return _engine;

        var langs = Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages;
        var lang  = langs.FirstOrDefault(l => l.LanguageTag.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                 ?? langs.FirstOrDefault()
                 ?? new Windows.Globalization.Language("en-US");

        try { _engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(lang); }
        catch { _engine = null; }
        return _engine;
    }

    /// <summary>
    /// Recognises text in the given WPF BitmapSource (from clipboard).
    /// Returns the full concatenated OCR text, or null if recognition is unavailable.
    /// </summary>
    public static async Task<string?> RecognizeTextAsync(BitmapSource bitmapSource)
    {
        var engine = GetEngine();
        if (engine == null) return null;

        try
        {
            // Convert WPF BitmapSource → PNG bytes → Windows SoftwareBitmap
            byte[] pngBytes;
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
                encoder.Save(ms);
                pngBytes = ms.ToArray();
            }

            WinSoftwareBitmap softBmp;
            using (var inMs = new MemoryStream(pngBytes))
            {
                var raStream = inMs.AsRandomAccessStream();
                var decoder  = await WinBitmapDecoder.CreateAsync(raStream);
                softBmp = await decoder.GetSoftwareBitmapAsync(
                    WinBitmapPixelFormat.Bgra8, WinBitmapAlphaMode.Premultiplied);
            }

            var ocrResult = await engine.RecognizeAsync(softBmp);
            if (ocrResult == null) return null;

            var sb = new System.Text.StringBuilder();
            foreach (var line in ocrResult.Lines)
                sb.AppendLine(line.Text);
            return sb.ToString().Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns a SHA-256 hash of the bitmap pixels for deduplication.</summary>
    public static string HashBitmap(BitmapSource bitmapSource)
    {
        try
        {
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
            encoder.Save(ms);
            return Convert.ToHexString(SHA256.HashData(ms.ToArray()));
        }
        catch { return string.Empty; }
    }
}
