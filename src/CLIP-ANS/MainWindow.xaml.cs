using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using QuizHelper.AI;
using QuizHelper.Core;
using QuizHelper.Models;

namespace QuizHelper;

/// <summary>
/// WPF data-binding proxy for AppState (INotifyPropertyChanged singleton).
/// Used as a StaticResource in XAML DataContext bindings.
/// </summary>
public class AppStateProxy
{
    public static AppState Instance => AppState.Instance;
}

/// <summary>
/// ViewModel item for the color legend DataTemplate.
/// </summary>
public class LegendItem
{
    public string Letter { get; set; } = string.Empty;
    public string HexColor { get; set; } = "#FFFFFF";
    /// <summary>True when this option can be deleted (user-added extras).</summary>
    public bool CanDelete { get; set; }
    public SolidColorBrush ColorBrush =>
        new(ColorFromHex(HexColor));

    private static System.Windows.Media.Color ColorFromHex(string hex)
    {
        try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
        catch { return Colors.White; }
    }
}

/// <summary>
/// Main configuration window. Closing hides the window; the process stays alive.
/// </summary>
public partial class MainWindow : Window
{
    public event Action<AppConfig>? ConfigChanged;
    public event Action<AppConfig>? OverlayToggleRequested;

    private readonly ConfigService _configService = new();
    private AppConfig _config = new();
    private bool _apiKeyDirty;
    private bool _showingKey;
    private bool _initialized;
    private ObservableCollection<LegendItem> _legendItems = [];

