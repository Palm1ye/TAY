using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace TAY.Controls;

public sealed class SparklineControl : Grid
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IEnumerable<double>), typeof(SparklineControl), new PropertyMetadata(null, OnGraphPropertyChanged));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(SparklineControl), new PropertyMetadata(null, OnGraphPropertyChanged));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(SparklineControl), new PropertyMetadata(null, OnGraphPropertyChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SparklineControl), new PropertyMetadata(double.NaN, OnGraphPropertyChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SparklineControl), new PropertyMetadata(double.NaN, OnGraphPropertyChanged));

    public static readonly DependencyProperty LineThicknessProperty =
        DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(SparklineControl), new PropertyMetadata(1.7d, OnGraphPropertyChanged));

    private readonly Polygon _area = new();
    private readonly Polyline _line = new()
    {
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round
    };

    public SparklineControl()
    {
        IsHitTestVisible = false;
        Children.Add(_area);
        Children.Add(_line);
        SizeChanged += (_, _) => UpdateGraph();
    }

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double LineThickness
    {
        get => (double)GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    private static void OnGraphPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SparklineControl)d).UpdateGraph();
    }

    private void UpdateGraph()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var values = Values?.ToList() ?? new List<double>();

        _line.Stroke = Stroke;
        _line.StrokeThickness = LineThickness;
        _area.Fill = Fill;

        if (width <= 1 || height <= 1 || values.Count == 0)
        {
            _line.Points = new PointCollection();
            _area.Points = new PointCollection();
            return;
        }

        if (values.Count == 1)
        {
            values.Add(values[0]);
        }

        var min = double.IsNaN(Minimum) ? values.Min() : Minimum;
        var max = double.IsNaN(Maximum) ? values.Max() : Maximum;
        if (Math.Abs(max - min) < 0.01)
        {
            min -= 1;
            max += 1;
        }
        else if (double.IsNaN(Minimum) || double.IsNaN(Maximum))
        {
            var padding = Math.Max((max - min) * 0.38, 2.0);
            min -= padding;
            max += padding;
        }

        var points = new PointCollection();
        var top = Math.Max(2, LineThickness);
        var bottom = Math.Max(top + 1, height - Math.Max(3, LineThickness + 1));
        var step = width / (values.Count - 1);

        for (var i = 0; i < values.Count; i++)
        {
            var normalized = Math.Clamp((values[i] - min) / (max - min), 0, 1);
            var x = i * step;
            var y = bottom - normalized * (bottom - top);
            points.Add(new Point(x, y));
        }

        var areaPoints = new PointCollection { new(0, bottom) };
        foreach (var point in points)
        {
            areaPoints.Add(point);
        }
        areaPoints.Add(new Point(width, bottom));

        _line.Points = points;
        _area.Points = areaPoints;
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
    }
}
