using RevitAccessibility.Core.Geometry;
using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.Rules;

/// <summary>
/// A coordination check between two categories. This is a GEOMETRIC check, not a regulatory one: it cites no
/// legal clause and its result is never a legal Fail.
/// </summary>
/// <param name="ToleranceMm">
/// Boxes must overlap by MORE than this on every axis to become a candidate. It is a modelling tolerance to be
/// agreed by the BIM coordinator. It is not a regulation value.
/// </param>
public sealed record ClashRuleSpec(string RuleId, string Version, string CategoryA, string CategoryB, double ToleranceMm = 1.0)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RuleId)) throw new ArgumentException("Rule id is required.");
        if (string.IsNullOrWhiteSpace(Version)) throw new ArgumentException("Rule version is required.");
        if (string.IsNullOrWhiteSpace(CategoryA) || string.IsNullOrWhiteSpace(CategoryB)) throw new ArgumentException("Both categories are required.");
        if (!double.IsFinite(ToleranceMm) || ToleranceMm < 0) throw new ArgumentOutOfRangeException(nameof(ToleranceMm), "Tolerance must be finite and not negative.");
    }
}

public sealed record ClashCandidate(GeoElementDto A, GeoElementDto B, Vec3 OverlapMm)
{
    public double MinOverlapMm => Math.Min(OverlapMm.X, Math.Min(OverlapMm.Y, OverlapMm.Z));
    public string PairKey => ClashDetector.PairKey(A.Key, B.Key);
}

public sealed record ClashScan(
    IReadOnlyList<ClashCandidate> Candidates,
    IReadOnlyList<GeoElementDto> Unusable,
    IReadOnlyList<GeoElementDto> Duplicates);

/// <summary>
/// Broad phase. Finds element pairs whose boxes overlap, and reports elements it could not test.
/// Complexity is O(n*m) with a cheap per-axis rejection, which is adequate for thousands of elements per
/// category. A spatial grid is the upgrade path if models grow beyond that.
/// A box overlap is NOT proof of a clash. Candidates must be confirmed with an exact test (Revit side).
/// </summary>
public static class ClashDetector
{
    public static string PairKey(string keyA, string keyB) =>
        string.CompareOrdinal(keyA, keyB) <= 0 ? keyA + "|" + keyB : keyB + "|" + keyA;

    public static ClashScan Scan(IEnumerable<GeoElementDto> elements, ClashRuleSpec spec)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(spec);
        spec.Validate();

        // Identity is (DocumentId, UniqueId). The same identity supplied twice is reported, then used once.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<GeoElementDto>();
        var duplicates = new List<GeoElementDto>();
        foreach (var e in elements)
        {
            bool inScope = Same(e.Category, spec.CategoryA) || Same(e.Category, spec.CategoryB);
            if (!inScope) continue;
            if (seen.Add(e.Key)) unique.Add(e); else duplicates.Add(e);
        }

        var unusable = unique.Where(e => e.Status != GeometryStatus.Available || e.BoxMm is null).ToList();
        var usable = unique.Where(e => e.Status == GeometryStatus.Available && e.BoxMm is not null).ToList();

        var setA = usable.Where(e => Same(e.Category, spec.CategoryA)).ToList();
        var setB = usable.Where(e => Same(e.Category, spec.CategoryB)).ToList();
        bool sameCategory = Same(spec.CategoryA, spec.CategoryB);

        var found = new Dictionary<string, ClashCandidate>(StringComparer.Ordinal);
        foreach (var a in setA)
        {
            foreach (var b in setB)
            {
                if (a.Key == b.Key) continue;
                if (IsIntentionalConnection(a, b)) continue;

                // Cheap rejection on X before the full test.
                if (a.BoxMm!.Max.X + spec.ToleranceMm < b.BoxMm!.Min.X || b.BoxMm.Max.X + spec.ToleranceMm < a.BoxMm.Min.X) continue;
                if (!a.BoxMm.Overlaps(b.BoxMm, spec.ToleranceMm)) continue;

                string key = PairKey(a.Key, b.Key);
                if (sameCategory && found.ContainsKey(key)) continue; // (a,b) and (b,a) are one pair
                // Different categories: category A is always the primary element. Same category: order by identity so the pair is stable.
                var ordered = sameCategory && string.CompareOrdinal(a.Key, b.Key) > 0 ? (b, a) : (a, b);
                found[key] = new ClashCandidate(ordered.Item1, ordered.Item2, a.BoxMm.OverlapExtents(b.BoxMm));
            }
        }