    public MainWindow()
    {
        _config = _configService.Load();
        InitializeComponent();
        _initialized = true;
        PopulateFromConfig();
        AppState.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Status) ||
                e.PropertyName == nameof(AppState.LastAnswers))
                Dispatcher.Invoke(UpdateStatusChip);
        };
        AppState.Instance.History.CollectionChanged += (_, _) =>
            Dispatcher.Invoke(RefreshHistoryList);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Always-on: hide instead of close
        e.Cancel = true;
        Hide();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SETICON = 0x0080;
    private const IntPtr ICON_SMALL = 0;
    private const IntPtr ICON_BIG = 1;

    private System.Drawing.Icon? _wndIconBig;
    private System.Drawing.Icon? _wndIconSmall;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyWindowIcons();
    }

    private void ApplyWindowIcons()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Stream? iconStream = null;

            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon.ico"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", "app_icon.ico"),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "CLIP-ANS", "Resources", "app_icon.ico")
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    iconStream = File.OpenRead(p);
                    break;
                }
            }

            if (iconStream == null)
            {
                try
                {
                    var sri = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app_icon.ico"));
                    iconStream = sri?.Stream;
                }
                catch { }
            }

            if (iconStream != null)
            {
                using (iconStream)
                {
                    var ms = new MemoryStream();
                    iconStream.CopyTo(ms);

                    ms.Position = 0;
                    _wndIconBig = new System.Drawing.Icon(ms, 32, 32);

                    ms.Position = 0;
                    _wndIconSmall = new System.Drawing.Icon(ms, 16, 16);

                    SendMessage(hwnd, WM_SETICON, ICON_BIG, _wndIconBig.Handle);
                    SendMessage(hwnd, WM_SETICON, ICON_SMALL, _wndIconSmall.Handle);

                    ms.Position = 0;
                    Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                        ms,
                        System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                        System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                }
            }
        }
        catch { }
    }

    // ── Initial population ────────────────────────────────────────────────────

    private void PopulateFromConfig()
    {
        // Provider combo
        ProviderCombo.SelectedIndex = _config.Provider.ToLower() == "openai" ? 1 : 0;
        PopulateModelCombo(_config.Provider);
        SelectModelInCombo(_config.Model);

        // Load API key from vault (masked)
        var key = _configService.LoadApiKey(_config.Provider);
        if (!string.IsNullOrEmpty(key))
            ApiKeyBox.Password = key;

        // Behaviour sliders
        MinLengthSlider.Value = _config.MinTextLength;
        DebounceSlider.Value  = _config.DebounceMs;
        TextDetectionToggle.IsChecked = _config.DetectText;
        ScreenshotToggle.IsChecked    = _config.DetectScreenshots;
        NotificationsToggle.IsChecked = _config.ShowNotifications;
        OverlayToggleBtn.IsChecked    = _config.ShowOverlay;

        // Legend
        BuildLegendItems();
        LegendItems.ItemsSource = _legendItems;

        UpdateStatusChip();
    }

    private void PopulateModelCombo(string provider)
    {
        ModelCombo.Items.Clear();
        var models = AppConfig.ProviderModels.TryGetValue(provider.ToLower(), out var m)
            ? m : ["(default)"];
        foreach (var model in models)
            ModelCombo.Items.Add(new ComboBoxItem { Content = model, Tag = model });
        if (ModelCombo.Items.Count > 0) ModelCombo.SelectedIndex = 0;
    }

    private void SelectModelInCombo(string model)
    {
        foreach (ComboBoxItem item in ModelCombo.Items)
        {
            if (item.Tag?.ToString() == model)
            {
                ModelCombo.SelectedItem = item;
                return;
            }
        }
    }

    public void UpdateSelectedModel(string newModel)
    {
        _config.Model = newModel;
        SelectModelInCombo(newModel);
    }

    private static readonly HashSet<string> _defaultLetters = ["A","B","C","D","E","F"];

    private void BuildLegendItems()
    {
        _legendItems = [];
        foreach (var kv in _config.ColorMap.OrderBy(k => k.Key))
            _legendItems.Add(new LegendItem
            {
                Letter    = kv.Key,
                HexColor  = kv.Value,
                CanDelete = !_defaultLetters.Contains(kv.Key),
            });
        LegendItems.ItemsSource = _legendItems;
    }

    private void UpdateStatusChip()
    {
        if (StatusChip == null) return;
        var status = AppState.Instance.Status;
        var config = _config;

        StatusChip.Background = status switch
        {
            AppStatus.Querying => new SolidColorBrush(Colors.Cyan),
            AppStatus.Error    => new SolidColorBrush(Colors.Gray),
            AppStatus.Paused   => new SolidColorBrush(Colors.DarkGray),
            AppStatus.Answered => BuildAnswerBrush(config),
            _                  => new SolidColorBrush(Colors.White),
        };
    }

    private System.Windows.Media.Brush BuildAnswerBrush(AppConfig config)
    {
        var answers = AppState.Instance.LastAnswers;
        if (answers.Count == 0) return new SolidColorBrush(Colors.White);

        var colors = answers
            .Select(a =>
            {
                var hex = config.ColorMap.TryGetValue(a.ToString(), out var h) ? h : "#FFFFFF";
                try { return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex); }
                catch { return Colors.White; }
            }).ToList();

        if (colors.Count == 1)
            return new SolidColorBrush(colors[0]);

        // Sharp gradient stops dividing into N equal vertical slices
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint   = new System.Windows.Point(1, 0)
        };

        double step = 1.0 / colors.Count;
        for (int i = 0; i < colors.Count; i++)
        {
            double startOffset = i * step;
            double endOffset   = (i + 1) * step;
            brush.GradientStops.Add(new GradientStop(colors[i], startOffset));
            brush.GradientStops.Add(new GradientStop(colors[i], endOffset));
        }

        return brush;
    }

    // ── Event handlers — Provider / Model ────────────────────────────────────

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = (ProviderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "groq";
        _config.Provider = tag;
        PopulateModelCombo(tag);

        // Pre-fill key if already stored
        var key = _configService.LoadApiKey(tag);
        ApiKeyBox.Password = key ?? string.Empty;
        if (_showingKey) ApiKeyPlain.Text = key ?? string.Empty;
    }

    // ── Event handlers — API Key ──────────────────────────────────────────────

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _apiKeyDirty = true;

    private void ApiKeyPlain_TextChanged(object sender, TextChangedEventArgs e)
    {
        _apiKeyDirty = true;
        ApiKeyBox.Password = ApiKeyPlain.Text;
    }

    private void ShowKeyBtn_Click(object sender, RoutedEventArgs e)
    {
        _showingKey = !_showingKey;
        if (_showingKey)
        {
            ApiKeyPlain.Text    = ApiKeyBox.Password;
            ApiKeyBox.Visibility  = Visibility.Collapsed;
            ApiKeyPlain.Visibility = Visibility.Visible;
            ShowKeyBtn.Content  = "◎";
        }
        else
        {
            ApiKeyBox.Password  = ApiKeyPlain.Text;
            ApiKeyPlain.Visibility = Visibility.Collapsed;
            ApiKeyBox.Visibility  = Visibility.Visible;
            ShowKeyBtn.Content  = "◉";
        }
    }

    // ── Event handlers — Test connection ─────────────────────────────────────

    private async void RefreshModelsBtn_Click(object sender, RoutedEventArgs e)
    {
        var key = (_showingKey ? ApiKeyPlain.Text : ApiKeyBox.Password).Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length < 25 || key.Contains("..."))
        {
            TestResultLabel.Text       = "✗ PEGA TU API KEY PRIMERO";
            TestResultLabel.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        RefreshModelsBtn.IsEnabled = false;
        TestResultLabel.Text       = "CONSULTANDO MODELOS...";
        TestResultLabel.Foreground = System.Windows.Media.Brushes.Cyan;

        try
        {
            var models = await FetchAvailableModelsAsync(_config.Provider, key);
            if (models.Count > 0)
            {
                ModelCombo.Items.Clear();
                foreach (var m in models)
                    ModelCombo.Items.Add(new ComboBoxItem { Content = m, Tag = m });
                ModelCombo.SelectedIndex = 0;

                TestResultLabel.Text       = $"✓ {models.Count} MODELOS CARGADOS";
                TestResultLabel.Foreground = new SolidColorBrush(Colors.Lime);
            }
            else
            {
                TestResultLabel.Text       = "✗ NO SE PUDIERON CARGAR";
                TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
            }
        }
        catch (Exception ex)
        {
            TestResultLabel.Text       = $"✗ ERROR: {ex.Message[..Math.Min(35, ex.Message.Length)]}";
            TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
        }
        finally
        {
            RefreshModelsBtn.IsEnabled = true;
        }
    }

    private async Task<List<string>> FetchAvailableModelsAsync(string provider, string key)
    {
        var list = new List<string>();
        try
        {
            string url = provider.ToLowerInvariant() == "openai"
                ? "https://api.openai.com/v1/models"
                : "https://api.groq.com/openai/v1/models";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var req  = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return list;

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                foreach (var el in data.EnumerateArray())
                {
                    if (el.TryGetProperty("id", out var idProp))
                    {
                        var id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id) &&
                            !id.Contains("whisper") &&
                            !id.Contains("tts") &&
                            !id.Contains("guard") &&
                            !id.Contains("embed") &&
                            !id.Contains("bge"))
                        {
                            list.Add(id);
                        }
                    }
                }
            }
        }
        catch { /* ignore background network issues */ }
        return list;
    }

    private async void TestConnectionBtn_Click(object sender, RoutedEventArgs e)
    {
        var key = (_showingKey ? ApiKeyPlain.Text : ApiKeyBox.Password).Trim();

        if (string.IsNullOrWhiteSpace(key))
        {
            TestResultLabel.Text       = "✗ PEGA TU API KEY";
            TestResultLabel.Foreground = new SolidColorBrush(Colors.Orange);
            return;
        }

        if (key.Contains("...") || key.Length < 25)
        {
            TestResultLabel.Text       = "✗ CLAVE OCULTA / INCOMPLETA";
            TestResultLabel.Foreground = new SolidColorBrush(Colors.Orange);
            System.Windows.MessageBox.Show(
                "La clave copiada parece incompleta o contiene '...'.\n\n" +
                "En la web de Groq, debes hacer clic en el botón '+ Create API Key', " +
                "escribir un nombre y copiar la clave secreta completa en la ventana que aparece " +
                "haciendo clic en 'Copy' antes de cerrarla.",
                "CLIP-ANS — API Key Incompleta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        TestResultLabel.Text       = "PROBANDO...";
        TestResultLabel.Foreground = System.Windows.Media.Brushes.Cyan;

        var provider = _config.Provider;
        var model    = (ModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                       ?? _config.Model;

        IAIClient client = provider == "openai"
            ? new OpenAIClient(key, model, 10)
            : new GroqClient(key, model, 10);

        try
        {
            var result = await client.AskAsync(
                "Pregunta: ¿Cuál es la capital de Francia?\nA) Madrid\nB) París\nC) Roma\nD) Berlín");

            var parsed = AnswerParser.Parse(result, _config.ColorMap.Keys);
            var displayText = parsed.IsMultipleChoice && parsed.Letters.Count > 0
                ? $"✓ OK — Resp: {string.Join(",", parsed.Letters)}"
                : $"✓ OK — Resp: {(parsed.DirectText.Length > 30 ? parsed.DirectText[..30] + "…" : parsed.DirectText)}";
            TestResultLabel.Text       = displayText;
            TestResultLabel.Foreground = new SolidColorBrush(Colors.Lime);

            // Auto-save working configuration immediately
            _config.Model = model;
            _configService.SaveApiKey(_config.Provider, key);
            _configService.Save(_config);
            ConfigChanged?.Invoke(_config);
            _apiKeyDirty = false;
        }
        catch (TimeoutException)
        {
            TestResultLabel.Text       = "✗ TIMEOUT";
            TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("401") || msg.Contains("Unauthorized") || msg.Contains("Invalid API Key"))
            {
                TestResultLabel.Text       = "✗ API KEY INVÁLIDA";
                TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
            }
            else if (msg.Contains("decommissioned") || msg.Contains("not found") || msg.Contains("does not exist") || msg.Contains("model"))
            {
                TestResultLabel.Text       = "BUSCANDO MODELOS ACTIVOS...";
                TestResultLabel.Foreground = System.Windows.Media.Brushes.Cyan;

                var liveModels = await FetchAvailableModelsAsync(provider, key);
                if (liveModels.Count > 0)
                {
                    ModelCombo.Items.Clear();
                    foreach (var m in liveModels)
                        ModelCombo.Items.Add(new ComboBoxItem { Content = m, Tag = m });
                    ModelCombo.SelectedIndex = 0;
                    var chosen = (ModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? liveModels[0];
                    TestResultLabel.Text       = $"↻ ASIGNADO '{chosen}'. VUELVE A PROBAR";
                    TestResultLabel.Foreground = new SolidColorBrush(Colors.Yellow);
                }
                else
                {
                    TestResultLabel.Text       = "✗ MODELO NO DISPONIBLE";
                    TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
                }
            }
            else
            {
                TestResultLabel.Text       = $"✗ ERROR: {msg[..Math.Min(35, msg.Length)]}";
                TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
            }
        }
    }

    // ── Event handlers — Behaviour sliders ────────────────────────────────────

    private void MinLengthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized || _config == null) return;
        _config.MinTextLength = (int)e.NewValue;
        if (MinLengthLabel != null)
            MinLengthLabel.Text = $"{(int)e.NewValue} chars";
    }

    private void DebounceSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized || _config == null) return;
        _config.DebounceMs = (int)e.NewValue;
        if (DebounceLabel != null)
            DebounceLabel.Text = $"{(int)e.NewValue} ms";
    }

    private void TextDetectionToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized || _config == null) return;
        _config.DetectText = TextDetectionToggle.IsChecked ?? true;
        ApplyDetectionState();
    }

    private void ScreenshotToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized || _config == null) return;
        _config.DetectScreenshots = ScreenshotToggle.IsChecked ?? true;
        ApplyDetectionState();
    }

    private void NotificationsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized || _config == null) return;
        _config.ShowNotifications = NotificationsToggle.IsChecked ?? true;
        _configService.Save(_config);
    }

    private void ApplyDetectionState()
    {
        if (_config == null) return;
        var bothDisabled = !_config.DetectText && !_config.DetectScreenshots;
        AppState.Instance.DetectionEnabled = !bothDisabled;

        if (bothDisabled)
            AppState.Instance.Status = AppStatus.Paused;
        else if (AppState.Instance.Status == AppStatus.Paused)
            AppState.Instance.Status = AppStatus.Watching;

        _configService.Save(_config);
        UpdateStatusChip();
    }

    public void SyncTogglesFromConfig(AppConfig config)
    {
        _config = config;
        TextDetectionToggle.IsChecked = config.DetectText;
        ScreenshotToggle.IsChecked    = config.DetectScreenshots;
        NotificationsToggle.IsChecked = config.ShowNotifications;
        OverlayToggleBtn.IsChecked    = config.ShowOverlay;
        UpdateStatusChip();
    }

    private void OverlayToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        bool nowOn = OverlayToggleBtn.IsChecked == true;
        _config.ShowOverlay = nowOn;
        OverlayToggleRequested?.Invoke(_config);
    }

    // ── Event handlers — Add / Delete color option ────────────────────────────

    private void AddOptionBtn_Click(object sender, RoutedEventArgs e)
    {
        var next = _config.NextAvailableLetter();
        if (next is null)
        {
            System.Windows.MessageBox.Show(
                "Has llegado al límite de 26 opciones (A-Z).",
                "CLIP-ANS", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var color = _config.DefaultColorForNewLetter();
        _config.ColorMap[next] = color;
        BuildLegendItems();
        Dispatcher.InvokeAsync(() => LegendScrollViewer.ScrollToEnd(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void DeleteOptionBtn_Click(object sender, RoutedEventArgs e)
    {
        var btn    = (System.Windows.Controls.Button)sender;
        var letter = btn.Tag?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(letter) || _defaultLetters.Contains(letter)) return;
        _config.ColorMap.Remove(letter);
        BuildLegendItems();
        UpdateStatusChip();
    }

    private void CopyLastAnswerBtn_Click(object sender, RoutedEventArgs e)
    {
        var text = AppState.Instance.IsDirectAnswer
            ? AppState.Instance.LastDirectAnswer
            : AppState.Instance.LastAnswerDisplay;

        if (!string.IsNullOrWhiteSpace(text) && text != "—")
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                System.Windows.MessageBox.Show(
                    "Respuesta copiada al portapapeles.",
                    "CLIP-ANS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }
    }

    // ── Event handlers — Save ─────────────────────────────────────────────────

    private void SaveConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        // Update model
        _config.Model = (ModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                        ?? _config.Model;

        // Save API key if modified
        if (_apiKeyDirty)
        {
            var key = (_showingKey ? ApiKeyPlain.Text : ApiKeyBox.Password).Trim();
            if (!string.IsNullOrWhiteSpace(key) && !key.Contains("..."))
            {
                _configService.SaveApiKey(_config.Provider, key);
                _apiKeyDirty = false;
            }
        }

        // Fire hot-reload
        ConfigChanged?.Invoke(_config);
        _configService.Save(_config);

        // Visual confirmation
        System.Windows.MessageBox.Show(
            "CONFIGURACIÓN GUARDADA",
            "CLIP-ANS",
            MessageBoxButton.OK,
            MessageBoxImage.None);
    }

    // ── Event handlers — Legend ───────────────────────────────────────────────

    private void ColorChip_Click(object sender, RoutedEventArgs e)
    {
        var btn    = (System.Windows.Controls.Button)sender;
        var letter = btn.Tag?.ToString() ?? string.Empty;
        var item   = _legendItems.FirstOrDefault(i => i.Letter == letter);
        if (item is null) return;

        // Open WinForms color dialog (native Windows look)
        using var dlg = new ColorDialog
        {
            Color = System.Drawing.ColorTranslator.FromHtml(item.HexColor),
            FullOpen = true,
            AllowFullOpen = true,
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
            item.HexColor = hex;
            _config.ColorMap[letter] = hex;

            // Refresh list
            BuildLegendItems();
            UpdateStatusChip();
        }
    }

    private void ResetColorsBtn_Click(object sender, RoutedEventArgs e)
    {
        _config.ColorMap = AppConfig.DefaultColorMap;
        BuildLegendItems();
        UpdateStatusChip();
    }

    // ── Event handlers — History ──────────────────────────────────────────────

    private void HistoryToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        bool visible = HistoryPanel.Visibility == Visibility.Visible;
        HistoryPanel.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        HistoryToggleBtn.Content = visible ? "+ HISTORIAL" : "− HISTORIAL";
    }

    private void RefreshHistoryList()
    {
        HistoryList.Items.Clear();
        foreach (var entry in AppState.Instance.History)
        {
            HistoryList.Items.Add(new
            {
                Hora     = entry.Timestamp.ToString("HH:mm:ss"),
                Pregunta = entry.QuestionPreview,
                Resp     = entry.AnswerDisplay,
            });
        }
    }
}
