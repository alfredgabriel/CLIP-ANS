using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using QuizHelper.Models;

namespace QuizHelper.Core;

/// <summary>
/// Background clipboard watcher. Polls the clipboard every <see cref="AppConfig.PollIntervalMs"/>
/// milliseconds and fires <see cref="NewTextDetected"/> when:
/// - A new, sufficiently long text is found (normal text copy), OR
/// - A new image is detected (Win+Shift+S, Snipping Tool, screenshot) and OCR extracts enough text from it.
/// Uses Windows GetClipboardSequenceNumber to reliably detect every copy event.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    private readonly AppState  _state;
    private readonly DebounceHelper _debounce = new();
    private System.Threading.Timer? _timer;
    private uint _lastSequenceNumber;
    private string _lastHash = string.Empty;
    private bool _disposed;

    public event Func<string, CancellationToken, Task>? NewTextDetected;

    public ClipboardWatcher(AppState state)
    {
        _state = state;
    }

    // ── Public control ───────────────────────────────────────────────────────

    public void Start()
    {
        var config = _state.Config ?? new AppConfig();
        _lastSequenceNumber = GetClipboardSequenceNumber();
        _timer?.Dispose();
        _timer = new System.Threading.Timer(
            _ => Poll(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(config.PollIntervalMs));
        _state.Status = AppStatus.Watching;
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _debounce.Cancel();
    }

    // ── Internal polling ─────────────────────────────────────────────────────

    private void Poll()
    {
        if (_state.Config is null) return;
        if (!_state.DetectionEnabled) return;

        // Check if clipboard changed in Windows
        uint currentSeq = GetClipboardSequenceNumber();
        if (currentSeq == _lastSequenceNumber && currentSeq != 0)
            return;

        var config = _state.Config;
        string text = string.Empty;
        byte[]? imageBytes = null;

        // Try reading clipboard with retries in case Snipping Tool (Win+Shift+S) or another app has locked it
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // 1. Check text
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        text = System.Windows.Clipboard.GetText();
                    }

                    // 2. Check image if no text or screenshot detection is active
                    if (string.IsNullOrWhiteSpace(text) && config.DetectScreenshots)
                    {
                        // Prefer WinForms Clipboard — handles Snipping Tool GDI+ DIB/Bitmap natively without COM thread issues
                        if (System.Windows.Forms.Clipboard.ContainsImage())
                        {
                            using var gdiImg = System.Windows.Forms.Clipboard.GetImage();
                            if (gdiImg != null)
                            {
                                using var ms = new MemoryStream();
                                gdiImg.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                imageBytes = ms.ToArray();
                            }
                        }
                        // Fallback to WPF Clipboard if needed
                        if (imageBytes == null && System.Windows.Clipboard.ContainsImage())
                        {
                            var wpfImg = System.Windows.Clipboard.GetImage();
                            if (wpfImg != null)
                            {
                                var encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(wpfImg));
                                using var ms = new MemoryStream();
                                encoder.Save(ms);
                                imageBytes = ms.ToArray();
                            }
                        }
                    }
                });

                if (!string.IsNullOrWhiteSpace(text) || imageBytes != null)
                    break;
            }
            catch
            {
                // Clipboard temporarily locked by screenshot tool writing data
                Thread.Sleep(75);
            }
        }

        _lastSequenceNumber = currentSeq;

        // ── Text path ────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(text) && text.Length >= config.MinTextLength)
        {
            var hash = ComputeHash(text);
            if (hash == _lastHash && _state.Status == AppStatus.Answered) return;
            _lastHash = hash;

            _debounce.Debounce(config.DebounceMs, async ct =>
            {
                if (NewTextDetected is not null)
                    await NewTextDetected.Invoke(text, ct);
            });
            return;
        }

        // ── Image/screenshot path ────────────────────────────────────────────
        if (config.DetectScreenshots && imageBytes != null && imageBytes.Length > 0)
        {
            var bmpHash = Convert.ToHexString(SHA256.HashData(imageBytes));
            if (bmpHash == _lastHash) return;
            _lastHash = bmpHash;

            _ = Task.Run(async () =>
            {
                try
                {
                    var ocrText = await OcrHelper.RecognizeTextAsync(imageBytes);
                    if (string.IsNullOrWhiteSpace(ocrText) || ocrText.Length < config.MinTextLength)
                        return;

                    _debounce.Debounce(config.DebounceMs, async ct =>
                    {
                        if (NewTextDetected is not null)
                            await NewTextDetected.Invoke(ocrText, ct);
                    });
                }
                catch { }
            });
        }
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _debounce.Cancel();
    }
}
