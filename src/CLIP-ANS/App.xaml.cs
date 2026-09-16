using System.Runtime.InteropServices;
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
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

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
            SetCurrentProcessExplicitAppUserModelID("CLIPANS.App.v2");
        }
        catch { }

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
            _tray.ConfigToggled  += OnTrayConfigToggled;

            // If both detection modes are disabled, start in Paused state (gray icon)
            if (!config.DetectText && !config.DetectScreenshots)
            {
                state.DetectionEnabled = false;
                state.Status = AppStatus.Paused;
                _tray.UpdateIconFromState();
            }

            // 3. Create main window (hidden by default unless first run)
            _mainWindow = new MainWindow();
            _mainWindow.ConfigChanged += OnConfigChanged;

            bool firstRun = !_configService.ConfigExists();
            if (firstRun || config.ShowWindowOnStart)
                ShowMainWindow();

            // 4. Initialize AI client and clipboard watcher
            RebuildAIClient(config);
            StartWatcher();
            // 5. Silent startup connection check (auto-fixes stale model without user action)
            _ = StartupConnectionCheckAsync(config);
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

    private void OnTrayConfigToggled(AppConfig newConfig)
    {
        _configService.Save(newConfig);
        _mainWindow?.SyncTogglesFromConfig(newConfig);
    }

    // ── AI client factory ─────────────────────────────────────────────────────

    // ── Startup auto-connection check ─────────────────────────────────────────

    /// <summary>
    /// Runs silently after startup. If the saved model is decommissioned,
    /// fetches live models from the provider API and auto-switches to the
    /// first available one, saving the config without user intervention.
    /// </summary>
    private async Task StartupConnectionCheckAsync(AppConfig config)
    {
        // Wait a moment so the UI finishes loading and network is ready
        await Task.Delay(3000);

        if (_aiClient is null) return;

        try
        {
            // Try a minimal test prompt
            await _aiClient.AskAsync(
                "Pregunta: ¿Cuál es la capital de Francia?\nA) Madrid\nB) París\nC) Roma\nD) Berlín",
                CancellationToken.None);
            // Success — model is working fine, nothing to do
        }
        catch (Exception ex)
        {
            var msg = ex.Message.ToLowerInvariant();
            bool isModelError = msg.Contains("does not exist") ||
                                msg.Contains("not found")     ||
                                msg.Contains("decommissioned")  ||
                                msg.Contains("model_not_found");

            if (!isModelError) return; // network error etc — don't touch config

            // Try to get live model list and pick the first one that works
            try
            {
                var apiKey = _configService.LoadApiKey(config.Provider);
                if (string.IsNullOrWhiteSpace(apiKey)) return;

                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                using var req  = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get,
                    config.Provider.ToLowerInvariant() == "openai"
                        ? "https://api.openai.com/v1/models"
                        : "https://api.groq.com/openai/v1/models");
                req.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return;

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(body);

                var liveModels = new List<string>();
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var el in data.EnumerateArray())
                    {
                        if (el.TryGetProperty("id", out var idProp))
                        {
                            var id = idProp.GetString();
                            if (!string.IsNullOrEmpty(id)         &&
                                !id.Contains("whisper")            &&
                                !id.Contains("tts")                &&
                                !id.Contains("guard")              &&
                                !id.Contains("embed")              &&
                                !id.Contains("bge"))
                                liveModels.Add(id);
                        }
                    }
                }

                if (liveModels.Count == 0) return;

                // Pick the first live model, save and rebuild client
                var newModel = liveModels[0];
                Dispatcher.Invoke(() =>
                {
                    config.Model = newModel;
                    _configService.Save(config);
                    AppState.Instance.Config = config;
                    RebuildAIClient(config);
                    _mainWindow?.UpdateSelectedModel(newModel);
                    System.Diagnostics.Debug.WriteLine($"[CLIP-ANS] Startup auto-switch: modelo cambiado a {newModel}");
                });
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"[CLIP-ANS] StartupConnectionCheck fetch error: {innerEx.Message}");
            }
        }
    }

    private void RebuildAIClient(AppConfig config)
    {
        var apiKey = _configService.LoadApiKey(config.Provider);
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var candidates = AppConfig.ProviderModels.TryGetValue(config.Provider.ToLowerInvariant(), out var m) ? m : null;

        _aiClient = config.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIClient(apiKey, config.Model, config.TimeoutSeconds, candidates),
            _        => new GroqClient(apiKey, config.Model, config.TimeoutSeconds, candidates),
        };

        _aiClient.ModelAutoSwitched += OnModelAutoSwitched;
    }

    private void OnModelAutoSwitched(string newModel)
    {
        Dispatcher.Invoke(() =>
        {
            if (AppState.Instance.Config is { } cfg)
            {
                cfg.Model = newModel;
                _configService.Save(cfg);
            }
            _mainWindow?.UpdateSelectedModel(newModel);
            AppState.Instance.LastErrorMessage = $"Tokens agotados: cambiado a {newModel}";
        });
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
            var config   = state.Config ?? new AppConfig();
            var result   = AnswerParser.Parse(response, config.ColorMap.Keys);

            if (AnswerParser.IsValid(result))
            {
                state.IsDirectAnswer = !result.IsMultipleChoice;

                if (result.IsMultipleChoice)
                {
                    state.LastAnswers = result.Letters;
                    var firstColor = result.Letters.Count > 0
                        ? IconRenderer.ParseHexColor(
                            config.ColorMap.TryGetValue(result.Letters[0].ToString(), out var h) ? h : "#FFFFFF")
                        : System.Drawing.Color.White;

                    state.AddHistory(new HistoryEntry
                    {
                        Timestamp       = DateTime.Now,
                        QuestionPreview = text.Length > 80 ? text[..80] + "…" : text,
                        Answers         = result.Letters,
                        IsDirectAnswer  = false,
                        AnswerColor     = System.Windows.Media.Color.FromArgb(
                            firstColor.A, firstColor.R, firstColor.G, firstColor.B),
                    });
                }
                else
                {
                    state.LastDirectAnswer = result.DirectText;
                    state.AddHistory(new HistoryEntry
                    {
                        Timestamp       = DateTime.Now,
                        QuestionPreview = text.Length > 80 ? text[..80] + "…" : text,
                        DirectText      = result.DirectText,
                        IsDirectAnswer  = true,
                        AnswerColor     = System.Windows.Media.Color.FromArgb(255, 255, 255, 255),
                    });
                }

                state.Status = AppStatus.Answered;
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
