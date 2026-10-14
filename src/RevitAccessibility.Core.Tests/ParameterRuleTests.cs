using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Register;
using RevitAccessibility.Core.Rules;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class ParameterRuleTests
{
    private const string Param = "Threshold Height";

    private static ParameterRule Rule(Comparison cmp, double thresholdMm = 20, string status = "Verified", string category = "Doors") =>
        new(new ParameterRuleSpec("THRESH-H", "Test clause", category, Param, cmp, thresholdMm, Severity.Warning, status));

    private static ElementDto Element(string uid, double? valueMm, string category = "Doors", string? note = null) =>
        new(uid, category, "L0", "M-" + uid, new Dictionary<string, MeasurementDto> { [Param] = new(valueMm, note) });

    private static FindingStatus StatusOf(ParameterRule rule, double? value) =>
        Assert.Single(rule.Evaluate(new BuildingModel([], [Element("u1", value)]))).Status;

    // "At most" (for example a maximum threshold height)
    [Fact] public void AtMost_above_threshold_fails() => Assert.Equal(FindingStatus.Fail, StatusOf(Rule(Comparison.AtMost), 21));
    [Fact] public void AtMost_exactly_at_threshold_passes() => Assert.Equal(FindingStatus.Pass, StatusOf(Rule(Comparison.AtMost), 20));
    [Fact] public void AtMost_one_unit_above_fails() => Assert.Equal(FindingStatus.Fail, StatusOf(Rule(Comparison.AtMost), 20.001));
    [Fact] public void AtMost_below_threshold_passes() => Assert.Equal(FindingStatus.Pass, StatusOf(Rule(Comparison.AtMost), 5));

    // "At least"
    [Fact] public void AtLeast_below_threshold_fails() => Assert.Equal(FindingStatus.Fail, StatusOf(Rule(Comparison.AtLeast, 900), 899));
    [Fact] public void AtLeast_exactly_at_threshold_passes() => Assert.Equal(FindingStatus.Pass, StatusOf(Rule(Comparison.AtLeast, 900), 900));
    [Fact] public void AtLeast_float_noise_at_boundary_passes() => Assert.Equal(FindingStatus.Pass, StatusOf(Rule(Comparison.AtLeast, 900), 899.9999999));

    // Missing or invalid data is never a pass
    [Fact] public void Missing_value_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(Rule(Comparison.AtMost), null));
    [Fact] public void Zero_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(Rule(Comparison.AtMost), 0));
    [Fact] public void Negative_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(Rule(Comparison.AtMost), -5));
    [Fact] public void NaN_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(Rule(Comparison.AtMost), double.NaN));
    [Fact] public void Infinity_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(Rule(Comparison.AtMost), double.PositiveInfinity));

    [Fact]
    public void Unread_parameter_is_cannot_evaluate()
    {
        var e = new ElementDto("u1", "Doors", "L0", null, new Dictionary<string, MeasurementDto>());
        var f = Assert.Single(Rule(Comparison.AtMost).Evaluate(new BuildingModel([], [e])));
        Assert.Equal(FindingStatus.CannotEvaluate, f.Status);
    }

    [Fact]
    public void Measurement_note_is_shown()
    {
        var model = new BuildingModel([], [Element("u1", null, note: "Parameter not found on this element.")]);
        var f = Assert.Single(Rule(Comparison.AtMost).Evaluate(model));
        Assert.Contains("Parameter not found on this element.", f.Message);
    }

    // Structure
    [Fact] public void Empty_model_yields_no_findings() => Assert.Empty(Rule(Comparison.AtMost).Evaluate(new BuildingModel([])));

    [Fact]
    public void Other_categories_are_ignored_and_each_element_gets_one_finding()
    {
        var model = new BuildingModel([],
        [
            Element("a", 10), Element("b", 30), Element("c", null), Element("w", 99, category: "Windows"),
        ]);
        var findings = Rule(Comparison.AtMost).Evaluate(model).ToList();
        Assert.Equal(3, findings.Count);
        Assert.Equal(1, findings.Count(f => f.Status == FindingStatus.Pass));
        Assert.Equal(1, findings.Count(f => f.Status == FindingStatus.Fail));
        Assert.Equal(1, findings.Count(f => f.Status == FindingStatus.CannotEvaluate));
    }

    [Fact]
    public void Category_match_ignores_case() =>
        Assert.Equal(FindingStatus.Pass, Assert.Single(Rule(Comparison.AtMost, category: "doors")
            .Evaluate(new BuildingModel([], [Element("u1", 5)]))).Status);

    [Fact]
    public void Fail_uses_the_register_severity_and_clause()
    {
        var f = Assert.Single(Rule(Comparison.AtMost).Evaluate(new BuildingModel([], [Element("u1", 50)])));
        Assert.Equal(Severity.Warning, f.Severity);
        Assert.Equal("Test clause", f.ClauseRef);
        Assert.Equal("THRESH-H", f.RuleId);
        Assert.Equal("u1", f.ElementUid);
    }

    // Verification labelling (CLAUDE.md hard rule 1)
    [Fact]
    public void Unverified_rows_are_labelled_in_every_finding()
    {
        var rule = Rule(Comparison.AtMost, status: "Needs review");
        var model = new BuildingModel([], [Element("a", 10), Element("b", 30), Element("c", null)]);
        Assert.All(rule.Evaluate(model), f => Assert.Contains("UNVERIFIED", f.Message));
    }

    [Fact]
    public void Verified_rows_carry_no_label() =>
        Assert.DoesNotContain("UNVERIFIED", Assert.Single(Rule(Comparison.AtMost).Evaluate(new BuildingModel([], [Element("u", 10)]))).Message);

    [Fact] public void Zero_threshold_is_rejected() => Assert.Throws<ArgumentOutOfRangeException>(() => Rule(Comparison.AtMost, 0));
    [Fact] public void Blank_parameter_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new ParameterRule(new ParameterRuleSpec("R", "c", "Doors", " ", Comparison.AtMost, 1, Severity.Error, "Verified")));
}

