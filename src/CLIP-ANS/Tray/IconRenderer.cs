using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using QuizHelper.Models;

namespace QuizHelper.Tray;

/// <summary>
/// Renders system tray icons as clean, minimalist geometric emblems.
/// Supports:
///   - Single answer: solid color circle with crisp border.
///   - Multiple answers (e.g. A, B, D): circle divided into N equal sectors (e.g. 3 parts of 120°),
///     each part filled with its corresponding answer color.
///   - Special states: Idle (clean outline + center dot), Querying (cyan), Error (red), Paused (dashed).
/// Uses direct PNG-in-ICO binary streaming for 100% crystal-clear 32-bit ARGB transparency with zero GDI leaks.
/// </summary>
public static class IconRenderer
{
    private const int Size = 32; // px

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Creates an icon for the idle/waiting state (minimalist white ring with center dot).</summary>
    public static Icon CreateIdleIcon()
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        ConfigureGraphics(g);

        float margin = 3.5f;
        var rect = new RectangleF(margin, margin, Size - 2 * margin, Size - 2 * margin);

        // Crisp white ring
        using var pen = new Pen(Color.White, 1.8f);
        g.DrawEllipse(pen, rect);

        // Center dot
        float dotR = 2.5f;
        using var dotBrush = new SolidBrush(Color.White);
        g.FillEllipse(dotBrush, Size / 2f - dotR, Size / 2f - dotR, dotR * 2, dotR * 2);

        return BitmapToIcon(bmp);
    }

    /// <summary>Creates an icon for a single answer letter.</summary>
    public static Icon CreateAnswerIcon(Color answerColor)
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        ConfigureGraphics(g);

        float margin = 3f;
        var rect = new RectangleF(margin, margin, Size - 2 * margin, Size - 2 * margin);

        using var brush = new SolidBrush(answerColor);
        g.FillEllipse(brush, rect);

        using var borderPen = new Pen(Color.White, 1.8f);
        g.DrawEllipse(borderPen, rect);

        return BitmapToIcon(bmp);
    }

    /// <summary>
    /// Creates an icon divided into N equal circular sectors (pie slices),
    /// each sector painted with its corresponding answer color (e.g. A, B, D divided in 3 parts).
    /// </summary>
    public static Icon CreateMultiAnswerIcon(IReadOnlyList<Color> colors)
    {
        if (colors.Count == 0) return CreateIdleIcon();
        if (colors.Count == 1) return CreateAnswerIcon(colors[0]);

        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        ConfigureGraphics(g);

        float margin = 3f;
        var rect = new RectangleF(margin, margin, Size - 2 * margin, Size - 2 * margin);
        float cx = Size / 2f;
        float cy = Size / 2f;
        float radius = (Size - 2 * margin) / 2f;

        int count = colors.Count;
        float sweepAngle = 360f / count;

        // 1. Draw each sector starting from top (-90 degrees)
        for (int i = 0; i < count; i++)
        {
            float startAngle = -90f + (i * sweepAngle);
            using var brush = new SolidBrush(colors[i]);
            g.FillPie(brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
        }

        // 2. Draw clean separating lines between sectors
        using var sepPen = new Pen(Color.FromArgb(220, 20, 20, 20), 1.5f);
        for (int i = 0; i < count; i++)
        {
            float angleDeg = -90f + (i * sweepAngle);
            float angleRad = (float)(angleDeg * Math.PI / 180.0);
            float x2 = cx + radius * (float)Math.Cos(angleRad);
            float y2 = cy + radius * (float)Math.Sin(angleRad);
            g.DrawLine(sepPen, cx, cy, x2, y2);
        }

        // 3. Crisp outer border
        using var borderPen = new Pen(Color.White, 1.8f);
        g.DrawEllipse(borderPen, rect);

        return BitmapToIcon(bmp);
    }

    /// <summary>Creates an icon for the querying state (vibrant cyan circle).</summary>
    public static Icon CreateQueryingIcon()
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        ConfigureGraphics(g);

        float margin = 3f;
        var rect = new RectangleF(margin, margin, Size - 2 * margin, Size - 2 * margin);

        using var brush = new SolidBrush(Color.FromArgb(0, 230, 255));
        g.FillEllipse(brush, rect);

        using var borderPen = new Pen(Color.White, 1.8f);
        g.DrawEllipse(borderPen, rect);

        return BitmapToIcon(bmp);
    }

    /// <summary>Creates an icon for the error/timeout state (vibrant red circle).</summary>
    public static Icon CreateErrorIcon()
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        ConfigureGraphics(g);

        float margin = 3f;
        var rect = new RectangleF(margin, margin, Size - 2 * margin, Size - 2 * margin);

        using var brush = new SolidBrush(Color.FromArgb(235, 60, 60));
        g.FillEllipse(brush, rect);

        using var borderPen = new Pen(Color.White, 1.8f);
        g.DrawEllipse(borderPen, rect);

        return BitmapToIcon(bmp);
    }

    /// <summary>Creates an icon for the paused state (dashed dim gray outline).</summary>
    public static Icon CreatePausedIcon()
    {
        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);
        ConfigureGraphics(g);

        float margin = 3.5f;
        var rect = new RectangleF(margin, margin, Size - 2 * margin, Size - 2 * margin);

        using var pen = new Pen(Color.FromArgb(130, 130, 130), 1.8f)
        {
            DashStyle = DashStyle.Dash
        };
        g.DrawEllipse(pen, rect);

        return BitmapToIcon(bmp);
    }

    // ── Helper methods ────────────────────────────────────────────────────────

    private static void ConfigureGraphics(Graphics g)
    {
        g.SmoothingMode      = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode    = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);
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
        writer.Write((byte)0);   // colors
        writer.Write((byte)0);   // reserved
        writer.Write((ushort)1);  // color planes
        writer.Write((ushort)32); // bits per pixel
        writer.Write((uint)pngBytes.Length); // payload byte length
        writer.Write((uint)22);  // offset (6 + 16 = 22)

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
