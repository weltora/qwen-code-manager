using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Procedural dark placeholder cover generated from the project name. No AI, no network.
/// </summary>
public static class CoverPlaceholder
{
    private static readonly (string Bg, string Accent)[] Palette =
    [
        ("#232A31", "#2E9E9E"), // graphite + teal
        ("#24272C", "#D9A441"), // slate + amber
        ("#1F272B", "#4A9D8C"),
        ("#2A2D33", "#7A8C99"),
        ("#262B33", "#5B8FA8"),
        ("#22262B", "#9C7A4F"),
    ];

    public static void Generate(string text, string targetPath, int width = 480, int height = 240)
    {
        var seed = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(text));
        var (bg, accent) = Palette[seed % Palette.Length];

        using var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        using (var brush = new SolidBrush(ColorTranslator.FromHtml(bg)))
        {
            g.FillRectangle(brush, 0, 0, width, height);
        }

        // Accent corner mark (small "QM" chip, no visual noise)
        var accentColor = ColorTranslator.FromHtml(accent);
        using var path = new GraphicsPath();
        path.AddRoundedRectangle(new Rectangle(width - 84, height - 84, 64, 64), 14);
        using (var fill = new SolidBrush(Color.FromArgb(190, accentColor)))
        {
            g.FillPath(fill, path);
        }

        var label = (text.Length > 2 ? text[..2] : text).ToUpperInvariant();
        using var labelFont = new Font("Segoe UI", 22f, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString(label, labelFont, Brushes.White, width - 84 + 14, height - 84 + 16);

        // Project initials, large, low-contrast
        var initials = Initials(text);
        using var font = new Font("Segoe UI", 64f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.FromArgb(70, Color.White));
        var size = g.MeasureString(initials, font);
        g.DrawString(initials, font, textBrush, 24, (height - size.Height) / 2);

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        bitmap.Save(targetPath, ImageFormat.Png);
    }

    private static string Initials(string text)
    {
        var parts = text.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length > 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        }

        return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    private static void AddRoundedRectangle(this GraphicsPath path, Rectangle bounds, int radius)
    {
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
    }
}
