using System.Text.RegularExpressions;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;

namespace RevitAccessibility.Core.RuleDefinitions;

/// <summary>
/// Three different kinds of problem, never merged into one Boolean:
/// Schema = the definition is malformed; Execution = well-formed but the engine cannot run it;
/// Governance = it may run, but its legal or review status needs attention.
/// </summary>
public enum IssueCategory { Schema, Execution, Governance }

public sealed record ValidationIssue(IssueCategory Category, string Code, string Field, string Message);

public sealed record RuleValidationReport(string RuleId, int RuleVersion, IReadOnlyList<ValidationIssue> Issues)
{
    private IEnumerable<ValidationIssue> Of(IssueCategory c) => Issues.Where(i => i.Category == c);

    public IReadOnlyList<ValidationIssue> SchemaErrors => Of(IssueCategory.Schema).ToList();
    public IReadOnlyList<ValidationIssue> ExecutionErrors => Of(IssueCategory.Execution).ToList();
    public IReadOnlyList<ValidationIssue> GovernanceWarnings => Of(IssueCategory.Governance).ToList();

    public bool IsSchemaValid => !Issues.Any(i => i.Category == IssueCategory.Schema);

    /// <summary>True only when the definition is well-formed AND the engine implements what it asks for.
    /// Says nothing about legal approval or about applicability to a project.</summary>
    public bool IsExecutable => IsSchemaValid && !Issues.Any(i => i.Category == IssueCategory.Execution);

    public string Summary() => string.Join(" ", Issues.Where(i => i.Category != IssueCategory.Governance).Select(i => $"[{i.Code}] {i.Message}"));
}

public static partial class RuleValidator
{
    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{1,63}$")]
    private static partial Regex IdPattern();

    public static RuleValidationReport Validate(RuleDefinition? rule, RuleCapabilities? capabilities = null)
    {
        capabilities ??= RuleCapabilities.Default;
        if (rule is null)
            return new RuleValidationReport("(none)", 0,
                [new(IssueCategory.Schema, "SCH-NULL", "(root)", "The rule definition is empty.")]);

        var issues = new List<ValidationIssue>();
        void Add(IssueCategory c, string code, string field, string message) => issues.Add(new(c, code, field, message));
        void Schema(string code, string field, string message) => Add(IssueCategory.Schema, code, field, message);
        void Exec(string code, string field, string message) => Add(IssueCategory.Execution, code, field, message);
        void Gov(string code, string field, string message) => Add(IssueCategory.Governance, code, field, message);

        ValidateSchema(rule, Schema);
        if (!issues.Any(i => i.Category == IssueCategory.Schema))
            ValidateExecution(rule, capabilities, Exec);
        ValidateGovernance(rule, Gov);

        return new RuleValidationReport(rule.RuleId ?? "(no id)", rule.RuleVersion, issues);
    }

