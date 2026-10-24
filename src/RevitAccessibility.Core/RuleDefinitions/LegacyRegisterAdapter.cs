using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Register;
using RevitAccessibility.Core.Rules;

namespace RevitAccessibility.Core.RuleDefinitions;

/// <summary>
/// Expresses rows of the existing CSV register as schema-1 definitions, so rules/rule-register.csv stays the single
/// source of truth (no second copy of any threshold). Fields the CSV does not hold are left empty and surface as
/// governance warnings; nothing is invented. Rows the loader rejected are returned separately, never dropped.
/// </summary>
public static class LegacyRegisterAdapter
{
    /// <summary>Definitions plus every row that could not become one (loader rejections, unknown severity).</summary>
    public sealed record Adapted(IReadOnlyList<RuleDefinition> Definitions, IReadOnlyList<RuleNotRun> NotRun);

    public static Adapted Adapt(RegisterLoadResult register)
    {
        ArgumentNullException.ThrowIfNull(register);
        var defs = new List<RuleDefinition>();
        var notRun = register.Errors
            .Select(e => new RuleNotRun(e.RuleId.Length == 0 ? $"(row {e.Line})" : e.RuleId, e.Reason))
            .ToList();

        foreach (var r in register.Rules)
        {
            if (!Enum.TryParse<Severity>(r.Severity, true, out var sev) || !Enum.IsDefined(sev))
            {
                notRun.Add(new RuleNotRun(r.RuleId, $"Severity '{r.Severity}' is not Info, Warning or Error."));
                continue;
            }
            defs.Add(ToDefinition(r, sev));
        }
        return new Adapted(defs, notRun);
    }

    private static RuleDefinition ToDefinition(RegisterRule r, Severity severity)
    {
        bool isDoorClear = r.RuleId is DoorClearWidthThresholds.EntryRuleId or DoorClearWidthThresholds.OtherRuleId;
        bool isEntry = r.RuleId == DoorClearWidthThresholds.EntryRuleId;

        var verification = new RuleVerification(
            r.IsVerified ? VerificationState.Verified
            : string.Equals(r.Status.Trim(), "Needs review", StringComparison.OrdinalIgnoreCase) ? VerificationState.NeedsReview
            : VerificationState.Unverified,
            Notes: $"Register status: {r.Status}");

        return new RuleDefinition(
            SchemaVersion: RuleDefinition.CurrentSchemaVersion,
            RuleId: r.RuleId,
            RuleVersion: 1,
            Name: isDoorClear ? (isEntry ? "Door clear width: access door" : "Door clear width: other doors") : $"{r.Category} {r.Parameter}",
            Domain: RuleDomain.Accessibility,
            RuleType: RuleType.Dimension,
            SourceType: SourceType.Legislation,
            ElementCategories: [r.Category],
            Applicability: new RuleApplicability(
                RequiresClassification: isDoorClear
                    ? new ClassificationCondition(RuleCapabilities.AccessDoorClassification, isEntry ? "yes" : "no")
                    : null),
            Measurement: isDoorClear
                ? new RuleMeasurement("clear-width", RuleCapabilities.DoorClearWidth, null, "mm", MeasurementMeaning.ClearOpening)
                : new RuleMeasurement(r.Parameter, RuleCapabilities.LengthParameter, r.Parameter, "mm"),
            Evaluation: new RuleEvaluation(r.Operator.Trim(), r.ThresholdMm, "mm"),
            Source: new RuleSourceRef(Document: "rules/rule-register.csv", Clause: r.Clause),
            Verification: verification,
            Lifecycle: new RuleLifecycle(LifecycleStatus.Draft),
            Severity: severity);
    }
}
