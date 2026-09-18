using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using QuizHelper.Models;
using WpfBrushes    = System.Windows.Media.Brushes;
using WpfBrush      = System.Windows.Media.Brush;
using WpfColor      = System.Windows.Media.Color;
using WpfColors     = System.Windows.Media.Colors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using SolidBrush    = System.Windows.Media.SolidColorBrush;

namespace QuizHelper.Overlay;

/// <summary>
/// Always-on-top transparent overlay that shows the current answer even when
/// any app is running in fullscreen (F11). The user can drag it anywhere and
/// toggle it via tray menu or Ctrl+Shift+Space.
/// </summary>
public partial class OverlayWindow : Window
{
    private bool _visible;

    public OverlayWindow()
    {
        InitializeComponent();

        // Listen to state changes
        AppState.Instance.PropertyChanged += OnStateChanged;

        // Position: top-right corner of the primary screen
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 24;
        Top  = screen.Top   + 24;

        // Start hidden but Show() it so Topmost works across monitors
        Show();
        FadeOut(instant: true);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Shows or hides the overlay and persists the preference.</summary>
    public void SetVisible(bool show)
    {
        _visible = show;
        if (show)
        {
            RefreshDisplay();
            FadeIn();
        }
        else
        {
            FadeOut();
        }
    }

    /// <summary>Toggles overlay visibility and returns the new state.</summary>
    public bool Toggle()
    {
        SetVisible(!_visible);
        return _visible;
    }

    // ── State change handler ───────────────────────────────────────────────────

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_visible) return;

        if (e.PropertyName is nameof(AppState.Status)
                           or nameof(AppState.LastAnswers)
                           or nameof(AppState.LastDirectAnswer)
                           or nameof(AppState.IsDirectAnswer))
        {
            Dispatcher.Invoke(RefreshDisplay);
        }
    }

    private void RefreshDisplay()
    {
        var state  = AppState.Instance;
        var config = state.Config ?? new AppConfig();

        switch (state.Status)
        {
            case AppStatus.Querying:
                ShowWaiting("⏳");
                break;

            case AppStatus.Answered:
                if (state.IsDirectAnswer)
                    ShowDirectText(state.LastDirectAnswer);
                else
                    ShowLetters(state.LastAnswers, config.ColorMap);
                break;

            case AppStatus.Error:
                ShowDirectText("❌ " + (state.LastErrorMessage.Length > 30
                    ? state.LastErrorMessage[..30] + "…"
                    : state.LastErrorMessage));
                break;

            default:
                ShowWaiting("···");
                break;
        }

        // Auto-resize height to fit content
        SizeToContent = SizeToContent.Height;
    }

    // ── Display modes ──────────────────────────────────────────────────────────

    private void ShowLetters(IReadOnlyList<char> letters, Dictionary<string, string> colorMap)
    {
        LettersPanel.Visibility     = Visibility.Visible;
        TextScrollViewer.Visibility = Visibility.Collapsed;
        WaitingText.Visibility      = Visibility.Collapsed;

        LettersPanel.Children.Clear();

        if (letters.Count == 0)
        {
            ShowWaiting("···");
            return;
        }

        foreach (var letter in letters)
        {
            var key   = letter.ToString();
            var hex   = colorMap.TryGetValue(key, out var h) ? h : "#FFFFFF";
            WpfColor color = ParseHexToWpf(hex);

            var badge = new Border
            {
                Width           = 46,
                Height          = 46,
                CornerRadius    = new CornerRadius(23),
                BorderBrush     = WpfBrushes.White,
                BorderThickness = new Thickness(2),
                Margin          = new Thickness(3, 0, 3, 0),
                Background      = new SolidBrush(color),
            };

            var text = new TextBlock
            {
                Text                = key,
                FontFamily          = new WpfFontFamily("Consolas, Courier New"),
                FontSize            = 22,
                FontWeight          = FontWeights.Bold,
                Foreground          = WpfBrushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
            };

            badge.Child = text;
            LettersPanel.Children.Add(badge);
        }
    }

    private void ShowDirectText(string text)
    {
        LettersPanel.Visibility     = Visibility.Collapsed;
        TextScrollViewer.Visibility = Visibility.Visible;
        WaitingText.Visibility      = Visibility.Collapsed;
        DirectAnswerText.Text       = string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    private void ShowWaiting(string symbol)
    {
        LettersPanel.Visibility     = Visibility.Collapsed;
        TextScrollViewer.Visibility = Visibility.Collapsed;
        WaitingText.Visibility      = Visibility.Visible;
        WaitingText.Text            = symbol;
    }

    // ── Animations ─────────────────────────────────────────────────────────────

    private void FadeIn()
    {
        var anim = new DoubleAnimation(0, 0.92, TimeSpan.FromMilliseconds(180));
        BeginAnimation(OpacityProperty, anim);
    }

    private void FadeOut(bool instant = false)
    {
        if (instant)
        {
            Opacity = 0;
            return;
        }
        var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
        BeginAnimation(OpacityProperty, anim);
    }

    // ── Drag ───────────────────────────────────────────────────────────────────

    private void RootBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    // ── Close override: hide instead of destroy ────────────────────────────────

    protected override void OnClosing(CancelEventArgs e)
    {
        // Never destroy the overlay; just hide it
        e.Cancel = true;
        SetVisible(false);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private static System.Windows.Media.Color ParseHexToWpf(string hex)
    {
        try
        {
            var c = System.Drawing.ColorTranslator.FromHtml(hex);
            return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch
        {
            return System.Windows.Media.Colors.White;
        }
    }
}

