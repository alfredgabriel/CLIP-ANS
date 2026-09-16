using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuizHelper.Models;

/// <summary>
/// Represents a single entry in the answer history.
/// </summary>
public class HistoryEntry
{
    public DateTime Timestamp { get; init; }
    public string QuestionPreview { get; init; } = string.Empty;
    /// <summary>For multiple-choice answers.</summary>
    public List<char> Answers { get; init; } = [];
    /// <summary>For open-ended answers.</summary>
    public string DirectText { get; init; } = string.Empty;
    public bool IsDirectAnswer { get; init; }
    public string AnswerDisplay => IsDirectAnswer
        ? (DirectText.Length > 50 ? DirectText[..50] + "…" : DirectText)
        : string.Join(", ", Answers);
    public System.Windows.Media.Color AnswerColor { get; init; }
}

/// <summary>
/// Application status shown in the main window and tray tooltip.
/// </summary>
public enum AppStatus
{
    Idle,
    Watching,
    Querying,
    Answered,
    Error,
    Paused
}

/// <summary>
/// Central application state — all threads read/write this via thread-safe properties.
/// UI binds to this via INotifyPropertyChanged.
/// </summary>
public class AppState : INotifyPropertyChanged
{
    // ── Singleton ───────────────────────────────────────────────────────────
    private static AppState? _instance;
    public static AppState Instance => _instance ??= new AppState();
    private AppState() { }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        NotifyChanged(name);
    }

    public void NotifyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Status ───────────────────────────────────────────────────────────────
    private AppStatus _status = AppStatus.Idle;
    public AppStatus Status
    {
        get => _status;
        set
        {
            Set(ref _status, value);
            NotifyChanged(nameof(StatusText));
            NotifyChanged(nameof(StatusColor));
        }
    }

    public string StatusText => Status switch
    {
        AppStatus.Idle    => "ESPERANDO PREGUNTA",
        AppStatus.Watching => "MONITORIZANDO...",
        AppStatus.Querying => "CONSULTANDO IA...",
        AppStatus.Answered => $"ÚLTIMA RESPUESTA: {LastAnswerDisplay}",
        AppStatus.Error   => $"ERROR: {LastErrorMessage}",
        AppStatus.Paused  => "PAUSADO",
        _                 => "DESCONOCIDO"
    };

    public System.Windows.Media.Brush StatusColor => Status switch
    {
        AppStatus.Querying => System.Windows.Media.Brushes.Cyan,
        AppStatus.Error    => System.Windows.Media.Brushes.White,
        AppStatus.Paused   => System.Windows.Media.Brushes.DarkGray,
        _                  => System.Windows.Media.Brushes.White
    };

    // ── Last answer ──────────────────────────────────────────────────────────
    private List<char> _lastAnswers = [];
    public List<char> LastAnswers
    {
        get => _lastAnswers;
        set
        {
            _lastAnswers = value;
            NotifyChanged(nameof(LastAnswers));
            NotifyChanged(nameof(LastAnswerDisplay));
        }
    }

    private string _lastDirectAnswer = string.Empty;
    public string LastDirectAnswer
    {
        get => _lastDirectAnswer;
        set
        {
            _lastDirectAnswer = value;
            NotifyChanged(nameof(LastDirectAnswer));
            NotifyChanged(nameof(LastAnswerDisplay));
        }
    }

    private bool _isDirectAnswer;
    public bool IsDirectAnswer
    {
        get => _isDirectAnswer;
        set => Set(ref _isDirectAnswer, value);
    }

    public string LastAnswerDisplay
    {
        get
        {
            if (_isDirectAnswer)
                return string.IsNullOrWhiteSpace(_lastDirectAnswer) ? "—" : _lastDirectAnswer;
            return _lastAnswers.Count > 0
                ? string.Join(", ", _lastAnswers)
                : "—";
        }
    }

    private string _lastErrorMessage = string.Empty;
    public string LastErrorMessage
    {
        get => _lastErrorMessage;
        set => Set(ref _lastErrorMessage, value);
    }

    // ── Detection toggle ─────────────────────────────────────────────────────
    private bool _detectionEnabled = true;
    public bool DetectionEnabled
    {
        get => _detectionEnabled;
        set
        {
            Set(ref _detectionEnabled, value);
            if (Status != AppStatus.Error)
                Status = value ? AppStatus.Watching : AppStatus.Paused;
        }
    }

    // ── History ──────────────────────────────────────────────────────────────
    public ObservableCollection<HistoryEntry> History { get; } = [];

    public void AddHistory(HistoryEntry entry)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            History.Insert(0, entry);
            while (History.Count > 10) History.RemoveAt(History.Count - 1);
        });
    }

    // ── Config ref (set after ConfigService loads) ───────────────────────────
    public AppConfig? Config { get; set; }
}
