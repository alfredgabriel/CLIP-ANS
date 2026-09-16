using System.Security.Cryptography;
using System.Text;
using QuizHelper.Models;

namespace QuizHelper.Core;

/// <summary>
/// Background clipboard watcher. Polls the clipboard every <see cref="AppConfig.PollIntervalMs"/>
/// milliseconds and fires <see cref="NewTextDetected"/> when a new, sufficiently long
/// text is found. Applies a debounce before raising the event to avoid flooding the AI.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly AppState  _state;
    private readonly DebounceHelper _debounce = new();
    private System.Threading.Timer? _timer;
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

        string text = string.Empty;
        try
        {
            // Clipboard must be read on an STA thread
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (System.Windows.Clipboard.ContainsText())
                    text = System.Windows.Clipboard.GetText();
            });
        }
        catch { return; }

        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length < _state.Config.MinTextLength) return;

        // Only trigger on new content
        var hash = ComputeHash(text);
        if (hash == _lastHash) return;
        _lastHash = hash;

        // Debounce before firing
        _debounce.Debounce(_state.Config.DebounceMs, async ct =>
        {
            if (NewTextDetected is not null)
                await NewTextDetected.Invoke(text, ct);
        });
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
