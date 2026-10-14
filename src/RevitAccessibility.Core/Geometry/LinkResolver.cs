namespace RevitAccessibility.Core.Geometry;

public enum LinkStatus { Host, Loaded, Unloaded, Missing, Unsupported }

/// <summary>
/// One Revit document taking part in a check: the host project or a linked model.
/// <paramref name="TransformToParent"/> maps this document's coordinates into its parent's coordinates
/// (for a link instance in Revit this is the instance transform). Values are in millimetres.
/// </summary>
public sealed record DocumentDto(
    string Id,
    string Title,
    string? ParentId,
    LinkStatus Status,
    RigidTransform? TransformToParent,
    string? Note = null);

/// <summary>A document with its transform into HOST coordinates, or the reason none can be given.</summary>
public sealed record ResolvedDocument(string Id, string Title, RigidTransform? ToHost, string? Problem)
{
    public bool IsUsable => ToHost is not null;
}

/// <summary>
/// Composes link transforms through nested links so every document can be expressed in host coordinates.
/// Raw coordinates from different documents must never be compared directly: always go through this.
/// Unloaded, missing or unsupported links, missing transforms, cycles and duplicate ids are reported,
/// never guessed.
/// </summary>
public static class LinkResolver
{
    private const int MaxDepth = 32;

    public static IReadOnlyDictionary<string, ResolvedDocument> Resolve(IReadOnlyList<DocumentDto> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var result = new Dictionary<string, ResolvedDocument>(StringComparer.Ordinal);

        var duplicateIds = documents.GroupBy(d => d.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var unique = documents.Where(d => !duplicateIds.Contains(d.Id)).ToDictionary(d => d.Id, StringComparer.Ordinal);

        foreach (var id in duplicateIds)
            result[id] = new ResolvedDocument(id, documents.First(d => d.Id == id).Title, null, $"Document id '{id}' is used more than once.");

        var hosts = documents.Where(d => d.Status == LinkStatus.Host).ToList();
        if (hosts.Count != 1)
        {
            foreach (var d in unique.Values)
                result[d.Id] = new ResolvedDocument(d.Id, d.Title, null, hosts.Count == 0 ? "No host document was supplied." : "More than one host document was supplied.");
            return result;
        }

        foreach (var doc in unique.Values)
            result[doc.Id] = ResolveOne(doc, unique);
        return result;
    }

    private static ResolvedDocument ResolveOne(DocumentDto doc, IReadOnlyDictionary<string, DocumentDto> all)
    {
        RigidTransform toHost = RigidTransform.Identity;
        var current = doc;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int depth = 0; depth < MaxDepth; depth++)
        {
            if (!seen.Add(current.Id))
                return Fail(doc, $"Link chain contains a cycle at '{current.Title}'.");

            if (current.Status == LinkStatus.Host)
                return new ResolvedDocument(doc.Id, doc.Title, toHost, null);

            if (current.Status != LinkStatus.Loaded)
                return Fail(doc, $"Linked document '{current.Title}' is {current.Status.ToString().ToLowerInvariant()}{(current == doc ? "" : " (an ancestor of this document)")}.");
            if (current.TransformToParent is null)
                return Fail(doc, $"Linked document '{current.Title}' has no transform to its parent.");
            if (current.ParentId is null || !all.TryGetValue(current.ParentId, out var parent))
                return Fail(doc, $"Parent of '{current.Title}' was not found.");

            // Apply the chain outward: this document -> parent -> ... -> host.
            toHost = current.TransformToParent.Compose(toHost);
            current = parent;
        }
        return Fail(doc, $"Link chain is deeper than {MaxDepth} levels.");
    }

    private static ResolvedDocument Fail(DocumentDto doc, string problem) => new(doc.Id, doc.Title, null, problem);
}
