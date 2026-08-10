using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace AmphetamineNet.Services;

/// <summary>
/// Renders the menu-bar pill icon and selection dots
/// </summary>
public static class TrayIconPainter
{
    /// <summary>
    /// Max menu-bar icon size in pixels (22pt at 3x for sharp Retina scaling)
    /// </summary>
    public const int MenuBarIconPixels = 66;

    /// <summary>
    /// Pill interior and highlight color
    /// </summary>
    private static readonly SKColor White = new(255, 255, 255);

    /// <summary>
    /// Pill outline and seam color
    /// </summary>
    private static readonly SKColor Black = new(29, 29, 31);

    /// <summary>
    /// Bright sun color (macOS system yellow)
    /// </summary>
    private static readonly SKColor SunYellow = new(255, 204, 0);

    /// <summary>
    /// Inactive icon background (macOS system gray)
    /// </summary>
    private static readonly SKColor BgIdle = new(242, 242, 247);

    /// <summary>
    /// Timed-session background (macOS system green)
    /// </summary>
    private static readonly SKColor BgTimed = new(52, 199, 89);

    /// <summary>
    /// Indefinite-session background (soft coral, macOS-friendly)
    /// </summary>
    private static readonly SKColor BgIndefinite = new(232, 117, 117);

    /// <summary>
    /// Menu selection indicator color (macOS system green)
    /// </summary>
    private static readonly SKColor SelectionGreen = new(52, 199, 89);

    /// <summary>
    /// Builds a tray icon for the current session state
    /// </summary>
    /// <param name="active">Whether a keep-awake session is running</param>
    /// <param name="timed">Whether the session uses a countdown timer</param>
    /// <param name="closedLid">Whether the closed-lid modifier is enabled</param>
    /// <param name="displayAwake">Whether the display-awake modifier is enabled</param>
    /// <returns>Tray window icon</returns>
    public static WindowIcon CreateTrayIcon(bool active, bool timed, bool closedLid, bool displayAwake)
    {
        using var bitmap = PaintIcon(MenuBarIconPixels, active, timed, closedLid, displayAwake);
        using var stream = new MemoryStream();
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Position = 0;
        return new WindowIcon(stream);
    }

    /// <summary>
    /// Builds a green selection indicator for tray menu items
    /// </summary>
    /// <param name="selected">Whether the menu item is selected</param>
    /// <returns>Bitmap used as a menu item icon</returns>
    public static Bitmap CreateSelectionDot(bool selected)
    {
        const int size = 16;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (selected)
        {
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = SelectionGreen,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawCircle(size / 2f, size / 2f, size * 0.34f, paint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    /// <summary>
    /// Draws the tray icon for the given visual state
    /// </summary>
    /// <param name="size">Output bitmap size in pixels</param>
    /// <param name="active">Whether a keep-awake session is running</param>
    /// <param name="timed">Whether the session uses a countdown timer</param>
    /// <param name="closedLid">Whether the closed-lid modifier is enabled</param>
    /// <param name="displayAwake">Whether the display-awake modifier is enabled</param>
    /// <returns>Rendered icon bitmap</returns>
    private static SKBitmap PaintIcon(int size, bool active, bool timed, bool closedLid, bool displayAwake)
    {
        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        var background = !active ? BgIdle : timed ? BgTimed : BgIndefinite;
        canvas.Clear(background);

        // Thinner outline; pill nearly fills the tile.
        var stroke = Math.Max(1.6f, size * 0.038f);
        var seam = Math.Max(1.8f, size * 0.042f);

        var pillW = size * 0.86f;
        var pillH = size * 0.96f;
        var left = (size - pillW) / 2f;
        var top = (size - pillH) / 2f;
        var right = left + pillW;
        var bottom = top + pillH;
        var midY = (top + bottom) / 2f;
        var cx = (left + right) / 2f;
        var rect = new SKRect(left, top, right, bottom);
        var radius = pillW / 2f;

        using var pillPath = new SKPath();
        pillPath.AddRoundRect(rect, radius, radius);

        // Top half is always white.
        using (var topFill = new SKPaint { IsAntialias = true, Color = White, Style = SKPaintStyle.Fill })
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, size, midY));
            canvas.DrawPath(pillPath, topFill);
            canvas.Restore();
        }

        // Bottom half: white by default, black with a closed-lid mark when enabled.
        if (closedLid)
        {
            using (var bottomFill = new SKPaint { IsAntialias = true, Color = Black, Style = SKPaintStyle.Fill })
            {
                canvas.Save();
                canvas.ClipRect(new SKRect(0, midY, size, size));
                canvas.DrawPath(pillPath, bottomFill);
                canvas.Restore();
            }

            DrawClosedLidMark(
                canvas,
                cx,
                (midY + bottom) / 2f,
                pillW * 0.50f,
                (bottom - midY) * 0.48f,
                White,
                Math.Max(2.8f, size * 0.09f));
        }
        else
        {
            using var bottomFill = new SKPaint { IsAntialias = true, Color = White, Style = SKPaintStyle.Fill };
            canvas.Save();
            canvas.ClipRect(new SKRect(0, midY, size, size));
            canvas.DrawPath(pillPath, bottomFill);
            canvas.Restore();
        }

        // Bright yellow sun in the top half when display-awake is on.
        if (displayAwake)
        {
            var topHalfHeight = midY - top;
            // Keep the whole sun (core + rays) inside the top chamber.
            var maxOuter = Math.Min(pillW * 0.42f, topHalfHeight * 0.42f);
            var sunRadius = maxOuter / 1.95f;
            canvas.Save();
            canvas.ClipRect(new SKRect(left, top, right, midY));
            canvas.ClipPath(pillPath, SKClipOperation.Intersect, antialias: true);
            DrawSun(
                canvas,
                cx,
                (top + midY) / 2f,
                sunRadius,
                SunYellow,
                Math.Max(2.0f, size * 0.055f),
                Math.Max(1.2f, size * 0.028f));
            canvas.Restore();
        }

        // Black outline.
        using (var outline = new SKPaint
        {
            IsAntialias = true,
            Color = Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = stroke,
            StrokeJoin = SKStrokeJoin.Round,
        })
        {
            canvas.DrawPath(pillPath, outline);
        }

        // Black seam across the middle.
        using (var seamPaint = new SKPaint
        {
            IsAntialias = true,
            Color = Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = seam,
            StrokeCap = SKStrokeCap.Butt,
        })
        {
            var inset = stroke * 0.4f;
            canvas.DrawLine(left + inset, midY, right - inset, midY, seamPaint);
        }

        return bitmap;
    }