    private static void ValidateSchema(RuleDefinition r, Action<string, string, string> schema)
    {
        if (r.SchemaVersion != RuleDefinition.CurrentSchemaVersion)
            schema("SCH-VERSION", "schemaVersion", $"Unsupported schemaVersion {r.SchemaVersion}; this build reads version {RuleDefinition.CurrentSchemaVersion}.");
        if (string.IsNullOrWhiteSpace(r.RuleId) || !IdPattern().IsMatch(r.RuleId))
            schema("SCH-ID", "ruleId", "ruleId must be 2-64 characters: upper-case letters, digits, '.', '_' or '-'.");
        if (r.RuleVersion < 1) schema("SCH-RULE-VERSION", "ruleVersion", "ruleVersion must be 1 or higher.");
        if (string.IsNullOrWhiteSpace(r.Name)) schema("SCH-NAME", "name", "name is required.");
        if (!Enum.IsDefined(r.Domain)) schema("SCH-DOMAIN", "domain", "domain is not a known value.");
        if (!Enum.IsDefined(r.RuleType)) schema("SCH-RULE-TYPE", "ruleType", "ruleType is not a known value.");
        if (!Enum.IsDefined(r.SourceType)) schema("SCH-SOURCE-TYPE", "sourceType", "sourceType is not a known value.");

        if (r.ElementCategories is null || r.ElementCategories.Count == 0 || r.ElementCategories.Any(string.IsNullOrWhiteSpace))
            schema("SCH-CATEGORIES", "elementCategories", "At least one non-blank element category is required.");

        if (r.Measurement is null) schema("SCH-MEASUREMENT", "measurement", "measurement is required.");
        else
        {
            if (string.IsNullOrWhiteSpace(r.Measurement.Method)) schema("SCH-METHOD", "measurement.method", "measurement.method is required.");
            if (string.IsNullOrWhiteSpace(r.Measurement.Id)) schema("SCH-MEASUREMENT-ID", "measurement.id", "measurement.id is required.");
        }

        if (r.Evaluation is null) schema("SCH-EVALUATION", "evaluation", "evaluation is required.");
        else
        {
            if (r.Evaluation.Operator is null || !RuleCapabilities.KnownOperators.Contains(r.Evaluation.Operator))
                schema("SCH-OPERATOR", "evaluation.operator", $"Operator '{r.Evaluation.Operator}' is not recognised (expected one of: {string.Join(" ", RuleCapabilities.KnownOperators.Order())}).");
            if (r.Evaluation.Threshold is not { } t || !double.IsFinite(t) || t <= 0)
                schema("SCH-THRESHOLD", "evaluation.threshold", "threshold must be a positive, finite number.");
            if (r.Evaluation.ThresholdUnit is null || !RuleCapabilities.KnownUnits.Contains(r.Evaluation.ThresholdUnit.Trim()))
                schema("SCH-UNIT", "evaluation.thresholdUnit", "thresholdUnit must be mm or cm.");
            if (r.Evaluation.ToleranceMm is { } tol && (!double.IsFinite(tol) || tol < 0))
                schema("SCH-TOLERANCE", "evaluation.toleranceMm", "toleranceMm must be zero or a positive, finite number.");
        }

        var needsClause = r.SourceType is SourceType.Legislation or SourceType.TechnicalStandard;
        if (r.Source is null || string.IsNullOrWhiteSpace(r.Source.Document))
            schema("SCH-SOURCE", "source.document", "source.document is required.");
        else if (needsClause && string.IsNullOrWhiteSpace(r.Source.Clause))
            schema("SCH-CLAUSE", "source.clause", "A legislation or technical-standard rule must cite the article, point or clause.");

        var applic = r.Applicability?.RequiresClassification;
        if (applic is not null && (string.IsNullOrWhiteSpace(applic.Name) || string.IsNullOrWhiteSpace(applic.Value)))
            schema("SCH-CLASSIFICATION", "applicability.requiresClassification", "A classification condition needs both a name and a value.");

        if (r.Verification?.Status == VerificationState.Verified)
        {
            if (string.IsNullOrWhiteSpace(r.Verification.ArchitectReviewer) || string.IsNullOrWhiteSpace(r.Verification.ProfessorReviewer)
                || string.IsNullOrWhiteSpace(r.Verification.ReviewDate) || string.IsNullOrWhiteSpace(r.Source?.Clause))
                schema("SCH-VERIFIED-UNSIGNED", "verification",
                    "A Verified rule needs the cited clause, both reviewers (architect and professor) and a review date. Verified cannot be claimed without sign-off.");
        }
    }

