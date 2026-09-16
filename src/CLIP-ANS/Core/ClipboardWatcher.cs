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
/// - A new image is detected and OCR extracts enough text from it (screenshot detection).
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

        _lastSequenceNumber = currentSeq;

        bool hasText  = false;
        bool hasImage = false;
        string text   = string.Empty;
        BitmapSource? bitmapSource = null;

        try
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                hasText  = System.Windows.Clipboard.ContainsText();
                hasImage = System.Windows.Clipboard.ContainsImage();

                if (hasText)
                    text = System.Windows.Clipboard.GetText();

                if (hasImage && !hasText)
                    bitmapSource = System.Windows.Clipboard.GetImage();
            });
        }
        catch { return; }

        var config = _state.Config;

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
        if (config.DetectScreenshots && bitmapSource != null)
        {
            var bmpHash = OcrHelper.HashBitmap(bitmapSource);
            if (bmpHash == _lastHash) return;
            _lastHash = bmpHash;

            _ = Task.Run(async () =>
            {
                try
                {
                    var ocrText = await OcrHelper.RecognizeTextAsync(bitmapSource);
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
