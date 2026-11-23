using System.Windows;
using System.Windows.Media;

namespace AefAccount.Controls;

public sealed class RarityChevrons : FrameworkElement
{
    public static readonly DependencyProperty RarityProperty = DependencyProperty.Register(
        nameof(Rarity), typeof(int), typeof(RarityChevrons),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Rarity
    {
        get => (int)GetValue(RarityProperty);
        set => SetValue(RarityProperty, value);
    }

    private const int Count = 6;
    private const double ChevronWidth = 6.2;
    private const double ChevronHeight = 9;
    private const double Gap = 3;

    public RarityChevrons()
    {
        Height = ChevronHeight;
        Width = Count * ChevronWidth + (Count - 1) * Gap;
        SnapsToDevicePixels = true;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var filled = TryFindResource(Rarity >= 6 ? "Brush.Rarity.6" : Rarity == 5 ? "Brush.Rarity.5" : "Brush.Rarity.4") as Brush
                     ?? Brushes.Gainsboro;
        var empty = TryFindResource("Brush.Line.Strong") as Brush ?? Brushes.DimGray;

        var geometry = BuildChevron();
        geometry.Freeze();

        for (var index = 0; index < Count; index++)
        {
            var brush = index < Rarity ? filled : empty;
            drawingContext.PushTransform(new TranslateTransform(index * (ChevronWidth + Gap), 0));
            drawingContext.DrawGeometry(brush, null, geometry);
            drawingContext.Pop();
        }
    }

    private static StreamGeometry BuildChevron()
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(0, 0), true, true);
        context.LineTo(new Point(3.4, ChevronHeight / 2), true, false);
        context.LineTo(new Point(0, ChevronHeight), true, false);
        context.LineTo(new Point(1.9, ChevronHeight), true, false);
        context.LineTo(new Point(ChevronWidth, ChevronHeight / 2), true, false);
        context.LineTo(new Point(1.9, 0), true, false);
        return geometry;
    }
}
