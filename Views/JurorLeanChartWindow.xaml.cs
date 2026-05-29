using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Verdict.Models;

namespace Verdict.Views;

public partial class JurorLeanChartWindow : Window
{
    // Distinct colors for up to 14 jurors (12 + 2 alternates)
    private static readonly Brush[] JurorColors = new[]
    {
        Brushes.DodgerBlue, Brushes.Crimson, Brushes.DarkGreen, Brushes.DarkOrange,
        Brushes.MediumPurple, Brushes.SaddleBrown, Brushes.DeepPink, Brushes.Teal,
        Brushes.Goldenrod, Brushes.SlateBlue, Brushes.Firebrick, Brushes.SeaGreen,
        Brushes.DarkCyan, Brushes.IndianRed
    };

    private readonly List<Agent> _jurors;

    public JurorLeanChartWindow(IEnumerable<Agent> jurors)
    {
        InitializeComponent();
        _jurors = jurors.Where(j => j.IsOccupied && j.CanVote).ToList();
        Loaded += (_, _) => DrawChart();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        if (_jurors.Count == 0) return;

        // Collect all unique phase labels across all jurors, preserving order
        var allPhases = new List<string>();
        var seen = new HashSet<string>();
        foreach (var j in _jurors)
        {
            foreach (var snap in j.OpinionHistory)
            {
                if (seen.Add(snap.Phase))
                    allPhases.Add(snap.Phase);
            }
        }

        if (allPhases.Count == 0)
        {
            var noData = new TextBlock
            {
                Text = "No opinion history recorded yet. Run a trial or deliberation to collect data.",
                FontSize = 13, Foreground = Brushes.Gray
            };
            Canvas.SetLeft(noData, 20);
            Canvas.SetTop(noData, 20);
            ChartCanvas.Children.Add(noData);
            return;
        }

        double chartWidth = ChartCanvas.ActualWidth > 0 ? ChartCanvas.ActualWidth - 40 : 760;
        double chartHeight = ChartCanvas.ActualHeight > 0 ? ChartCanvas.ActualHeight - 40 : 400;

        double marginLeft = 50;
        double marginBottom = 30;
        double plotWidth = chartWidth - marginLeft - 20;
        double plotHeight = chartHeight - marginBottom - 10;

        int phaseCount = allPhases.Count;
        double xStep = phaseCount > 1 ? plotWidth / (phaseCount - 1) : plotWidth / 2;

        // ── Y-axis labels and gridlines ──
        for (int pct = 0; pct <= 100; pct += 20)
        {
            double y = marginLeft > 0 ? marginLeft - 5 : 45;
            double val = pct / 100.0;
            double yPos = 10 + plotHeight - (val * plotHeight);

            // Gridline
            var gridLine = new Line
            {
                X1 = marginLeft, Y1 = yPos,
                X2 = marginLeft + plotWidth, Y2 = yPos,
                Stroke = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                StrokeThickness = 0.5
            };
            ChartCanvas.Children.Add(gridLine);

            // Label
            var label = new TextBlock
            {
                Text = pct == 100 ? "1.0 Prosecution" : pct == 0 ? "0.0 Defense" : $"0.{pct / 10}",
                FontSize = 10, Foreground = Brushes.Gray
            };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, yPos - 7);
            ChartCanvas.Children.Add(label);
        }

        // ── Threshold lines ──
        AddThresholdLine(plotHeight, 0.85, "Criminal: guilty > 0.85", Colors.OrangeRed);
        AddThresholdLine(plotHeight, 0.50, "Civil: liable > 0.50", Colors.DarkGray);

        // ── X-axis labels ──
        for (int i = 0; i < allPhases.Count; i++)
        {
            double xPos = marginLeft + (i * xStep);
            var label = new TextBlock
            {
                Text = TruncateLabel(allPhases[i], 10),
                FontSize = 9, Foreground = Brushes.Gray,
                RenderTransform = new RotateTransform(-30),
                RenderTransformOrigin = new Point(0, 0.5)
            };
            Canvas.SetLeft(label, xPos - 15);
            Canvas.SetTop(label, 10 + plotHeight + 8);
            ChartCanvas.Children.Add(label);
        }

        // ── Juror lines ──
        var legendItems = new List<LegendEntry>();
        for (int ji = 0; ji < _jurors.Count; ji++)
        {
            var juror = _jurors[ji];
            var brush = JurorColors[ji % JurorColors.Length];
            var history = juror.OpinionHistory;

            if (history.Count == 0) continue;

            var points = new PointCollection();
            for (int si = 0; si < history.Count; si++)
            {
                int phaseIdx = allPhases.IndexOf(history[si].Phase);
                if (phaseIdx < 0) phaseIdx = si; // fallback to sequential

                double x = marginLeft + (phaseIdx * xStep);
                double y = 10 + plotHeight - (history[si].VerdictLean * plotHeight);
                points.Add(new Point(x, y));
            }

            if (points.Count >= 2)
            {
                var polyline = new Polyline
                {
                    Points = points,
                    Stroke = brush,
                    StrokeThickness = 2.5,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                };
                ChartCanvas.Children.Add(polyline);

                // Add a dot at each data point
                foreach (var pt in points)
                {
                    var dot = new Ellipse
                    {
                        Width = 5, Height = 5,
                        Fill = brush,
                        Stroke = Brushes.White,
                        StrokeThickness = 1
                    };
                    Canvas.SetLeft(dot, pt.X - 2.5);
                    Canvas.SetTop(dot, pt.Y - 2.5);
                    ChartCanvas.Children.Add(dot);
                }

                // Label at the last point
                var nameLabel = new TextBlock
                {
                    Text = juror.Name.Split(' ').LastOrDefault() ?? juror.Name,
                    FontSize = 9, Foreground = brush, FontWeight = FontWeights.SemiBold
                };
                var lastPt = points.Last();
                Canvas.SetLeft(nameLabel, lastPt.X + 4);
                Canvas.SetTop(nameLabel, lastPt.Y - 8);
                ChartCanvas.Children.Add(nameLabel);
            }

            legendItems.Add(new LegendEntry { Name = juror.Name, Color = brush });
        }

        LegendPanel.ItemsSource = legendItems;
    }

    private void AddThresholdLine(double plotHeight, double threshold, string label, Color color)
    {
        double yPos = 10 + plotHeight - (threshold * plotHeight);
        var line = new Line
        {
            X1 = 50, Y1 = yPos,
            X2 = 50 + 720, Y2 = yPos,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([6, 3])
        };
        ChartCanvas.Children.Add(line);

        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 9, Foreground = new SolidColorBrush(color),
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF))
        };
        Canvas.SetLeft(lbl, 50 + 720 - 160);
        Canvas.SetTop(lbl, yPos - 14);
        ChartCanvas.Children.Add(lbl);
    }

    private static string TruncateLabel(string label, int maxLen)
        => label.Length <= maxLen ? label : label[..(maxLen - 1)] + "…";

    public class LegendEntry
    {
        public string Name { get; set; } = "";
        public Brush Color { get; set; } = Brushes.Black;
    }
}
