using System;
using System.Collections.Generic;
using System.Linq;

namespace Pixel_Craft_Launcher.Routing;

public sealed record RouteNode(string Segment, IReadOnlyDictionary<string, string>? Parameters = null, RouteNode? Child = null)
{
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        Parameters ?? new Dictionary<string, string>();

    public RouteNode Append(RouteNode child) => this with { Child = Child is null ? child : Child.Append(child) };

    public bool StartsWith(string segment) => string.Equals(Segment, segment, StringComparison.OrdinalIgnoreCase);

    public RouteNode? Find(string segment)
    {
        for (var current = this; current is not null; current = current.Child)
        {
            if (current.StartsWith(segment))
                return current;
        }

        return null;
    }

    public string Key => Child is null ? Segment : Segment + "/" + Child.Key;

    public override string ToString()
    {
        var query = Parameters.Count == 0
            ? string.Empty
            : "?" + string.Join("&", Parameters.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        return Segment + query + (Child is null ? string.Empty : "/" + Child);
    }
}
