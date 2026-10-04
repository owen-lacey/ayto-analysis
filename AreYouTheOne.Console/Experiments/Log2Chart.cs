using System.Globalization;
using System.Text;

namespace AreYouTheOne.Console.Experiments;

/// <summary>
/// Static SVG of mean log2(remaining viable matchings) after each event, one panel per strategy group, with a dashed
/// least-squares trendline per strategy. Uses the reference categorical palette with light and dark steps.
/// </summary>
public static class Log2Chart
{
    private static readonly (string Light, string Dark)[] Palette =
    [
        ("#2a78d6", "#3987e5"), ("#eb6834", "#d95926"), ("#1baf7a", "#199e70"), ("#eda100", "#c98500"),
        ("#e87ba4", "#d55181"), ("#008300", "#008300"), ("#4a3aa7", "#9085e9"), ("#e34948", "#e66767")
    ];

    private const double Width = 760, PanelHeight = 340, Left = 56, Right = 40, Top = 64, Bottom = 46;
    private const double YMax = 22;

    public static string Render(List<(string Title, List<HumanStrategiesExperiment.Summary> Series)> panels, int weeks)
    {
        var inv = CultureInfo.InvariantCulture;
        var events = 2 * weeks;
        var height = PanelHeight * panels.Count;
        var sb = new StringBuilder();
        sb.AppendLine(string.Format(inv, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {0} {1}\" width=\"{0}\" height=\"{1}\" font-family=\"system-ui, -apple-system, Segoe UI, sans-serif\" role=\"img\" aria-label=\"Mean log2 of remaining viable matchings after each event, per strategy\">", Width, height));
        sb.AppendLine("<style>");
        sb.AppendLine(":root{--surface:#fcfcfb;--text-primary:#0b0b0b;--text-secondary:#52514e;--grid:#e4e3df;");
        sb.AppendLine(string.Join("", Palette.Select((p, i) => $"--s{i + 1}:{p.Light};")) + "}");
        sb.AppendLine("@media (prefers-color-scheme: dark){:root{--surface:#1a1a19;--text-primary:#ffffff;--text-secondary:#c3c2b7;--grid:#3a3a37;");
        sb.AppendLine(string.Join("", Palette.Select((p, i) => $"--s{i + 1}:{p.Dark};")) + "}}");
        sb.AppendLine(".bg{fill:var(--surface)}.title{fill:var(--text-primary);font-size:15px;font-weight:600}.label{fill:var(--text-secondary);font-size:11px}");
        sb.AppendLine(".grid{stroke:var(--grid);stroke-width:1}.ref{stroke:var(--text-secondary);stroke-width:1;stroke-dasharray:3 3}");
        sb.AppendLine(".line{fill:none;stroke-width:2;stroke-linejoin:round;stroke-linecap:round}.trend{fill:none;stroke-width:1.5;stroke-dasharray:6 4;opacity:.75}");
        sb.AppendLine("circle.hit{fill:transparent;stroke:none}circle.hit:hover{fill:var(--surface);stroke:currentColor;stroke-width:2}");
        sb.AppendLine(string.Join("", Palette.Select((_, i) => $".c{i + 1}{{stroke:var(--s{i + 1});color:var(--s{i + 1})}}.f{i + 1}{{fill:var(--s{i + 1})}}")));
        sb.AppendLine("</style>");
        sb.AppendLine(string.Format(inv, "<rect class=\"bg\" width=\"{0}\" height=\"{1}\"/>", Width, height));

        for (var p = 0; p < panels.Count; p++)
        {
            var (title, series) = panels[p];
            var offset = p * PanelHeight;
            double X(double e) => Left + e / events * (Width - Left - Right);
            double Y(double v) => offset + Top + (1 - v / YMax) * (PanelHeight - Top - Bottom);

            sb.AppendLine(string.Format(inv, "<text class=\"title\" x=\"{0}\" y=\"{1}\">{2}</text>", Left, offset + 22, Escape(title)));

            // Legend: one row above the plot
            var lx = Left;
            for (var s = 0; s < series.Count; s++)
            {
                var name = series[s].Configuration.Name;
                sb.AppendLine(string.Format(inv, "<rect class=\"f{0}\" x=\"{1}\" y=\"{2}\" width=\"14\" height=\"3\" rx=\"1.5\"/>", s + 1, lx, offset + 37));
                sb.AppendLine(string.Format(inv, "<text class=\"label\" x=\"{0}\" y=\"{1}\">{2}</text>", lx + 19, offset + 42, Escape(name)));
                lx += 19 + name.Length * 6.2 + 16;
            }

            // Axes and grid
            for (var v = 0; v <= 20; v += 5)
            {
                sb.AppendLine(string.Format(inv, "<line class=\"grid\" x1=\"{0}\" x2=\"{1}\" y1=\"{2:F1}\" y2=\"{2:F1}\"/>", Left, Width - Right, Y(v)));
                sb.AppendLine(string.Format(inv, "<text class=\"label\" x=\"{0}\" y=\"{1:F1}\" text-anchor=\"end\">{2}</text>", Left - 8, Y(v) + 4, v));
            }

            for (var week = 0; week <= weeks; week++)
            {
                var label = week == 0 ? "start" : $"W{week}";
                sb.AppendLine(string.Format(inv, "<text class=\"label\" x=\"{0:F1}\" y=\"{1:F1}\" text-anchor=\"middle\">{2}</text>", X(2 * week), offset + PanelHeight - Bottom + 16, label));
            }

            sb.AppendLine(string.Format(inv, "<text class=\"label\" x=\"{0:F1}\" y=\"{1:F1}\" text-anchor=\"middle\">event (each week: truth booth, then match-up)</text>", X(events / 2d), offset + PanelHeight - 10));
            sb.AppendLine(string.Format(inv, "<text class=\"label\" transform=\"translate(14 {0:F1}) rotate(-90)\" text-anchor=\"middle\">log2(remaining viable matchings)</text>", Y(YMax / 2)));
            var lastBooth = events - 1;
            sb.AppendLine(string.Format(inv, "<line class=\"ref\" x1=\"{0:F1}\" x2=\"{0:F1}\" y1=\"{1:F1}\" y2=\"{2:F1}\"/>", X(lastBooth), Y(YMax), Y(0)));
            sb.AppendLine(string.Format(inv, "<text class=\"label\" x=\"{0:F1}\" y=\"{1:F1}\" text-anchor=\"end\">last truth booth →</text>", X(lastBooth) - 4, Y(YMax) + 10));

            for (var s = 0; s < series.Count; s++)
            {
                var means = series[s].MeanLog2ByEvent;
                var points = string.Join(" ", means.Select((m, e) => string.Format(inv, "{0:F1},{1:F1}", X(e), Y(m))));
                sb.AppendLine(string.Format(inv, "<polyline class=\"line c{0}\" points=\"{1}\"/>", s + 1, points));

                // Least-squares trendline over the events before the series reaches its floor
                var fit = means.Select((m, e) => (e, m)).Where(t => t.m > 0.5).ToList();
                if (fit.Count >= 2)
                {
                    var (slope, intercept) = LeastSquares(fit);
                    var xEnd = Math.Min(events, -intercept / slope);
                    sb.AppendLine(string.Format(inv, "<line class=\"trend c{0}\" x1=\"{1:F1}\" y1=\"{2:F1}\" x2=\"{3:F1}\" y2=\"{4:F1}\"><title>{5}: trend {6:F2} bits per event</title></line>",
                        s + 1, X(0), Y(intercept), X(xEnd), Y(intercept + slope * xEnd), Escape(series[s].Configuration.Name), -slope));
                }

                for (var e = 0; e < means.Length; e++)
                {
                    sb.AppendLine(string.Format(inv, "<circle class=\"hit c{0}\" cx=\"{1:F1}\" cy=\"{2:F1}\" r=\"5\"><title>{3}, event {4}: {5:F2}</title></circle>",
                        s + 1, X(e), Y(means[e]), Escape(series[s].Configuration.Name), e, means[e]));
                }
            }
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static (double Slope, double Intercept) LeastSquares(List<(int X, double Y)> points)
    {
        var meanX = points.Average(p => p.X);
        var meanY = points.Average(p => p.Y);
        var slope = points.Sum(p => (p.X - meanX) * (p.Y - meanY)) / points.Sum(p => Math.Pow(p.X - meanX, 2));
        return (slope, meanY - slope * meanX);
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