        var candidates = found.Values.OrderBy(c => c.PairKey, StringComparer.Ordinal).ToList();
        return new ClashScan(candidates, unusable, duplicates);
    }

    // An element hosted in another (a door in its wall) is an intentional connection, not a clash.
    private static bool IsIntentionalConnection(GeoElementDto a, GeoElementDto b) =>
        (a.HostUid is not null && a.HostUid == b.Uid && a.DocumentId == b.DocumentId) ||
        (b.HostUid is not null && b.HostUid == a.Uid && a.DocumentId == b.DocumentId);

    private static bool Same(string x, string y) => string.Equals(x?.Trim(), y?.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Reports potential clashes for review. Result is always NeedsReview (or CannotEvaluate when geometry is unusable):
/// a clash is not automatically a violation, because connections, penetrations and tolerances can be intentional.
/// A pair with no candidate overlap produces NO finding. Absence of a finding is not a Pass.
/// </summary>
public sealed class ClashRule : IRule
{
    public const string NotRegulatory = "n/a (geometric coordination check, not a regulatory rule)";
    private readonly ClashRuleSpec _spec;

    public ClashRule(ClashRuleSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        spec.Validate();
    }

    public string Id => _spec.RuleId;

    public IEnumerable<Finding> Evaluate(BuildingModel model)
    {
        var scan = ClashDetector.Scan(model.AllGeoElements, _spec);
        var confirmations = new Dictionary<string, PairConfirmationDto>(StringComparer.Ordinal);
        foreach (var c in model.AllConfirmations)
            confirmations[ClashDetector.PairKey(c.KeyA, c.KeyB)] = c;

        foreach (var d in scan.Duplicates)
            yield return Cannot(d, $"{d.Category} '{d.Key}' was supplied more than once; only the first is used.", "unique element identity");

        foreach (var u in scan.Unusable)
            yield return Cannot(u,
                $"{u.Category} on level '{u.Level}' has no usable geometry ({u.Status}){(string.IsNullOrWhiteSpace(u.GeometryNote) ? "" : ": " + u.GeometryNote.Trim())}. " +
                "It was not tested; this is not a 'no clash' result.",
                "element geometry");

        foreach (var c in scan.Candidates)
        {
            confirmations.TryGetValue(c.PairKey, out var conf);
            var result = conf?.Result ?? PairConfirmation.NotAttempted;
            if (result == PairConfirmation.NoIntersection) continue; // exact test ruled it out

            string pair = $"{c.A.Category} {Label(c.A)} and {c.B.Category} {Label(c.B)}";
            string message = result switch
            {
                PairConfirmation.Intersects =>
                    $"Confirmed geometric intersection between {pair}{(conf!.OverlapVolumeMm3 is { } v ? $" (overlap volume {v:0.##} mm3)" : "")}. " +
                    "Review whether it is an intentional connection or a permitted penetration; this is not a legal finding.",
                PairConfirmation.Failed =>
                    $"Potential clash between {pair}: boxes overlap by at least {c.MinOverlapMm:0.##} mm, but the exact intersection test failed" +
                    $"{(string.IsNullOrWhiteSpace(conf!.Note) ? "" : " (" + conf.Note.Trim() + ")")}. Review manually.",
                _ =>
                    $"Potential clash between {pair}: bounding boxes overlap by at least {c.MinOverlapMm:0.##} mm. " +
                    "Broad-phase only; the exact intersection was not tested. Review manually.",
            };

            string source = result == PairConfirmation.Intersects
                ? "exact solid intersection (Revit), after bounding-box broad phase"
                : "axis-aligned bounding boxes in host coordinates (broad phase only)";

            yield return new Finding(Id, "N/A (geometric check)", Severity.Warning, FindingStatus.NeedsReview, c.A.Uid, message,
                new FindingDetail(_spec.Version, CheckType.Geometric,
                    SourceDocument: c.A.DocumentId,
                    LinkedDocument: c.A.DocumentId != c.B.DocumentId ? c.B.DocumentId : null,
                    MeasuredValue: result == PairConfirmation.Intersects && conf!.OverlapVolumeMm3 is { } vol ? vol : c.MinOverlapMm,
                    Unit: result == PairConfirmation.Intersects && conf!.OverlapVolumeMm3 is not null ? "mm3" : "mm",
                    MeasurementSource: source,
                    ExpectedCondition: $"no unreviewed overlap between {_spec.CategoryA} and {_spec.CategoryB} (coordination tolerance {_spec.ToleranceMm:0.##} mm)",
                    VerificationStatus: NotRegulatory,
                    MissingInputs: result == PairConfirmation.Intersects ? null : ["exact intersection test"],
                    RelatedElement: c.B.Key));
        }
    }

    private static string Label(GeoElementDto e) => string.IsNullOrWhiteSpace(e.Mark) ? $"'{e.Uid}'" : $"'{e.Mark}'";

    private Finding Cannot(GeoElementDto e, string message, string missing) =>
        new(Id, "N/A (geometric check)", Severity.Info, FindingStatus.CannotEvaluate, e.Uid, message,
            new FindingDetail(_spec.Version, CheckType.Geometric, SourceDocument: e.DocumentId,
                VerificationStatus: NotRegulatory, MissingInputs: [missing]));
}
