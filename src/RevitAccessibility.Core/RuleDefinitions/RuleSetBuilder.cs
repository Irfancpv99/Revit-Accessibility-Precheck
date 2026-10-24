using System.Globalization;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;

namespace RevitAccessibility.Core.RuleDefinitions;

/// <summary>A rule that did not run, with the reason. Reported, never dropped.</summary>
public sealed record RuleNotRun(string RuleId, string Reason)
{
    public override string ToString() => $"{RuleId}: {Reason}";
}

public sealed record RuleSetBuildResult(
    IReadOnlyList<IRule> Rules,
    IReadOnlyList<RuleNotRun> NotRun,
    IReadOnlyList<RuleValidationReport> Reports);

/// <summary>
/// Turns validated definitions into runnable rules. A definition becomes a rule only if it is schema-valid and
/// executable; every other definition is listed in <see cref="RuleSetBuildResult.NotRun"/> with the validator's reasons.
/// Legal verification does not block execution (unverified rules still run and are labelled), exactly as before.
/// </summary>
public static class RuleSetBuilder
{
    public static RuleSetBuildResult Build(
        IEnumerable<RuleDefinition> definitions,
        RuleCapabilities? capabilities = null,
        IEnumerable<RuleNotRun>? alreadyNotRun = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        capabilities ??= RuleCapabilities.Default;

        var rules = new List<IRule>();
        var notRun = new List<RuleNotRun>(alreadyNotRun ?? []);
        var reports = new List<RuleValidationReport>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var doorDefs = new List<RuleDefinition>();

        foreach (var def in definitions)
        {
            var report = RuleValidator.Validate(def, capabilities);
            reports.Add(report);

            if (!report.IsExecutable)
            {
                notRun.Add(new RuleNotRun(report.RuleId, report.Summary()));
                continue;
            }
            if (!seenIds.Add(def.RuleId!))
            {
                notRun.Add(new RuleNotRun(def.RuleId!, "Duplicate rule id: another definition with this id was already loaded."));
                continue;
            }

            if (def.Measurement!.Method == RuleCapabilities.DoorClearWidth) doorDefs.Add(def);
            else rules.AddRange(BuildLengthRules(def));
        }

        BuildDoorRule(doorDefs, rules, notRun);
        return new RuleSetBuildResult(rules, notRun, reports);
    }

    private static double ToMm(RuleEvaluation e) =>
        e.Threshold!.Value * (string.Equals(e.ThresholdUnit!.Trim(), "cm", StringComparison.OrdinalIgnoreCase) ? 10.0 : 1.0);

    private static string StatusText(VerificationState s) => s switch
    {
        VerificationState.Verified => "Verified",
        VerificationState.NeedsReview => "Needs review",
        _ => "Unverified",
    };

    private static IEnumerable<IRule> BuildLengthRules(RuleDefinition d)
    {
        var cmp = d.Evaluation!.Operator == ">=" ? Comparison.AtLeast : Comparison.AtMost;
        var status = StatusText(d.Verification?.Status ?? VerificationState.Unverified);
        foreach (var category in d.ElementCategories!.Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return new ParameterRule(new ParameterRuleSpec(
                d.RuleId!, d.Source!.Clause ?? d.Source.Document!, category, d.Measurement!.Parameter!.Trim(),
                cmp, ToMm(d.Evaluation), d.Severity, status));
    }

    private static void BuildDoorRule(List<RuleDefinition> doorDefs, List<IRule> rules, List<RuleNotRun> notRun)
    {
        if (doorDefs.Count == 0) return;

        static bool IsAccess(RuleDefinition d) => string.Equals(d.Applicability!.RequiresClassification!.Value!.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        var access = doorDefs.Where(IsAccess).ToList();
        var other = doorDefs.Where(d => !IsAccess(d)).ToList();

        if (access.Count != 1 || other.Count != 1)
        {
            foreach (var d in doorDefs)
                notRun.Add(new RuleNotRun(d.RuleId!,
                    "The door clear-width rule needs exactly one access-door definition and exactly one other-door definition " +
                    $"(found {access.Count} and {other.Count}); an unpaired door rule is not run."));
            return;
        }

        var a = access[0];
        var o = other[0];
        var notVerified = a.Verification?.Status != VerificationState.Verified || o.Verification?.Status != VerificationState.Verified;
        var aText = StatusText(a.Verification?.Status ?? VerificationState.Unverified);
        var oText = StatusText(o.Verification?.Status ?? VerificationState.Unverified);

        var thresholds = new DoorClearWidthThresholds(
            ToMm(a.Evaluation!), ToMm(o.Evaluation!),
            a.Source!.Clause ?? a.Source.Document!,
            notVerified ? $"UNVERIFIED: register status '{aText}' / '{oText}'. Not a verified regulation value." : null,
            notVerified ? $"{aText} / {oText}" : "Verified");
        rules.Add(new DoorClearWidthRule(thresholds));
    }
}
