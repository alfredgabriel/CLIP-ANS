using System.Drawing;
using System.Windows.Forms;
using QuizHelper.Models;

namespace QuizHelper.Tray;

/// <summary>
/// Manages the system tray icon (NotifyIcon) and its context menu.
/// Lives on the UI thread. Holds the icon lifecycle for the always-on pattern.
/// </summary>
public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AppState   _state;
    private Icon? _currentIcon;
    private bool _disposed;

    // Events raised to App.xaml.cs
    public event Action? OpenRequested;
    public event Action? ExitRequested;
    public event Action<AppConfig>? ConfigToggled;

    public TrayManager(AppState state)
    {
        _state = state;
        _notifyIcon = new NotifyIcon
        {
            Text    = "CLIP-ANS — Esperando pregunta",
            Visible = true,
        };

        SetIcon(IconRenderer.CreateIdleIcon());
        BuildContextMenu();

        // Left / double-click → open window
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenRequested?.Invoke();
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();

        // React to state changes
        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.Status) ||
                e.PropertyName == nameof(AppState.LastAnswers))
                UpdateIconFromState();
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates tray icon and tooltip for the current app state.</summary>
    public void UpdateIconFromState()
    {
        var config = _state.Config ?? new AppConfig();

        Icon icon;
        string tip;

        switch (_state.Status)
        {
            case AppStatus.Idle:
            case AppStatus.Watching:
                icon = IconRenderer.CreateIdleIcon();
                tip  = "CLIP-ANS — Esperando pregunta";
                break;

            case AppStatus.Querying:
                icon = IconRenderer.CreateQueryingIcon();
                tip  = "CLIP-ANS — Consultando IA...";
                break;

            case AppStatus.Answered:
                if (_state.IsDirectAnswer)
                {
                    // Open-ended answer → white icon + text in tooltip
                    icon = IconRenderer.CreateDirectAnswerIcon();
                    var answer = _state.LastDirectAnswer;
                    tip = ("CLIP-ANS: " + answer).Length > 63
                        ? ("CLIP-ANS: " + answer)[..63]
                        : "CLIP-ANS: " + answer;

                    // Show balloon tip only if notifications are enabled
                    if (config.ShowNotifications && !string.IsNullOrWhiteSpace(answer))
                    {
                        var balloon = answer.Length > 250 ? answer[..250] + "…" : answer;
                        _notifyIcon.ShowBalloonTip(4000, "CLIP-ANS — Respuesta", balloon, ToolTipIcon.Info);
                    }
                }
                else
                {
                    icon = BuildAnswerIcon(config);
                    tip  = $"CLIP-ANS — Respuesta: {_state.LastAnswerDisplay}";
                }
                break;

            case AppStatus.Error:
                icon = IconRenderer.CreateErrorIcon();
                tip  = $"CLIP-ANS — Error: {_state.LastErrorMessage}";
                break;

            case AppStatus.Paused:
                icon = IconRenderer.CreatePausedIcon();
                tip  = "CLIP-ANS — DESACTIVADO (Detección apagada)";
                break;

            default:
                icon = IconRenderer.CreateIdleIcon();
                tip  = "CLIP-ANS";
                break;
        }

        SetIcon(icon);
        _notifyIcon.Text = tip.Length > 63 ? tip[..63] : tip; // NotifyIcon limit
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Icon BuildAnswerIcon(AppConfig config)
    {
        var answers = _state.LastAnswers;
        if (answers.Count == 0) return IconRenderer.CreateIdleIcon();

        var colors = answers
            .Select(a =>
            {
                var key = a.ToString();
                var hex = config.ColorMap.TryGetValue(key, out var h) ? h : "#FFFFFF";
                return IconRenderer.ParseHexColor(hex);
            })
            .ToList();

        return colors.Count == 1
            ? IconRenderer.CreateAnswerIcon(colors[0])
            : IconRenderer.CreateMultiAnswerIcon(colors);
    }

    private void SetIcon(Icon icon)
    {
        _notifyIcon.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    private void ApplyTogglesAndSave(AppConfig cfg)
    {
        var bothDisabled = !cfg.DetectText && !cfg.DetectScreenshots;
        _state.DetectionEnabled = !bothDisabled;

        if (bothDisabled)
        {
            _state.Status = AppStatus.Paused;
        }
        else if (_state.Status == AppStatus.Paused)
        {
            _state.Status = AppStatus.Watching;
        }

        UpdateIconFromState();
        ConfigToggled?.Invoke(cfg);
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.BackColor = Color.Black;
        menu.ForeColor = Color.White;
        menu.Font      = new Font("Consolas", 9f, FontStyle.Regular);
        menu.Renderer  = new BrutalistMenuRenderer();

        var itemCopy = new ToolStripMenuItem("📋 COPIAR RESPUESTA")
        {
            ForeColor = Color.FromArgb(0, 230, 255),
            Font      = new Font("Consolas", 9f, FontStyle.Bold),
            Enabled   = false,
        };
        itemCopy.Click += (_, _) =>
        {
            var answerToCopy = _state.IsDirectAnswer
                ? _state.LastDirectAnswer
                : _state.LastAnswerDisplay;

            if (!string.IsNullOrWhiteSpace(answerToCopy) && answerToCopy != "—")
            {
                try
                {
                    Clipboard.SetText(answerToCopy);
                    if (_state.Config?.ShowNotifications ?? true)
                    {
                        _notifyIcon.ShowBalloonTip(2000, "CLIP-ANS", "✓ Respuesta copiada al portapapeles", ToolTipIcon.None);
                    }
                }
                catch { }
            }
        };

        var itemOpen = new ToolStripMenuItem("ABRIR")
        {
            ForeColor = Color.White,
        };
        itemOpen.Click += (_, _) => OpenRequested?.Invoke();

        var itemDetectText = new ToolStripMenuItem("DETECTAR TEXTO COPIADO")
        {
            CheckOnClick = true,
            ForeColor    = Color.White,
        };
        itemDetectText.Click += (_, _) =>
        {
            if (_state.Config is { } cfg)
            {
                cfg.DetectText = itemDetectText.Checked;
                ApplyTogglesAndSave(cfg);
            }
        };

        var itemDetectScreenshots = new ToolStripMenuItem("DETECTAR CAPTURAS (OCR)")
        {
            CheckOnClick = true,
            ForeColor    = Color.White,
        };
        itemDetectScreenshots.Click += (_, _) =>
        {
            if (_state.Config is { } cfg)
            {
                cfg.DetectScreenshots = itemDetectScreenshots.Checked;
                ApplyTogglesAndSave(cfg);
            }
        };

        var itemNotifications = new ToolStripMenuItem("NOTIFICACIONES DE WINDOWS")
        {
            CheckOnClick = true,
            ForeColor    = Color.White,
        };
        itemNotifications.Click += (_, _) =>
        {
            if (_state.Config is { } cfg)
            {
                cfg.ShowNotifications = itemNotifications.Checked;
                ApplyTogglesAndSave(cfg);
            }
        };

        var itemExit = new ToolStripMenuItem("SALIR")
        {
            ForeColor = Color.FromArgb(255, 80, 80),
        };
        itemExit.Click += (_, _) => ExitRequested?.Invoke();

        menu.Opening += (_, _) =>
        {
            var hasAnswer = (_state.IsDirectAnswer && !string.IsNullOrWhiteSpace(_state.LastDirectAnswer))
                         || (!_state.IsDirectAnswer && _state.LastAnswers.Count > 0);
            itemCopy.Enabled = hasAnswer;
            if (hasAnswer)
            {
                var preview = _state.IsDirectAnswer
                    ? (_state.LastDirectAnswer.Length > 18 ? _state.LastDirectAnswer[..18] + "…" : _state.LastDirectAnswer)
                    : string.Join(",", _state.LastAnswers);
                itemCopy.Text = $"📋 COPIAR RESPUESTA ({preview})";
            }
            else
            {
                itemCopy.Text = "📋 COPIAR RESPUESTA";
            }

            if (_state.Config is { } cfg)
            {
                itemDetectText.Checked        = cfg.DetectText;
                itemDetectScreenshots.Checked = cfg.DetectScreenshots;
                itemNotifications.Checked     = cfg.ShowNotifications;
            }
        };

        menu.Items.AddRange([
            itemCopy,
            new ToolStripSeparator(),
            itemOpen,
            new ToolStripSeparator(),
            itemDetectText,
            itemDetectScreenshots,
            itemNotifications,
            new ToolStripSeparator(),
            itemExit
        ]);

        _notifyIcon.ContextMenuStrip = menu;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
    }
}

/// <summary>
/// Custom renderer for the tray context menu — brutalist black/white style.
/// </summary>
internal sealed class BrutalistMenuRenderer : ToolStripProfessionalRenderer
{
    public BrutalistMenuRenderer() : base(new BrutalistColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rect  = new Rectangle(Point.Empty, e.Item.Size);
        var color = e.Item.Selected ? Color.White : Color.Black;
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, rect);
        e.Item.ForeColor = e.Item.Selected ? Color.Black : Color.White;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Color.Black);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Height / 2;
        using var pen = new Pen(Color.FromArgb(80, 80, 80), 1);
        e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y);
    }
}

internal sealed class BrutalistColorTable : ProfessionalColorTable
{
    public override Color MenuBorder              => Color.White;
    public override Color MenuItemBorder          => Color.Transparent;
    public override Color MenuItemSelected         => Color.White;
    public override Color MenuItemSelectedGradientBegin => Color.White;
    public override Color MenuItemSelectedGradientEnd   => Color.White;
    public override Color ToolStripDropDownBackground   => Color.Black;
    public override Color ImageMarginGradientBegin      => Color.Black;
    public override Color ImageMarginGradientMiddle     => Color.Black;
    public override Color ImageMarginGradientEnd        => Color.Black;
}
