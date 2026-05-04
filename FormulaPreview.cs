using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Math2Tex;

public sealed class FormulaPreview : FrameworkElement
{
    public static readonly DependencyProperty LatexProperty =
        DependencyProperty.Register(
            nameof(Latex),
            typeof(string),
            typeof(FormulaPreview),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Typeface FormulaTypeface = new("Cambria Math");
    private static readonly Typeface MonoTypeface = new("Consolas");
    private static readonly Pen BaselinePen = CreatePen(Color.FromRgb(217, 222, 231), 1);
    private static readonly Brush InkBrush = CreateBrush(Color.FromRgb(31, 41, 51));
    private static readonly Brush MutedBrush = CreateBrush(Color.FromRgb(102, 112, 133));
    private static readonly Brush AccentBrush = CreateBrush(Color.FromRgb(14, 147, 132));
    private static readonly Brush AmberBrush = CreateBrush(Color.FromRgb(183, 121, 31));

    public string Latex
    {
        get => (string)GetValue(LatexProperty);
        set => SetValue(LatexProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 20 || height < 20)
        {
            return;
        }

        drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        drawingContext.DrawLine(BaselinePen, new Point(28, height * 0.58), new Point(width - 28, height * 0.58));

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var normalized = string.IsNullOrWhiteSpace(Latex) ? @"E = mc^2" : Latex.Trim();
        var display = NormalizeForPreview(normalized);
        var fontSize = Math.Clamp(width / Math.Max(display.Length, 8) * 1.8, 26, 54);

        DrawText(drawingContext, display, FormulaTypeface, fontSize, InkBrush, new Point(32, height * 0.40), dpi);
        DrawText(drawingContext, "LaTeX preview", MonoTypeface, 13, MutedBrush, new Point(30, 22), dpi);

        var complexity = CountComplexity(normalized);
        DrawText(drawingContext, $"{complexity} tokens", MonoTypeface, 12, AccentBrush, new Point(width - 102, 22), dpi);

        if (normalized.Contains("\\frac", StringComparison.Ordinal))
        {
            DrawFractionHint(drawingContext, width, height, dpi);
        }
    }

    private static void DrawFractionHint(DrawingContext dc, double width, double height, double dpi)
    {
        var centerX = width - 118;
        var centerY = height - 72;
        dc.DrawRoundedRectangle(CreateBrush(Color.FromRgb(255, 249, 235)), null, new Rect(centerX - 50, centerY - 34, 100, 68), 6, 6);
        dc.DrawLine(CreatePen(Color.FromRgb(183, 121, 31), 2), new Point(centerX - 28, centerY), new Point(centerX + 28, centerY));
        DrawText(dc, "a", FormulaTypeface, 22, AmberBrush, new Point(centerX - 7, centerY - 29), dpi);
        DrawText(dc, "b", FormulaTypeface, 22, AmberBrush, new Point(centerX - 7, centerY + 3), dpi);
    }

    private static void DrawText(DrawingContext dc, string text, Typeface typeface, double size, Brush brush, Point point, double dpi)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush,
            dpi);

        dc.DrawText(formatted, point);
    }

    private static string NormalizeForPreview(string latex)
    {
        return latex
            .Replace(@"\alpha", "alpha", StringComparison.Ordinal)
            .Replace(@"\beta", "beta", StringComparison.Ordinal)
            .Replace(@"\gamma", "gamma", StringComparison.Ordinal)
            .Replace(@"\theta", "theta", StringComparison.Ordinal)
            .Replace(@"\lambda", "lambda", StringComparison.Ordinal)
            .Replace(@"\sqrt", "sqrt", StringComparison.Ordinal)
            .Replace(@"\frac", "frac", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal);
    }

    private static int CountComplexity(string latex)
    {
        var count = 0;
        foreach (var ch in latex)
        {
            if (!char.IsWhiteSpace(ch))
            {
                count++;
            }
        }

        return count;
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var pen = new Pen(CreateBrush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private static Brush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
