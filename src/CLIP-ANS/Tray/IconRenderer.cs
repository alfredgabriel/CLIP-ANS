using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using QuizHelper.Models;

namespace QuizHelper.Tray;

/// <summary>
/// Renders the tray icon as a sleek, professional vector-styled clipboard badge.
/// Supports:
///   - Single color (one answer)
///   - Blend (multiple answers → averaged RGB)
///   - Special states: idle (clean white outline on transparent), querying (cyan pulse), error (dim warning), paused (dark outline)
/// Uses direct PNG-in-ICO streaming for 100% crystal-clear 32-bit ARGB transparency with zero GDI handle leaks.
/// </summary>
public static class IconRenderer
{
    private const int Size = 32; // px

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Creates an icon for the idle/waiting state (crisp white outline on transparent background).</summary>
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

    /// <summary>Creates an icon for the querying state (vibrant cyan).</summary>
    public static Icon CreateQueryingIcon() =>
        RenderIcon(Color.FromArgb(0, 225, 255), Color.White, filled: true);

    /// <summary>Creates an icon for the error/timeout state (dark gray with white border).</summary>
    public static Icon CreateErrorIcon() =>
        RenderIcon(Color.FromArgb(80, 80, 80), Color.FromArgb(255, 100, 100), filled: true);

    /// <summary>Creates an icon for the paused state (dim gray outline).</summary>
    public static Icon CreatePausedIcon() =>
        RenderIcon(Color.Transparent, Color.FromArgb(120, 120, 120), filled: false);

    // ── Rendering ─────────────────────────────────────────────────────────────

    private static Icon RenderIcon(Color fillColor, Color outlineColor, bool filled)
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);

        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // 1. Clipboard main body (smooth rounded rectangle)
        var bodyRect = new RectangleF(4.5f, 6.5f, 23f, 22f);
        using (var bodyPath = CreateRoundedRectangle(bodyRect, 4f))
        {
            if (filled && fillColor != Color.Transparent)
            {
                using var fillBrush = new SolidBrush(fillColor);
                g.FillPath(fillBrush, bodyPath);
            }

            using var outlinePen = new Pen(outlineColor, 1.8f);
            g.DrawPath(outlinePen, bodyPath);
        }

        // 2. Top clip (rounded tab at center top)
        var clipRect = new RectangleF(10.5f, 2.5f, 11f, 6f);
        using (var clipPath = CreateRoundedRectangle(clipRect, 2f))
        {
            Color clipFill = filled && fillColor != Color.Transparent
                ? fillColor
                : Color.FromArgb(20, 24, 30);

            using var clipBrush = new SolidBrush(clipFill);
            g.FillPath(clipBrush, clipPath);

            using var clipPen = new Pen(outlineColor, 1.4f);
            g.DrawPath(clipPen, clipPath);
        }

        // 3. Central accent dot / check for idle or answer
        if (!filled)
        {
            // Subtle inner line on idle
            using var linePen = new Pen(Color.FromArgb(180, outlineColor), 1.2f);
            g.DrawLine(linePen, 9f, 14f, 23f, 14f);
            g.DrawLine(linePen, 9f, 18f, 19f, 18f);
        }

        return BitmapToIcon(bmp);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Converts a 32-bit ARGB Bitmap to a System.Drawing.Icon using standard PNG-in-ICO format.
    /// Preserves full subpixel alpha transparency and produces zero unmanaged handle leaks.
    /// </summary>
    private static Icon BitmapToIcon(Bitmap bmp)
    {
        using var pngStream = new System.IO.MemoryStream();
        bmp.Save(pngStream, ImageFormat.Png);
        byte[] pngBytes = pngStream.ToArray();

        using var icoStream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(icoStream);

        // ICO Header (6 bytes): reserved=0, type=1 (ICO), count=1
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);

        // Directory Entry (16 bytes)
        writer.Write((byte)(bmp.Width >= 256 ? 0 : bmp.Width));
        writer.Write((byte)(bmp.Height >= 256 ? 0 : bmp.Height));
        writer.Write((byte)0);  // colors
        writer.Write((byte)0);  // reserved
        writer.Write((ushort)1); // color planes
        writer.Write((ushort)32); // bits per pixel
        writer.Write((uint)pngBytes.Length); // payload byte length
        writer.Write((uint)22); // offset (6 + 16 = 22)

        // PNG payload
        writer.Write(pngBytes);
        writer.Flush();

        icoStream.Seek(0, System.IO.SeekOrigin.Begin);
        return new Icon(icoStream);
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
