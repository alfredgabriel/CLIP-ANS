using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using QuizHelper.Models;
using WpfBrushes    = System.Windows.Media.Brushes;
using WpfBrush      = System.Windows.Media.Brush;
using WpfColor      = System.Windows.Media.Color;
using WpfColors     = System.Windows.Media.Colors;
using SolidBrush    = System.Windows.Media.SolidColorBrush;

namespace QuizHelper.Overlay;

/// <summary>
/// Always-on-top transparent overlay that shows the answer inside a single, invariant
/// 38x38 circle badge. Never changes shape.
/// - Single letters: large bold font.
/// - Multiple letters: displayed together (e.g. "ABD") with scaled-down font.
/// - Direct text answers: shows "T" and allows copying full text on right-click.
/// - Fixed color mode: solid blue background.
/// </summary>
public partial class OverlayWindow : Window
{
    public event Action<bool>? VisibilityChanged;

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
        VisibilityChanged?.Invoke(_visible);
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

    /// <summary>Updates opacity, color mode, and displays from updated AppConfig.</summary>
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
                SetBadge("···", 16, WpfColor.FromRgb(24, 24, 24), WpfBrushes.Cyan,
                    "CLIP-ANS: Consultando IA...\n(Arrastrar para mover • Ctrl+Shift+Espacio)");
                break;

            case AppStatus.Answered:
                if (state.IsDirectAnswer)
                {
                    ShowDirectAnswer(state.LastDirectAnswer, config);
                }
                else
                {
                    ShowMultipleChoice(state.LastAnswers, config);
                }
                break;

            case AppStatus.Error:
                SetBadge("!", 19, WpfColor.FromRgb(180, 20, 20), WpfBrushes.White,
                    $"CLIP-ANS Error: {state.LastErrorMessage}\n(Clic derecho para opciones)");
                break;

            default:
                SetBadge("—", 19, WpfColor.FromRgb(24, 24, 24), new SolidBrush(WpfColor.FromRgb(160, 160, 160)),
                    "CLIP-ANS: Esperando pregunta\n(Arrastrar para mover • Clic derecho: menú)");
                break;
        }
    }

    // ── Display modes ──────────────────────────────────────────────────────────

    private void ShowDirectAnswer(string text, AppConfig config)
    {
        // Remains circular, shows 'T' (for text answer), with full text in tooltip and right-click copy
        WpfColor blue = ParseHexToWpf(config.OverlayFixedColorHex);
        string tooltip = $"Respuesta de texto:\n{text}\n(Clic derecho: copiar texto • Arrastrar para mover)";

        SetBadge("T", 19, blue, WpfBrushes.White, tooltip);
    }

    private void ShowMultipleChoice(IReadOnlyList<char> letters, AppConfig config)
    {
        if (letters.Count == 0)
        {
            SetBadge("—", 19, WpfColor.FromRgb(24, 24, 24), new SolidBrush(WpfColor.FromRgb(160, 160, 160)),
                "CLIP-ANS: Sin opciones detectadas");
            return;
        }

        string lettersStr = string.Concat(letters);

        // Font size scales so multiple letters fit inside the 38px circle:
        // 1 letter: 19pt, 2 letters: 14pt, 3 letters: 11.5pt, 4+ letters: 9.5pt
        double fontSize = lettersStr.Length switch
        {
            1 => 19,
            2 => 14,
            3 => 11.5,
            _ => 9.5
        };

        WpfColor bgColor;
        WpfBrush textBrush;

        if (config.OverlayFixedColor)
        {
            bgColor   = ParseHexToWpf(config.OverlayFixedColorHex);
            textBrush = WpfBrushes.White;
        }
        else
        {
            // If single letter, use its assigned color; if multiple letters, use the primary letter's color
            string primaryKey = letters[0].ToString();
            string hex = config.ColorMap.TryGetValue(primaryKey, out var h) ? h : "#1E88E5";
            bgColor   = ParseHexToWpf(hex);
            textBrush = GetContrastingBrush(bgColor);
        }

        string tooltip = $"Respuesta: {lettersStr}\n(Clic derecho para copiar • Arrastrar para mover)";
        SetBadge(lettersStr, fontSize, bgColor, textBrush, tooltip);
    }

    private void SetBadge(string text, double fontSize, WpfColor bgColor, WpfBrush textBrush, string tooltip)
    {
        BadgeText.Text       = text;
        BadgeText.FontSize   = fontSize;
        BadgeText.Foreground = textBrush;
        CircleBadge.Background = new SolidBrush(bgColor);
        RootContainer.ToolTip  = tooltip;
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

    // ── Dragging & Context Menu ────────────────────────────────────────────────

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

    private void RootContainer_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var state = AppState.Instance;
        if (state.IsDirectAnswer && !string.IsNullOrWhiteSpace(state.LastDirectAnswer))
        {
            CopyAnswerMenuItem.IsEnabled = true;
            string preview = state.LastDirectAnswer.Length > 20
                ? state.LastDirectAnswer[..20] + "…"
                : state.LastDirectAnswer;
            CopyAnswerMenuItem.Header = $"📋 Copiar texto: \"{preview}\"";
        }
        else if (!state.IsDirectAnswer && state.LastAnswers.Count > 0)
        {
            CopyAnswerMenuItem.IsEnabled = true;
            CopyAnswerMenuItem.Header = $"📋 Copiar respuesta ({string.Join(",", state.LastAnswers)})";
        }
        else
        {
            CopyAnswerMenuItem.IsEnabled = false;
            CopyAnswerMenuItem.Header = "📋 Copiar (sin respuesta)";
        }
    }

    private void CopyAnswerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var state = AppState.Instance;
        string text = state.IsDirectAnswer
            ? state.LastDirectAnswer
            : string.Join(", ", state.LastAnswers);

        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch { }
        }
    }

    private void CloseOverlayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetVisible(false);
        VisibilityChanged?.Invoke(false);
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
                byte g = Convert.ToByte(hex[2..4], 16);
                byte b = Convert.ToByte(hex[4..6], 16);
                return WpfColor.FromArgb(a, r, g, b);
            }
        }
        catch { }
        return WpfColor.FromRgb(30, 136, 229); // Default blue
    }
}