public class ParameterRuleFactoryTests
{
    private const string Header = "RuleID,Jurisdiction,BuildingType,Clause,Category,Parameter,Operator,Threshold,Unit,Severity,Status,VerifiedBy_Architect,VerifiedBy_Professor,Date,Notes\n";

    private static string Row(string id, string threshold, string op = "<=", string unit = "mm", string severity = "Warning", string category = "Doors", string clause = "Clause X") =>
        $"{id},IT,Public,{clause},{category},Threshold Height,{op},{threshold},{unit},{severity},Needs review,,,,n\n";

    [Fact]
    public void Valid_rows_become_rules_with_values_in_mm()
    {
        var set = ParameterRuleFactory.FromRegister(RuleRegisterLoader.Load(Header + Row("A", "2", unit: "cm")), []);
        var rule = Assert.Single(set.Rules);
        Assert.Equal(20, rule.Spec.ThresholdMm);
        Assert.Equal(Comparison.AtMost, rule.Spec.Comparison);
        Assert.Empty(set.NotRun);
    }

    [Fact]
    public void Excluded_rule_ids_are_left_to_their_dedicated_rule()
    {
        var csv = Header + Row("DOOR-CLEAR-W-ENTRY", "800", ">=") + Row("A", "20");
        var set = ParameterRuleFactory.FromRegister(RuleRegisterLoader.Load(csv), [DoorClearWidthThresholds.EntryRuleId]);
        Assert.Equal("A", Assert.Single(set.Rules).Id);
    }

    [Fact]
    public void Unsupported_operator_is_reported_not_dropped()
    {
        var set = ParameterRuleFactory.FromRegister(RuleRegisterLoader.Load(Header + Row("A", "20", op: "==")), []);
        Assert.Empty(set.Rules);
        Assert.Contains("A", Assert.Single(set.NotRun));
    }

    [Fact]
    public void Unknown_severity_is_reported_not_dropped()
    {
        var set = ParameterRuleFactory.FromRegister(RuleRegisterLoader.Load(Header + Row("A", "20", severity: "Critical")), []);
        Assert.Empty(set.Rules);
        Assert.Single(set.NotRun);
    }

    [Fact]
    public void Rows_the_loader_rejected_are_reported()
    {
        var set = ParameterRuleFactory.FromRegister(RuleRegisterLoader.Load(Header + Row("A", "[TBD]")), []);
        Assert.Empty(set.Rules);
        Assert.Contains("A", Assert.Single(set.NotRun));
    }

    [Fact]
    public void Real_register_runs_no_generic_rules_yet_and_reports_the_tbd_rows()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "rules", "rule-register.csv"))) dir = dir.Parent;
        Assert.NotNull(dir);

        var reg = RuleRegisterLoader.LoadFile(Path.Combine(dir!.FullName, "rules", "rule-register.csv"));
        var set = ParameterRuleFactory.FromRegister(reg, [DoorClearWidthThresholds.EntryRuleId, DoorClearWidthThresholds.OtherRuleId]);
        Assert.Empty(set.Rules);
        Assert.Equal(3, set.NotRun.Count);
        Assert.Contains(set.NotRun, n => n.StartsWith("THRESH-H"));
    }
}

public class DoorToleranceTests
{
    private static FindingStatus Status(double width, bool access) =>
        Assert.Single(new DoorClearWidthRule(new DoorClearWidthThresholds(800, 750, "c"))
            .Evaluate(new BuildingModel([new DoorDto("u", "L0", width, access)]))).Status;

    [Fact] public void Float_noise_just_below_800_passes_as_access_door() => Assert.Equal(FindingStatus.Pass, Status(799.9999999, true));
    [Fact] public void A_thousandth_of_a_millimetre_below_still_fails() => Assert.Equal(FindingStatus.Fail, Status(799.999, true));
    [Fact] public void One_millimetre_below_fails() => Assert.Equal(FindingStatus.Fail, Status(799, true));
    [Fact] public void Float_noise_just_below_750_passes_as_other_door() => Assert.Equal(FindingStatus.Pass, Status(749.9999999, false));
}
