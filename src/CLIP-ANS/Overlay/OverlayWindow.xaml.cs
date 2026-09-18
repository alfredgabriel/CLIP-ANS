using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using QuizHelper.Models;
using WpfBrushes    = System.Windows.Media.Brushes;
using WpfBrush      = System.Windows.Media.Brush;
using WpfColor      = System.Windows.Media.Color;
using WpfColors     = System.Windows.Media.Colors;
using WpfFontFamily = System.Windows.Media.FontFamily;
using SolidBrush    = System.Windows.Media.SolidColorBrush;

namespace QuizHelper.Overlay;

/// <summary>
/// Always-on-top transparent overlay that shows the answer as a compact circle badge
/// with the letter (A, B, C, D...) and its configured color.
/// Can be dragged anywhere on screen and supports opacity adjustment.
/// </summary>
public partial class OverlayWindow : Window
{
    private bool _visible;
    private double _targetOpacity = 0.90;

    public OverlayWindow()
    {
        InitializeComponent();

        // Load configured opacity if available
        var config = AppState.Instance.Config;
        if (config != null && config.OverlayOpacity > 0)
        {
            _targetOpacity = Math.Clamp(config.OverlayOpacity, 0.10, 1.0);
        }

        // Listen to state changes
        AppState.Instance.PropertyChanged += OnStateChanged;

        // Position: top-right corner of the primary screen
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - 80;
        Top  = screen.Top   + 24;

        // Start hidden but initialized so Topmost stays active
        Show();
        FadeOut(instant: true);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Shows or hides the overlay.</summary>
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

    /// <summary>Dynamically updates overlay opacity and applies it immediately if visible.</summary>
    public void SetOpacity(double opacity)
    {
        _targetOpacity = Math.Clamp(opacity, 0.10, 1.0);
        if (_visible)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = _targetOpacity;
        }
    }

    /// <summary>Updates opacity and color configurations from updated AppConfig.</summary>
    public void UpdateFromConfig(AppConfig config)
    {
        SetOpacity(config.OverlayOpacity);
        if (_visible)
        {
            Dispatcher.Invoke(RefreshDisplay);
        }
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
                ShowWaiting("···", WpfColor.FromRgb(0, 230, 255), new SolidBrush(WpfColor.FromRgb(0, 230, 255)));
                break;

            case AppStatus.Answered:
                if (state.IsDirectAnswer)
                    ShowDirectText(state.LastDirectAnswer);
                else
                    ShowLetters(state.LastAnswers, config.ColorMap);
                break;

            case AppStatus.Error:
                ShowWaiting("!", WpfColor.FromRgb(255, 68, 68), new SolidBrush(WpfColor.FromRgb(255, 68, 68)));
                break;

            default:
                ShowWaiting("—", WpfColor.FromArgb(100, 255, 255, 255), new SolidBrush(WpfColor.FromRgb(170, 170, 170)));
                break;
        }
    }

    // ── Display modes ──────────────────────────────────────────────────────────

    private void ShowLetters(IReadOnlyList<char> letters, Dictionary<string, string> colorMap)
    {
        LettersPanel.Visibility = Visibility.Visible;
        TextPanel.Visibility    = Visibility.Collapsed;
        WaitingBadge.Visibility = Visibility.Collapsed;

        LettersPanel.Children.Clear();

        if (letters.Count == 0)
        {
            ShowWaiting("—");
            return;
        }

        foreach (var letter in letters)
        {
            var key   = letter.ToString();
            var hex   = colorMap.TryGetValue(key, out var h) ? h : "#FFFFFF";
            WpfColor color = ParseHexToWpf(hex);

            var badge = new Border
            {
                Width           = 38,
                Height          = 38,
                CornerRadius    = new CornerRadius(19),
                BorderBrush     = WpfBrushes.White,
                BorderThickness = new Thickness(2),
                Margin          = new Thickness(2, 0, 2, 0),
                Background      = new SolidBrush(color),
                Effect          = new DropShadowEffect
                {
                    BlurRadius  = 5,
                    ShadowDepth = 1,
                    Opacity     = 0.45,
                    Color       = WpfColors.Black,
                }
            };

            var text = new TextBlock
            {
                Text                = key,
                FontFamily          = new WpfFontFamily("Segoe UI, Consolas, Arial"),
                FontSize            = 19,
                FontWeight          = FontWeights.Bold,
                Foreground          = GetContrastingBrush(color),
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
        LettersPanel.Visibility = Visibility.Collapsed;
        TextPanel.Visibility    = Visibility.Visible;
        WaitingBadge.Visibility = Visibility.Collapsed;
        DirectAnswerText.Text   = string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    private void ShowWaiting(string symbol, WpfColor? borderColor = null, WpfBrush? textBrush = null)
    {
        LettersPanel.Visibility = Visibility.Collapsed;
        TextPanel.Visibility    = Visibility.Collapsed;
        WaitingBadge.Visibility = Visibility.Visible;
        WaitingText.Text        = symbol;

        WaitingBadge.BorderBrush = borderColor.HasValue
            ? new SolidBrush(borderColor.Value)
            : new SolidBrush(WpfColor.FromArgb(100, 255, 255, 255));

        WaitingText.Foreground = textBrush ?? WpfBrushes.White;
    }

    // ── Animations ─────────────────────────────────────────────────────────────

    private void FadeIn()
    {
        var anim = new DoubleAnimation(Opacity, _targetOpacity, TimeSpan.FromMilliseconds(150));
        BeginAnimation(OpacityProperty, anim);
    }

    private void FadeOut(bool instant = false)
    {
        if (instant)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            return;
        }
        var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(150));
        BeginAnimation(OpacityProperty, anim);
    }

    // ── Dragging ───────────────────────────────────────────────────────────────

    private void RootContainer_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static WpfBrush GetContrastingBrush(WpfColor bg)
    {
        // Perceived luminance: (299*R + 587*G + 114*B) / 1000
        double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B);
        return lum > 145 ? WpfBrushes.Black : WpfBrushes.White;
    }

    private static WpfColor ParseHexToWpf(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex[..2],  16);
                byte g = Convert.ToByte(hex[2..4], 16);
                byte b = Convert.ToByte(hex[4..6], 16);
                return WpfColor.FromRgb(r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = Convert.ToByte(hex[..2],  16);
                byte r = Convert.ToByte(hex[2..4], 16);
                byte g = Convert.ToByte(hex[4..6], 16);
                byte b = Convert.ToByte(hex[6..8], 16);
                return WpfColor.FromArgb(a, r, g, b);
            }
        }
        catch { }
        return WpfColors.White;
    }
}
