using System.Windows;
using QuizHelper.AI;
using QuizHelper.Core;
using QuizHelper.Models;
using QuizHelper.Tray;

namespace QuizHelper;

/// <summary>
/// Application entry point. Manages the always-on lifecycle:
///   - ShutdownMode = OnExplicitShutdown (set in App.xaml)
///   - Window X closes → Hide(), process stays alive
///   - Only "Salir" from tray menu calls Shutdown()
/// </summary>
public partial class App : System.Windows.Application
{
    private TrayManager?    _tray;
    private MainWindow?     _mainWindow;
    private ClipboardWatcher? _watcher;
    private IAIClient?      _aiClient;
    private ConfigService   _configService = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 1. Load configuration
            var config  = _configService.Load();
            var state   = AppState.Instance;
            state.Config = config;

            // 2. Create tray icon (always visible)
            _tray = new TrayManager(state);
            _tray.OpenRequested  += ShowMainWindow;
            _tray.ExitRequested  += () => Shutdown();
            _tray.PauseToggled   += paused => state.DetectionEnabled = !paused;

            // 3. Create main window (hidden by default unless first run)
            _mainWindow = new MainWindow();
            _mainWindow.ConfigChanged += OnConfigChanged;

            bool firstRun = !_configService.ConfigExists();
            if (firstRun || config.ShowWindowOnStart)
                ShowMainWindow();

            // 4. Initialize AI client and clipboard watcher
            RebuildAIClient(config);
            StartWatcher();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error al iniciar CLIP-ANS:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "CLIP-ANS — Error de inicio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    // ── Window management ─────────────────────────────────────────────────────

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    // ── Config hot-reload ─────────────────────────────────────────────────────

    private void OnConfigChanged(AppConfig newConfig)
    {
        AppState.Instance.Config = newConfig;
        _configService.Save(newConfig);
        RebuildAIClient(newConfig);
        // Restart watcher with new poll interval / min length
        _watcher?.Stop();
        StartWatcher();
    }

    // ── AI client factory ─────────────────────────────────────────────────────

    private void RebuildAIClient(AppConfig config)
    {
        var apiKey = _configService.LoadApiKey(config.Provider);
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        _aiClient = config.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIClient(apiKey, config.Model, config.TimeoutSeconds),
            _        => new GroqClient(apiKey, config.Model, config.TimeoutSeconds),
        };
    }

    // ── Clipboard watcher ─────────────────────────────────────────────────────

    private void StartWatcher()
    {
        _watcher?.Dispose();
        _watcher = new ClipboardWatcher(AppState.Instance);
        _watcher.NewTextDetected += HandleNewClipboardText;
        _watcher.Start();
    }

    private async Task HandleNewClipboardText(string text, CancellationToken ct)
    {
        if (_aiClient is null)
        {
            AppState.Instance.Status           = AppStatus.Error;
            AppState.Instance.LastErrorMessage = "API key no configurada";
            return;
        }

        var state = AppState.Instance;
        state.Status = AppStatus.Querying;
        _tray?.UpdateIconFromState();

        try
        {
            var response = await _aiClient.AskAsync(text, ct);
            var answers  = AnswerParser.Parse(response);

            if (AnswerParser.IsValid(answers))
            {
                state.LastAnswers = answers;
                state.Status      = AppStatus.Answered;

                // Add to history
                var config     = state.Config ?? new AppConfig();
                var firstColor = answers.Count > 0
                    ? IconRenderer.ParseHexColor(
                        config.ColorMap.TryGetValue(answers[0].ToString(), out var h) ? h : "#FFFFFF")
                    : System.Drawing.Color.White;

                state.AddHistory(new HistoryEntry
                {
                    Timestamp       = DateTime.Now,
                    QuestionPreview = text.Length > 80 ? text[..80] + "…" : text,
                    Answers         = answers,
                    AnswerColor     = System.Windows.Media.Color.FromArgb(
                        firstColor.A, firstColor.R, firstColor.G, firstColor.B),
                });
            }
            else
            {
                state.LastErrorMessage = "Respuesta no parseable";
                state.Status           = AppStatus.Error;
            }
        }
        catch (TimeoutException)
        {
            state.LastErrorMessage = "Timeout (8s)";
            state.Status           = AppStatus.Error;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            state.LastErrorMessage = ex.Message.Length > 60
                ? ex.Message[..60] + "…"
                : ex.Message;
            state.Status = AppStatus.Error;
        }

        _tray?.UpdateIconFromState();

        // Auto-reset error after 3 seconds
        if (state.Status == AppStatus.Error)
        {
            await Task.Delay(3000, ct);
            if (state.Status == AppStatus.Error)
            {
                state.Status = state.DetectionEnabled ? AppStatus.Watching : AppStatus.Paused;
                _tray?.UpdateIconFromState();
            }
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void OnExit(ExitEventArgs e)
    {
        _watcher?.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