    /// <summary>
    /// Draws a closed-lid mark: two parallel lines joined on the right
    /// </summary>
    /// <param name="canvas">Target canvas</param>
    /// <param name="cx">Center X</param>
    /// <param name="cy">Center Y</param>
    /// <param name="width">Mark width</param>
    /// <param name="height">Mark height</param>
    /// <param name="color">Stroke color</param>
    /// <param name="stroke">Stroke width</param>
    private static void DrawClosedLidMark(
        SKCanvas canvas,
        float cx,
        float cy,
        float width,
        float height,
        SKColor color,
        float stroke)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        var left = cx - width / 2f;
        var right = cx + width / 2f;
        var top = cy - height / 2f;
        var bottom = cy + height / 2f;

        using var path = new SKPath();
        // Two parallel horizontals joined only on the right (closed Mac lid silhouette).
        path.MoveTo(left, top);
        path.LineTo(right, top);
        path.MoveTo(right, top);
        path.LineTo(right, bottom);
        path.MoveTo(right, bottom);
        path.LineTo(left, bottom);
        canvas.DrawPath(path, paint);
    }

    /// <summary>
    /// Draws a bright sun glyph in the upper pill half
    /// </summary>
    /// <param name="canvas">Target canvas</param>
    /// <param name="cx">Center X</param>
    /// <param name="cy">Center Y</param>
    /// <param name="radius">Sun core radius</param>
    /// <param name="color">Sun color</param>
    /// <param name="rayStroke">Ray stroke width</param>
    /// <param name="outlineStroke">Black outline width</param>
    private static void DrawSun(
        SKCanvas canvas,
        float cx,
        float cy,
        float radius,
        SKColor color,
        float rayStroke,
        float outlineStroke)
    {
        var inner = radius * 1.22f;
        var outer = radius * 1.90f;

        using var rayFill = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = rayStroke,
            StrokeCap = SKStrokeCap.Round,
        };
        using var rayOutline = new SKPaint
        {
            IsAntialias = true,
            Color = Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = rayStroke + outlineStroke * 2f,
            StrokeCap = SKStrokeCap.Round,
        };

        for (var i = 0; i < 8; i++)
        {
            var angle = i * (MathF.PI / 4f);
            var dx = MathF.Cos(angle);
            var dy = MathF.Sin(angle);
            var x0 = cx + dx * inner;
            var y0 = cy + dy * inner;
            var x1 = cx + dx * outer;
            var y1 = cy + dy * outer;
            canvas.DrawLine(x0, y0, x1, y1, rayOutline);
            canvas.DrawLine(x0, y0, x1, y1, rayFill);
        }

        using (var coreOutline = new SKPaint
        {
            IsAntialias = true,
            Color = Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = outlineStroke,
        })
        using (var coreFill = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill })
        {
            canvas.DrawCircle(cx, cy, radius, coreFill);
            canvas.DrawCircle(cx, cy, radius, coreOutline);
        }
    }
}
