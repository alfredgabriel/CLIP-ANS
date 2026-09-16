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
    public event Action<bool>? PauseToggled;

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
                icon = BuildAnswerIcon(config);
                tip  = $"CLIP-ANS — Respuesta: {_state.LastAnswerDisplay}";
                break;

            case AppStatus.Error:
                icon = IconRenderer.CreateErrorIcon();
                tip  = $"CLIP-ANS — Error: {_state.LastErrorMessage}";
                break;

            case AppStatus.Paused:
                icon = IconRenderer.CreatePausedIcon();
                tip  = "CLIP-ANS — PAUSADO";
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

    private void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.BackColor = Color.Black;
        menu.ForeColor = Color.White;
        menu.Font      = new Font("Consolas", 9f, FontStyle.Regular);
        menu.Renderer  = new BrutalistMenuRenderer();

        var itemOpen = new ToolStripMenuItem("ABRIR")
        {
            ForeColor = Color.White,
        };
        itemOpen.Click += (_, _) => OpenRequested?.Invoke();

        var itemPause = new ToolStripMenuItem("PAUSAR DETECCIÓN")
        {
            ForeColor = Color.White,
        };
        itemPause.Click += (_, _) =>
        {
            _state.DetectionEnabled = !_state.DetectionEnabled;
            itemPause.Text = _state.DetectionEnabled ? "PAUSAR DETECCIÓN" : "REANUDAR DETECCIÓN";
            PauseToggled?.Invoke(!_state.DetectionEnabled);
        };

        var sep = new ToolStripSeparator();

        var itemExit = new ToolStripMenuItem("SALIR")
        {
            ForeColor = Color.FromArgb(255, 80, 80),
        };
        itemExit.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.AddRange([itemOpen, itemPause, sep, itemExit]);
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
