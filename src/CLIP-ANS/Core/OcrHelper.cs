using System.IO;
using System.Security.Cryptography;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace QuizHelper.Core;

/// <summary>
/// Wraps Windows.Media.Ocr to extract text from clipboard images (screenshots).
/// Uses the best available installed OCR language (es-ES preferred, en-US fallback).
/// All processing is local/offline — no network calls, no token consumption.
/// </summary>
public static class OcrHelper
{
    private static OcrEngine? _engine;
    private static readonly object _engineLock = new();

    private static OcrEngine? GetEngine()
    {
        if (_engine != null) return _engine;

        lock (_engineLock)
        {
            if (_engine != null) return _engine;

            try
            {
                _engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
            catch { }

            if (_engine == null)
            {
                var langs = OcrEngine.AvailableRecognizerLanguages;
                var lang  = langs.FirstOrDefault(l => l.LanguageTag.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                         ?? langs.FirstOrDefault();
                if (lang != null)
                {
                    try { _engine = OcrEngine.TryCreateFromLanguage(lang); }
                    catch { }
                }
            }
        }

        return _engine;
    }

    /// <summary>
    /// Recognises text in the given PNG/JPEG image bytes.
    /// Returns the full concatenated OCR text, or null if recognition is unavailable.
    /// </summary>
    public static async Task<string?> RecognizeTextAsync(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0) return null;

        var engine = GetEngine();
        if (engine == null) return null;

        try
        {
            using var ras = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(ras))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
            ras.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(ras);
            var softBmp = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

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
}
