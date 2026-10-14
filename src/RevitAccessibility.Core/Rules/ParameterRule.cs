using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Register;

namespace RevitAccessibility.Core.Rules;

public enum Comparison { AtLeast, AtMost }

/// <summary>One register row turned into a runnable check. Threshold is in millimetres.</summary>
public sealed record ParameterRuleSpec(
    string RuleId,
    string Clause,
    string Category,
    string Parameter,
    Comparison Comparison,
    double ThresholdMm,
    Severity Severity,
    string Status)
{
    public bool IsVerified => string.Equals(Status, "Verified", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Generic rule driven by a register row: "parameter P of every element in category C must be &gt;= (or &lt;=) T".
/// Every element of the category gets exactly one finding. Missing, zero, negative or non-finite values are
/// Cannot evaluate, never Pass. Rows whose status is not Verified are labelled UNVERIFIED in every message.
/// </summary>
public sealed class ParameterRule : IRule
{
    private readonly ParameterRuleSpec _spec;

    public ParameterRule(ParameterRuleSpec spec)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        if (string.IsNullOrWhiteSpace(spec.RuleId)) throw new ArgumentException("Rule id is required.", nameof(spec));
        if (string.IsNullOrWhiteSpace(spec.Clause)) throw new ArgumentException("Clause reference is required.", nameof(spec));
        if (string.IsNullOrWhiteSpace(spec.Category)) throw new ArgumentException("Category is required.", nameof(spec));
        if (string.IsNullOrWhiteSpace(spec.Parameter)) throw new ArgumentException("Parameter is required.", nameof(spec));
        if (!double.IsFinite(spec.ThresholdMm) || spec.ThresholdMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(spec), "Threshold must be a positive, finite value.");
    }

    public string Id => _spec.RuleId;
    public ParameterRuleSpec Spec => _spec;

    public IEnumerable<Finding> Evaluate(BuildingModel model)
    {
        foreach (var element in model.AllElements)
        {
            if (!string.Equals(element.Category, _spec.Category, StringComparison.OrdinalIgnoreCase)) continue;
            yield return EvaluateElement(element);
        }
    }

    private Finding EvaluateElement(ElementDto e)
    {
        string where = $"{e.Category} on level '{e.Level}'";

        if (!e.Measurements.TryGetValue(_spec.Parameter, out var m))
            return Cannot(e, $"{where}: parameter '{_spec.Parameter}' was not read.");
        if (m.ValueMm is null)
            return Cannot(e, $"{where}: no value for '{_spec.Parameter}'.{(string.IsNullOrWhiteSpace(m.Note) ? "" : " " + m.Note.Trim())}");

        double v = m.ValueMm.Value;
        if (!double.IsFinite(v) || v <= 0)
            return Cannot(e, $"{where}: '{_spec.Parameter}' has an invalid value ({v}); 0 is treated as not measured.");

        bool ok = _spec.Comparison == Comparison.AtLeast
            ? MeasurementTolerance.AtLeast(v, _spec.ThresholdMm)
            : MeasurementTolerance.AtMost(v, _spec.ThresholdMm);

        string op = _spec.Comparison == Comparison.AtLeast ? "at least" : "at most";
        return ok
            ? new Finding(Id, _spec.Clause, Severity.Info, FindingStatus.Pass, e.Uid,
                Label($"{where}: {_spec.Parameter} {v:0.##} mm meets the requirement ({op} {_spec.ThresholdMm:0.##} mm)."))
            : new Finding(Id, _spec.Clause, _spec.Severity, FindingStatus.Fail, e.Uid,
                Label($"{where}: {_spec.Parameter} {v:0.##} mm does not meet the requirement ({op} {_spec.ThresholdMm:0.##} mm)."));
    }

    private Finding Cannot(ElementDto e, string message) =>
        new(Id, _spec.Clause, Severity.Info, FindingStatus.CannotEvaluate, e.Uid, Label(message));

    private string Label(string message) =>
        _spec.IsVerified ? message : $"{message} [UNVERIFIED: register status '{_spec.Status}'. Not a verified regulation value.]";
}

/// <summary>Builds <see cref="ParameterRule"/>s from the register. Rows that cannot run are reported, never dropped.</summary>
public sealed record ParameterRuleSet(IReadOnlyList<ParameterRule> Rules, IReadOnlyList<string> NotRun);

public static class ParameterRuleFactory
{
    /// <param name="excludeRuleIds">Rows handled by a dedicated rule (for example the two door clear-width rows).</param>
    public static ParameterRuleSet FromRegister(RegisterLoadResult register, IEnumerable<string> excludeRuleIds)
    {
        ArgumentNullException.ThrowIfNull(register);
        var exclude = new HashSet<string>(excludeRuleIds ?? [], StringComparer.Ordinal);
        var rules = new List<ParameterRule>();
        var notRun = new List<string>();

        foreach (var r in register.Rules.Where(r => !exclude.Contains(r.RuleId)))
        {
            Comparison? cmp = r.Operator.Trim() switch { ">=" => Comparison.AtLeast, "<=" => Comparison.AtMost, _ => null };
            if (cmp is null) { notRun.Add($"{r.RuleId}: operator '{r.Operator}' is not supported (use >= or <=)."); continue; }

            Severity? sev = Enum.TryParse<Severity>(r.Severity, ignoreCase: true, out var parsed) ? parsed : null;
            if (sev is null) { notRun.Add($"{r.RuleId}: severity '{r.Severity}' is not Info, Warning or Error."); continue; }

            if (string.IsNullOrWhiteSpace(r.Category) || string.IsNullOrWhiteSpace(r.Parameter))
            { notRun.Add($"{r.RuleId}: Category and Parameter are required."); continue; }

            rules.Add(new ParameterRule(new ParameterRuleSpec(
                r.RuleId, r.Clause, r.Category.Trim(), r.Parameter.Trim(), cmp.Value, r.ThresholdMm, sev.Value, r.Status)));
        }

        foreach (var e in register.Errors.Where(e => !exclude.Contains(e.RuleId)))
            notRun.Add($"{(e.RuleId.Length == 0 ? "(row " + e.Line + ")" : e.RuleId)}: {e.Reason}");

        return new ParameterRuleSet(rules, notRun);
    }
}
