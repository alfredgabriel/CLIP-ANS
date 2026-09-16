namespace QuizHelper.Core;

/// <summary>
/// Simple debounce helper that delays an async action until a specified
/// quiet period has elapsed since the last call.
/// Thread-safe for multiple concurrent callers.
/// </summary>
public sealed class DebounceHelper
{
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();

    /// <summary>
    /// Schedules <paramref name="action"/> to run after <paramref name="delayMs"/>
    /// milliseconds of silence. Any previous pending call is cancelled.
    /// </summary>
    public void Debounce(int delayMs, Func<CancellationToken, Task> action)
    {
        CancellationTokenSource newCts;
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            newCts = new CancellationTokenSource();
            _cts = newCts;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, newCts.Token);
                if (!newCts.Token.IsCancellationRequested)
                    await action(newCts.Token);
            }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DebounceHelper] Error: {ex.Message}");
            }
        });
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
