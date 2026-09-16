using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
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
    public SolidColorBrush ColorBrush =>
        new(ColorFromHex(HexColor));

    private static Color ColorFromHex(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.White; }
    }
}

/// <summary>
/// Main configuration window. Closing hides the window; the process stays alive.
/// </summary>
public partial class MainWindow : Window
{
    public event Action<AppConfig>? ConfigChanged;

    private readonly ConfigService _configService = new();
    private AppConfig _config;
    private bool _apiKeyDirty;
    private bool _showingKey;
    private ObservableCollection<LegendItem> _legendItems = [];

    public MainWindow()
    {
        InitializeComponent();
        _config = _configService.Load();
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
        DetectionToggle.IsChecked = AppState.Instance.DetectionEnabled;

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

    private void BuildLegendItems()
    {
        _legendItems = [];
        foreach (var kv in _config.ColorMap)
            _legendItems.Add(new LegendItem { Letter = kv.Key, HexColor = kv.Value });
        LegendItems.ItemsSource = _legendItems;
    }

    private void UpdateStatusChip()
    {
        var status = AppState.Instance.Status;
        var config = _config;

        System.Windows.Media.Color chipColor = status switch
        {
            AppStatus.Querying => Colors.Cyan,
            AppStatus.Error    => Colors.Gray,
            AppStatus.Paused   => Colors.DarkGray,
            AppStatus.Answered => GetAnswerChipColor(config),
            _                  => Colors.White,
        };
        StatusChip.Background = new SolidColorBrush(chipColor);
    }

    private System.Windows.Media.Color GetAnswerChipColor(AppConfig config)
    {
        var answers = AppState.Instance.LastAnswers;
        if (answers.Count == 0) return Colors.White;

        var colors = answers
            .Select(a =>
            {
                var hex = config.ColorMap.TryGetValue(a.ToString(), out var h) ? h : "#FFFFFF";
                try { return (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex); }
                catch { return Colors.White; }
            }).ToList();

        if (colors.Count == 1) return colors[0];

        // Blend
        return System.Windows.Media.Color.FromRgb(
            (byte)colors.Average(c => c.R),
            (byte)colors.Average(c => c.G),
            (byte)colors.Average(c => c.B));
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

    private async void TestConnectionBtn_Click(object sender, RoutedEventArgs e)
    {
        TestResultLabel.Text      = "PROBANDO...";
        TestResultLabel.Foreground = System.Windows.Media.Brushes.Cyan;

        var key = _showingKey ? ApiKeyPlain.Text : ApiKeyBox.Password;
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

            var parsed = AnswerParser.Parse(result);
            TestResultLabel.Text = parsed.Count > 0
                ? $"✓ OK — Resp: {string.Join(",", parsed)}"
                : $"✓ OK — Resp: {result.Trim()}";
            TestResultLabel.Foreground = new SolidColorBrush(Colors.Lime);
        }
        catch (TimeoutException)
        {
            TestResultLabel.Text      = "✗ TIMEOUT";
            TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
        }
        catch (Exception ex)
        {
            TestResultLabel.Text = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
                ? "✗ API KEY INVÁLIDA"
                : $"✗ ERROR: {ex.Message[..Math.Min(40, ex.Message.Length)]}";
            TestResultLabel.Foreground = System.Windows.Media.Brushes.White;
        }
    }

    // ── Event handlers — Behaviour sliders ────────────────────────────────────

    private void MinLengthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _config.MinTextLength = (int)e.NewValue;
        if (MinLengthLabel != null)
            MinLengthLabel.Text = $"{(int)e.NewValue} chars";
    }

    private void DebounceSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _config.DebounceMs = (int)e.NewValue;
        if (DebounceLabel != null)
            DebounceLabel.Text = $"{(int)e.NewValue} ms";
    }

    private void DetectionToggle_Changed(object sender, RoutedEventArgs e)
    {
        AppState.Instance.DetectionEnabled = DetectionToggle.IsChecked ?? true;
    }

    // ── Event handlers — Save ─────────────────────────────────────────────────

    private void SaveConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        // Update model
        _config.Model = (ModelCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                        ?? _config.Model;

        // Save API key if changed
        if (_apiKeyDirty)
        {
            var key = _showingKey ? ApiKeyPlain.Text : ApiKeyBox.Password;
            if (!string.IsNullOrWhiteSpace(key))
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
            "QUIZ HELPER",
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
