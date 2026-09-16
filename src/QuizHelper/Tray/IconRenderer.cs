using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using QuizHelper.Models;

namespace QuizHelper.Tray;

/// <summary>
/// Renders the tray icon as a geometric cloud shape in a given color.
/// Supports:
///   - Single color (one answer)
///   - Blend (multiple answers → averaged RGB)
///   - Special states: idle (white outline), querying (cyan pulse), error (gray + border)
/// </summary>
public static class IconRenderer
{
    private const int Size = 32; // px

    // Cloud shape as a polygon approximation (normalized 0-1, scaled to Size)
    // Points describe a cloud outline going clockwise
    private static readonly PointF[] CloudPoints =
    [
        // Bottom-left foot
        new(0.05f, 0.85f), new(0.05f, 0.95f), new(0.95f, 0.95f), new(0.95f, 0.75f),
        // Right bump
        new(0.88f, 0.65f), new(0.92f, 0.55f), new(0.88f, 0.42f), new(0.75f, 0.38f),
        // Top-right bump (big)
        new(0.80f, 0.28f), new(0.78f, 0.18f), new(0.68f, 0.10f), new(0.55f, 0.10f),
        new(0.44f, 0.14f), new(0.38f, 0.22f),
        // Top-left bump (small)
        new(0.30f, 0.16f), new(0.20f, 0.16f), new(0.12f, 0.22f), new(0.08f, 0.32f),
        new(0.10f, 0.42f),
        // Left side back to bottom
        new(0.05f, 0.50f), new(0.03f, 0.62f), new(0.05f, 0.75f),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Creates an icon for the idle/waiting state (white outline on transparent).</summary>
    public static Icon CreateIdleIcon() =>
        RenderIcon(Color.Transparent, Color.White, filled: false);

    /// <summary>Creates an icon for a single answer letter.</summary>
    public static Icon CreateAnswerIcon(Color answerColor) =>
        RenderIcon(answerColor, Color.White, filled: true);

    /// <summary>
    /// Creates an icon for multiple answers using color blending.
    /// Blends R, G, B channels proportionally.
    /// </summary>
    public static Icon CreateBlendIcon(IReadOnlyList<Color> colors)
    {
        if (colors.Count == 0) return CreateIdleIcon();
        if (colors.Count == 1) return CreateAnswerIcon(colors[0]);

        int r = (int)colors.Average(c => c.R);
        int g = (int)colors.Average(c => c.G);
        int b = (int)colors.Average(c => c.B);
        return CreateAnswerIcon(Color.FromArgb(255, r, g, b));
    }

    /// <summary>Creates an icon for the querying state (cyan).</summary>
    public static Icon CreateQueryingIcon() =>
        RenderIcon(Color.FromArgb(0, 220, 220), Color.White, filled: true);

    /// <summary>Creates an icon for the error/timeout state (dark gray with white border).</summary>
    public static Icon CreateErrorIcon() =>
        RenderIcon(Color.FromArgb(60, 60, 60), Color.White, filled: true);

    /// <summary>Creates an icon for the paused state (dark gray outline).</summary>
    public static Icon CreatePausedIcon() =>
        RenderIcon(Color.Transparent, Color.FromArgb(120, 120, 120), filled: false);

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static Icon RenderIcon(Color fillColor, Color outlineColor, bool filled)
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);

        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.Clear(Color.Transparent);

        // Scale points to bitmap size
        var pts = ScalePoints(CloudPoints, Size, Size);

        // Fill
        if (filled && fillColor != Color.Transparent)
        {
            using var fill = new SolidBrush(fillColor);
            g.FillPolygon(fill, pts);
        }

        // Outline (always drawn)
        using var pen = new Pen(outlineColor, 1.5f);
        g.DrawPolygon(pen, pts);

        return BitmapToIcon(bmp);
    }

    private static PointF[] ScalePoints(PointF[] normalized, int width, int height) =>
        normalized.Select(p => new PointF(p.X * width, p.Y * height)).ToArray();

    /// <summary>Converts a Bitmap to a System.Drawing.Icon.</summary>
    private static Icon BitmapToIcon(Bitmap bmp)
    {
        using var ms = new System.IO.MemoryStream();
        // Write ICO header manually for a single 32x32 image
        bmp.Save(ms, ImageFormat.Png);
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        // Use a handle-based approach for clean conversion
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    /// <summary>
    /// Parses a hex color string (#RRGGBB or #AARRGGBB) to a System.Drawing.Color.
    /// Returns White on failure.
    /// </summary>
    public static Color ParseHexColor(string hex)
    {
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return Color.White;
        }
    }
}
