using System.Text;
using KnuthPlass.Core.Breaking;
using KnuthPlass.Core.Results;

namespace KnuthPlass.Rendering.Svg;

public sealed record BreakpointGraphRenderOptions(
    int DetailThreshold = 120,
    int SelectedNeighborhood = 1);

public sealed class BreakpointGraphSvgRenderer
{
    public string Render(
        LineBreakResult result,
        BreakpointGraphRenderOptions? renderOptions = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        renderOptions ??= new BreakpointGraphRenderOptions();
        if (renderOptions.DetailThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderOptions),
                "The graph detail threshold must be positive.");
        }

        if (renderOptions.SelectedNeighborhood < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(renderOptions),
                "The selected neighborhood cannot be negative.");
        }

        if (result.AlgorithmName != KnuthPlassLineBreaker.Name)
        {
            throw new ArgumentException("The breakpoint graph requires a Knuth-Plass result.", nameof(result));
        }

        if (!result.HasGraphEvidence)
        {
            throw new InvalidOperationException(
                "The breakpoint graph requires captured graph evidence.");
        }

        var selectedKeys = SelectedEdgeKeys(result);
        var allEdges = result.GraphEdges
            .Select(item => new GraphEdge(
                item.StartBreakpointId,
                item.StartFitness,
                item.EndBreakpointId,
                item.EndFitness,
                item.LineDemerits,
                item.AccumulatedDemerits,
                selectedKeys.Contains(new EdgeKey(
                    item.StartBreakpointId,
                    item.StartFitness,
                    item.EndBreakpointId,
                    item.EndFitness))))
            .Distinct()
            .OrderBy(edge => edge.EndBreakpoint)
            .ThenBy(edge => edge.StartBreakpoint)
            .ThenBy(edge => edge.StartFitness)
            .ThenBy(edge => edge.EndFitness)
            .ToArray();

        var pruned = allEdges.Length > renderOptions.DetailThreshold;
        var edges = pruned
            ? Prune(allEdges, result.SelectedBreakpointIds, renderOptions)
            : allEdges;
        var selectedNodes = SelectedNodes(result).ToArray();
        var nodes = edges
            .SelectMany(edge => new[]
            {
                new GraphNode(edge.StartBreakpoint, edge.StartFitness),
                new GraphNode(edge.EndBreakpoint, edge.EndFitness),
            })
            .Concat(selectedNodes)
            .Distinct()
            .OrderBy(node => node.Breakpoint)
            .ThenBy(node => node.Fitness)
            .ToArray();

        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-labelledby=\"graph-title graph-desc\" viewBox=\"0 0 1200 520\">\n")
            .Append("<title id=\"graph-title\">Knuth-Plass feasible breakpoint fitness graph</title>\n")
            .Append("<desc id=\"graph-desc\">Directed edges are feasible candidate lines. The selected minimum-demerit path is emphasized.</desc>\n")
            .Append("""
<defs><marker id="arrow" markerWidth="8" markerHeight="6" refX="7" refY="3" orient="auto"><path d="M0 0L8 3L0 6Z" fill="context-stroke"/></marker></defs>
<style>.edge{stroke:#94a3b8;stroke-width:1.4;fill:none;marker-end:url(#arrow)}.edge.selected{stroke:#b91c1c;stroke-width:3}.node{fill:#eff6ff;stroke:#1d4ed8}.node.selected{fill:#fee2e2;stroke:#b91c1c;stroke-width:2.5}.node-label{font:12px system-ui,sans-serif;fill:#172033;text-anchor:middle}.edge-label{font:10px ui-monospace,monospace;fill:#475569;text-anchor:middle}.note{font:13px system-ui,sans-serif;fill:#9a3412}</style>
""");

        if (pruned)
        {
            builder.Append("<text class=\"note\" x=\"30\" y=\"28\">Graph pruned: selected path plus bounded neighborhood shown; counters remain complete.</text>\n");
        }

        for (var edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            var edge = edges[edgeIndex];
            var start = Position(
                edge.StartBreakpoint,
                edge.StartFitness,
                result.ParagraphBreakpoints.Length);
            var end = Position(
                edge.EndBreakpoint,
                edge.EndFitness,
                result.ParagraphBreakpoints.Length);
            var curveOffset = (edgeIndex % 5 - 2) * 6;
            var middleX = (start.X + end.X) / 2;
            var middleY = (start.Y + end.Y) / 2 + curveOffset;
            builder.Append("<path id=\"edge-").Append(edgeIndex.ToString("D3", System.Globalization.CultureInfo.InvariantCulture))
                .Append("\" class=\"edge").Append(edge.Selected ? " selected" : string.Empty)
                .Append("\" d=\"M").Append(RenderFormatting.Number(start.X)).Append(' ')
                .Append(RenderFormatting.Number(start.Y)).Append(" Q")
                .Append(RenderFormatting.Number(middleX)).Append(' ')
                .Append(RenderFormatting.Number(middleY)).Append(' ')
                .Append(RenderFormatting.Number(end.X)).Append(' ')
                .Append(RenderFormatting.Number(end.Y)).Append("\" data-line-demerits=\"")
                .Append(RenderFormatting.Number(edge.LineDemerits)).Append("\"/>\n")
                .Append("<text class=\"edge-label\" x=\"").Append(RenderFormatting.Number(middleX))
                .Append("\" y=\"").Append(RenderFormatting.Number(middleY - 4)).Append("\">d=")
                .Append(RenderFormatting.Number(edge.LineDemerits)).Append("</text>\n");
        }

        var selectedNodeSet = selectedNodes.ToHashSet();
        foreach (var node in nodes)
        {
            var position = Position(
                node.Breakpoint,
                node.Fitness,
                result.ParagraphBreakpoints.Length);
            var nodeId = $"node-b{node.Breakpoint}-{FitnessToken(node.Fitness)}";
            builder.Append("<g id=\"").Append(nodeId).Append("\"><circle class=\"node")
                .Append(selectedNodeSet.Contains(node) ? " selected" : string.Empty)
                .Append("\" cx=\"").Append(RenderFormatting.Number(position.X))
                .Append("\" cy=\"").Append(RenderFormatting.Number(position.Y))
                .Append("\" r=\"18\"/><text class=\"node-label\" x=\"")
                .Append(RenderFormatting.Number(position.X)).Append("\" y=\"")
                .Append(RenderFormatting.Number(position.Y + 4)).Append("\">B")
                .Append(node.Breakpoint).Append("</text><text class=\"node-label\" x=\"")
                .Append(RenderFormatting.Number(position.X)).Append("\" y=\"")
                .Append(RenderFormatting.Number(position.Y + 34)).Append("\">")
                .Append(node.Fitness?.ToString() ?? "Start").Append("</text></g>\n");
        }

        builder.Append("</svg>\n");
        return builder.ToString();
    }

    private static GraphEdge[] Prune(
        IReadOnlyList<GraphEdge> allEdges,
        IReadOnlyCollection<int> selectedBreakpoints,
        BreakpointGraphRenderOptions options)
    {
        var selected = allEdges.Where(edge => edge.Selected).ToArray();
        var budget = Math.Max(0, options.DetailThreshold - selected.Length);
        var neighborhood = allEdges
            .Where(edge => !edge.Selected)
            .Where(edge => selectedBreakpoints.Any(selectedBreakpoint =>
                Math.Abs(edge.StartBreakpoint - selectedBreakpoint) <= options.SelectedNeighborhood
                || Math.Abs(edge.EndBreakpoint - selectedBreakpoint) <= options.SelectedNeighborhood))
            .Take(budget);

        return selected.Concat(neighborhood)
            .OrderBy(edge => edge.EndBreakpoint)
            .ThenBy(edge => edge.StartBreakpoint)
            .ThenBy(edge => edge.StartFitness)
            .ThenBy(edge => edge.EndFitness)
            .ToArray();
    }

    private static HashSet<EdgeKey> SelectedEdgeKeys(LineBreakResult result)
    {
        var keys = new HashSet<EdgeKey>();
        for (var index = 0; index < result.Lines.Length; index++)
        {
            keys.Add(new EdgeKey(
                result.Lines[index].Metrics.Start.Id,
                index == 0 ? null : result.Lines[index - 1].Metrics.Fitness,
                result.Lines[index].Metrics.End.Id,
                result.Lines[index].Metrics.Fitness!.Value));
        }

        return keys;
    }

    private static IEnumerable<GraphNode> SelectedNodes(LineBreakResult result)
    {
        if (!result.IsSuccess)
        {
            return [];
        }

        var nodes = new List<GraphNode> { new(result.SelectedBreakpointIds[0], null) };
        nodes.AddRange(result.Lines.Select(line =>
            new GraphNode(line.Metrics.End.Id, line.Metrics.Fitness)));
        return nodes;
    }

    private static (double X, double Y) Position(
        int breakpoint,
        FitnessClass? fitness,
        int breakpointCount)
    {
        var denominator = Math.Max(1, breakpointCount - 1);
        var x = 70 + (double)breakpoint / denominator * 1060;
        var row = fitness is null ? 0 : (int)fitness.Value + 1;
        return (x, 72 + row * 86);
    }

    private static string FitnessToken(FitnessClass? fitness) =>
        fitness?.ToString().ToLowerInvariant() ?? "start";

    private sealed record GraphNode(int Breakpoint, FitnessClass? Fitness);

    private sealed record EdgeKey(
        int StartBreakpoint,
        FitnessClass? StartFitness,
        int EndBreakpoint,
        FitnessClass EndFitness);

    private sealed record GraphEdge(
        int StartBreakpoint,
        FitnessClass? StartFitness,
        int EndBreakpoint,
        FitnessClass EndFitness,
        double LineDemerits,
        double? AccumulatedDemerits,
        bool Selected);
}