    private static void ValidateExecution(RuleDefinition r, RuleCapabilities cap, Action<string, string, string> exec)
    {
        if (r.Lifecycle?.Status == LifecycleStatus.Deprecated)
            exec("EXE-DEPRECATED", "lifecycle.status", "This rule version is deprecated and does not run.");

        if (r.RuleType != RuleType.Dimension)
            exec("EXE-RULETYPE", "ruleType", $"Rule type '{r.RuleType}' is not implemented; only Dimension rules run today.");

        var method = r.Measurement!.Method!;
        if (!cap.Methods.TryGetValue(method, out var supported))
        {
            exec("EXE-METHOD", "measurement.method",
                $"Measurement method '{method}' is not implemented (supported: {string.Join(", ", cap.Methods.Keys.Order())}).");
            return;
        }

        if (supported.RuleType != r.RuleType)
            exec("EXE-METHOD-TYPE", "ruleType", $"Method '{method}' belongs to rule type {supported.RuleType}, not {r.RuleType}.");

        if (!supported.Operators.Contains(r.Evaluation!.Operator!))
            exec("EXE-OPERATOR", "evaluation.operator",
                $"Operator '{r.Evaluation.Operator}' is not implemented for method '{method}' (supported: {string.Join(" ", supported.Operators.Order())}).");

        if (supported.NeedsParameter && string.IsNullOrWhiteSpace(r.Measurement.Parameter))
            exec("EXE-PARAMETER", "measurement.parameter", $"Method '{method}' reads a Revit parameter; measurement.parameter is required.");

        if (r.Evaluation.ToleranceMm is { } tol && Math.Abs(tol - MeasurementTolerance.Mm) > 1e-12)
            exec("EXE-TOLERANCE", "evaluation.toleranceMm",
                $"Per-rule tolerance is not supported; only the global {MeasurementTolerance.Mm} mm floating-point tolerance applies.");

        if (method == RuleCapabilities.LengthParameter)
        {
            foreach (var c in r.ElementCategories!.Where(c => !cap.SupportedCategories.Contains(c.Trim())))
                exec("EXE-CATEGORY", "elementCategories", $"Category '{c}' cannot be read by the Revit adapter yet.");
        }
        else if (method == RuleCapabilities.DoorClearWidth)
        {
            if (r.ElementCategories!.Count != 1 || !string.Equals(r.ElementCategories[0].Trim(), "Doors", StringComparison.OrdinalIgnoreCase))
                exec("EXE-CATEGORY", "elementCategories", "door-clear-width applies to the Doors category only.");
            var cond = r.Applicability?.RequiresClassification;
            if (cond is null || cond.Name?.Trim() != RuleCapabilities.AccessDoorClassification
                || !(string.Equals(cond.Value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(cond.Value?.Trim(), "no", StringComparison.OrdinalIgnoreCase)))
                exec("EXE-CLASSIFICATION", "applicability.requiresClassification",
                    "door-clear-width needs requiresClassification access-door = yes (access doors) or no (other doors); an unclassified door is never assumed.");
            if (r.Measurement.RequiredMeaning != MeasurementMeaning.ClearOpening)
                exec("EXE-MEANING", "measurement.requiredMeaning",
                    "door-clear-width must declare requiredMeaning ClearOpening; a nominal width is never judged as the legal clear opening.");
        }
    }

    private static void ValidateGovernance(RuleDefinition r, Action<string, string, string> gov)
    {
        var v = r.Verification?.Status ?? VerificationState.Unverified;
        if (v != VerificationState.Verified)
            gov("GOV-UNVERIFIED", "verification.status", $"Legal value is {v}; findings are labelled UNVERIFIED and are not approved results.");
        if (r.Lifecycle is null || r.Lifecycle.Status != LifecycleStatus.Approved)
            gov("GOV-NOT-APPROVED", "lifecycle.status", $"Lifecycle is {r.Lifecycle?.Status.ToString() ?? "not set"}, not Approved.");
        if (r.Lifecycle?.TestCaseRefs is null or { Count: 0 })
            gov("GOV-NO-TESTS", "lifecycle.testCaseRefs", "No test cases are referenced for this rule.");
        var a = r.Applicability;
        if (a is null || string.IsNullOrWhiteSpace(a.Jurisdiction) || string.IsNullOrWhiteSpace(a.BuildingType) || a.Assumptions is null or { Count: 0 })
            gov("GOV-APPLICABILITY", "applicability", "Applicability is incomplete (jurisdiction, building type and explicit assumptions are expected). Applicability to a project is not inferred from the Revit category.");
        if (r.SourceType == SourceType.TestOnly)
            gov("GOV-TEST-ONLY", "sourceType", "Test-only rule: not a regulatory or project requirement.");
    }
}
