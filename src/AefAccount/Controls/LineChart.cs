using System.Windows;
using System.Windows.Media;
using AefAccount.Localization;
using AefAccount.Models;

namespace AefAccount.Controls;

public sealed class LineChart : FrameworkElement
{
    public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
        nameof(Chart), typeof(ChartModel), typeof(LineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public ChartModel? Chart
    {
        get => (ChartModel?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    public LineChart()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var chart = Chart;
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 10 || height < 10) return;

        var muted = Brush("Brush.Text.Muted");
        var faint = Brush("Brush.Text.Faint");
        var accent = Brush("Brush.Accent");
        var line = Brush("Brush.Line.Soft");
        if (chart == null || chart.Points.Count == 0)
        {
            DrawCentered(drawingContext, L.T("chart_no_data"), width, height, muted, 11, Mono());
            return;
        }

        if (chart.Points.Count == 1)
        {
            var point = chart.Points[0];
            DrawCentered(drawingContext, point.Value.ToString("N0", AppCulture.Current), width, height / 2 - 6, Brush("Brush.Text"), 46, Display());
            DrawCentered(drawingContext, $"{chart.Label.ToUpperInvariant()} · {point.Label}", width, height / 2 + 36, muted, 11, Mono());
            DrawCentered(drawingContext, L.T("chart_need_more"), width, height / 2 + 58, faint, 10, Mono());
            return;
        }

        const double left = 56;
        const double right = 22;
        const double top = 24;
        const double bottom = 38;
        var values = chart.Points.Select(item => item.Value).ToList();
        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 0.0001)
        {
            min -= 1;
            max += 1;
        }
        else
        {
            var span = max - min;
            min -= span * 0.14;
            max += span * 0.14;
        }
        if (min < 0 && values.Min() >= 0) min = 0;

        double X(int index) => left + index * (width - left - right) / Math.Max(1, chart.Points.Count - 1);
        double Y(double value) => top + (max - value) / (max - min) * (height - top - bottom);

        var gridPen = new Pen(line, 1);
        for (var index = 0; index <= 4; index++)
        {
            var value = min + (max - min) * index / 4;
            var y = Math.Round(Y(value)) + 0.5;
            drawingContext.DrawLine(gridPen, new Point(left, y), new Point(width - right, y));
            var label = Math.Abs(value - Math.Round(value)) < 0.0001
                ? Math.Round(value).ToString("N0", AppCulture.Current)
                : (Math.Round(value * 10) / 10).ToString("N1", AppCulture.Current);
            DrawText(drawingContext, label, 10, faint, new Point(left - 10, y), TextAlignment.Right, VerticalAlignment.Center, Mono());
        }

        var linePen = new Pen(accent, 2) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat, LineJoin = PenLineJoin.Miter };
        for (var index = 1; index < chart.Points.Count; index++)
        {
            drawingContext.DrawLine(
                linePen,
                new Point(X(index - 1), Y(chart.Points[index - 1].Value)),
                new Point(X(index), Y(chart.Points[index].Value)));
        }

        var dotFill = Brush("Brush.Bg.Raised");
        var dotPen = new Pen(accent, 1.5);
        for (var index = 0; index < chart.Points.Count; index++)
        {
            var x = Math.Round(X(index)) - 2;
            var y = Math.Round(Y(chart.Points[index].Value)) - 2;
            var rect = new Rect(x, y, 4, 4);
            drawingContext.DrawRectangle(dotFill, dotPen, rect);
        }

        var step = Math.Max(1, (int)Math.Ceiling(chart.Points.Count / 8.0));
        for (var index = 0; index < chart.Points.Count; index++)
        {
            if (index != 0 && index != chart.Points.Count - 1 && index % step != 0) continue;
            DrawText(drawingContext, chart.Points[index].Label, 10, faint,
                new Point(X(index), height - 14), TextAlignment.Center, VerticalAlignment.Top, Mono());
        }
    }

    private Brush Brush(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;

    private FontFamily Mono() => TryFindResource("Font.Mono") as FontFamily ?? new FontFamily("Cascadia Mono");

    private FontFamily Display() => TryFindResource("Font.Display") as FontFamily ?? new FontFamily("Bahnschrift");

    private void DrawCentered(DrawingContext context, string text, double width, double centerY, Brush brush, double size, FontFamily family)
    {
        var formatted = new FormattedText(
            text,
            AppCulture.Current,
            FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            TextAlignment = TextAlignment.Center,
            MaxTextWidth = width - 20,
        };
        context.DrawText(formatted, new Point(0, centerY - formatted.Height / 2));
    }

    private void DrawText(DrawingContext context, string text, double size, Brush brush, Point point, TextAlignment alignment, VerticalAlignment vertical, FontFamily family)
    {
        var formatted = new FormattedText(
            text,
            AppCulture.Current,
            FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var x = alignment switch
        {
            TextAlignment.Right => point.X - formatted.Width,
            TextAlignment.Center => point.X - formatted.Width / 2,
            _ => point.X,
        };
        var y = vertical switch
        {
            VerticalAlignment.Center => point.Y - formatted.Height / 2,
            VerticalAlignment.Top => point.Y,
            _ => point.Y - formatted.Height,
        };
        context.DrawText(formatted, new Point(x, y));
    }
}
