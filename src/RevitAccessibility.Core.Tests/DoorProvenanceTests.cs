using RevitAccessibility.Core.Config;
using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;
using Xunit;

namespace RevitAccessibility.Core.Tests;

/// <summary>
/// The door rule must never silently treat a nominal width as the legal clear opening, and every finding
/// must say which parameter and meaning it used.
/// </summary>
public class DoorProvenanceTests
{
    private static readonly MeasurementInfo Nominal = new("Width", "type", "Revit length parameter converted to mm once", MeasurementMeaning.NominalProxy);
    private static readonly MeasurementInfo Clear = new("Clear Opening", "instance", "Revit length parameter converted to mm once", MeasurementMeaning.ClearOpening);

    private static DoorClearWidthRule Strict(string status = "Needs review") =>
        new(new DoorClearWidthThresholds(800, 750, "DM 236/1989 p. 8.1.1 [test]", verificationStatus: status));

    private static Finding Eval(DoorDto door, DoorClearWidthRule? rule = null) =>
        Assert.Single((rule ?? Strict()).Evaluate(new BuildingModel([door])));

    private static DoorDto Door(double? width, bool? access, MeasurementInfo? source, string? doc = "H") =>
        new("u1", "L1", width, access, "D1", ClearWidthSource: source, DocumentId: doc);

    // ---- nominal / undeclared widths are never judged ----

    [Theory]
    [InlineData(900.0)]   // comfortably above the minimum
    [InlineData(500.0)]   // far below the minimum
    [InlineData(800.0)]   // exactly at the minimum
    public void Nominal_width_is_needs_review_whatever_its_value(double width)
    {
        var f = Eval(Door(width, true, Nominal));
        Assert.Equal(FindingStatus.NeedsReview, f.Status);
        Assert.Contains("not the legal clear opening", f.Message);
        Assert.Contains("Width", f.Message);
    }

    [Fact]
    public void Undeclared_source_is_needs_review()
    {
        var f = Eval(Door(900, true, null));
        Assert.Equal(FindingStatus.NeedsReview, f.Status);
        Assert.Contains("undeclared source", f.Message);
    }

    [Fact]
    public void Unknown_meaning_is_needs_review() =>
        Assert.Equal(FindingStatus.NeedsReview,
            Eval(Door(900, true, new MeasurementInfo("X", "type", "m", MeasurementMeaning.Unknown))).Status);

    [Fact]
    public void Needs_review_message_is_indicative_and_never_says_meets_or_below_minimum_as_a_result()
    {
        var above = Eval(Door(900, true, Nominal));
        var below = Eval(Door(700, true, Nominal));
        Assert.Contains("Indicative only", above.Message);
        Assert.Contains("at or above", above.Message);
        Assert.Contains("below", below.Message);
        Assert.DoesNotContain("meets the minimum", above.Message);
    }

    [Fact]
    public void Needs_review_names_the_missing_input()
    {
        var f = Eval(Door(900, true, Nominal));
        Assert.Contains("declared clear-opening measurement", f.Detail!.MissingInputs!);
    }

    // ---- declared clear opening is judged normally ----

    [Fact]
    public void Declared_clear_opening_below_minimum_fails() => Assert.Equal(FindingStatus.Fail, Eval(Door(799, true, Clear)).Status);

    [Fact]
    public void Declared_clear_opening_at_minimum_passes() => Assert.Equal(FindingStatus.Pass, Eval(Door(800, true, Clear)).Status);

    [Fact]
    public void Declared_clear_opening_other_door_uses_the_other_threshold() =>
        Assert.Equal(FindingStatus.Pass, Eval(Door(750, false, Clear)).Status);

    // ---- missing data is still cannot-evaluate, before provenance matters ----

    [Fact]
    public void Unclassified_door_is_cannot_evaluate_even_with_nominal_source() =>
        Assert.Equal(FindingStatus.CannotEvaluate, Eval(Door(900, null, Nominal)).Status);

    [Fact]
    public void Missing_width_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, Eval(Door(null, true, Nominal)).Status);

    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_width_is_cannot_evaluate_under_the_strict_policy(double width) =>
        Assert.Equal(FindingStatus.CannotEvaluate, Eval(Door(width, true, Clear)).Status);

    [Fact]
    public void Cannot_evaluate_lists_what_is_missing()
    {
        Assert.Equal(["access-door classification"], Eval(Door(900, null, Clear)).Detail!.MissingInputs);
        Assert.Equal(["clear width"], Eval(Door(null, true, Clear)).Detail!.MissingInputs);
        Assert.Equal(["valid clear width"], Eval(Door(0, true, Clear)).Detail!.MissingInputs);
    }

    // ---- the explicit legacy mode ----

    [Fact]
    public void Trust_supplied_value_mode_judges_a_bare_width()
    {
        var rule = new DoorClearWidthRule(new DoorClearWidthThresholds(800, 750, "c"), WidthTrust.TrustSuppliedValue);
        Assert.Equal(FindingStatus.Fail, Eval(Door(700, true, null), rule).Status);
    }

    [Fact]
    public void Default_policy_is_the_safe_one()
    {
        // No policy argument: a bare width must NOT be judged.
        var rule = new DoorClearWidthRule(new DoorClearWidthThresholds(800, 750, "c"));
        Assert.Equal(FindingStatus.NeedsReview, Eval(Door(700, true, null), rule).Status);
    }

    // ---- provenance on findings ----

    [Fact]
    public void Pass_and_fail_report_value_unit_source_expectation_and_verification()
    {
        var f = Eval(Door(799.5, true, Clear, "DOC-1"));
        var d = f.Detail!;
        Assert.Equal(FindingStatus.Fail, f.Status);
        Assert.Equal(DoorClearWidthRule.Version, d.RuleVersion);
        Assert.Equal(CheckType.Regulatory, d.CheckType);
        Assert.Equal("DOC-1", d.SourceDocument);
        Assert.Equal(799.5, d.MeasuredValue);
        Assert.Equal("mm", d.Unit);
        Assert.Contains("Clear Opening", d.MeasurementSource);
        Assert.Contains("ClearOpening", d.MeasurementSource);
        Assert.Equal("at least 800 mm (access door)", d.ExpectedCondition);
        Assert.Equal("Needs review", d.VerificationStatus);
        Assert.Null(d.MissingInputs);
    }

    [Fact]
    public void Measured_value_is_not_display_rounded()
    {
        var d = Eval(Door(800.4999, true, Clear)).Detail!;
        Assert.Equal(800.4999, d.MeasuredValue);
    }

    [Fact]
    public void Verified_status_is_carried_through() =>
        Assert.Equal("Verified", Eval(Door(900, true, Clear), Strict("Verified")).Detail!.VerificationStatus);

    [Fact]
    public void Thresholds_from_register_report_the_register_status()
    {
        var csv = "RuleID,Jurisdiction,BuildingType,Clause,Category,Parameter,Operator,Threshold,Unit,Severity,Status,VerifiedBy_Architect,VerifiedBy_Professor,Date,Notes\n" +
                  "DOOR-CLEAR-W-ENTRY,IT,Public,DM 236,Doors,Clear Width,>=,80,cm,Error,Needs review,,,,n\n" +
                  "DOOR-CLEAR-W-OTHER,IT,Public,DM 236,Doors,Clear Width,>=,75,cm,Error,Needs review,,,,n\n";
        var t = DoorClearWidthThresholds.FromRegister(Register.RuleRegisterLoader.Load(csv));
        Assert.Equal("Needs review / Needs review", t.VerificationStatus);
        Assert.Contains("UNVERIFIED", Eval(Door(900, true, Clear), new DoorClearWidthRule(t)).Message);
    }

    // ---- engine ordering with the new status ----

    [Fact]
    public void Engine_orders_fail_then_needs_review_then_cannot_evaluate_then_pass()
    {
        var model = new BuildingModel(
        [
            new DoorDto("pass", "L1", 900, false, ClearWidthSource: Clear),
            new DoorDto("cannot", "L1", null, false, ClearWidthSource: Clear),
            new DoorDto("review", "L1", 900, false, ClearWidthSource: Nominal),
            new DoorDto("fail", "L1", 100, false, ClearWidthSource: Clear),
        ]);
        var order = new RuleEngine([Strict()]).Run(model).Select(f => f.Status).ToList();
        Assert.Equal([FindingStatus.Fail, FindingStatus.NeedsReview, FindingStatus.CannotEvaluate, FindingStatus.Pass], order);
    }

    [Fact]
    public void Needs_review_is_never_counted_as_a_pass()
    {
        var findings = new RuleEngine([Strict()]).Run(new BuildingModel([Door(900, true, Nominal)]));
        Assert.Equal(0, findings.Count(f => f.Status == FindingStatus.Pass));
        Assert.Equal(1, findings.Count(f => f.Status == FindingStatus.NeedsReview));
    }

    [Fact]
    public void Every_door_still_gets_exactly_one_finding()
    {
        var doors = new[]
        {
            Door(900, true, Clear), Door(700, true, Clear), Door(900, true, Nominal), Door(null, true, Clear), Door(900, null, Clear),
        }.Select((d, i) => d with { Uid = "u" + i }).ToList();
        Assert.Equal(5, Strict().Evaluate(new BuildingModel(doors)).Count());
    }
}

public class ClearWidthMeaningConfigTests
{
    [Fact]
    public void Default_is_nominal_proxy_so_results_need_review() =>
        Assert.Equal(MeasurementMeaning.NominalProxy, AddinConfig.Parse("{}").ClearWidthMeaning);

    [Fact]
    public void Owner_can_declare_a_clear_opening_parameter() =>
        Assert.Equal(MeasurementMeaning.ClearOpening, AddinConfig.Parse("{\"ClearWidthMeaning\":\"ClearOpening\"}").ClearWidthMeaning);

    [Fact]
    public void Meaning_is_case_insensitive() =>
        Assert.Equal(MeasurementMeaning.ClearOpening, AddinConfig.Parse("{\"clearwidthmeaning\":\"clearopening\"}").ClearWidthMeaning);

    [Theory]
    [InlineData("{\"ClearWidthMeaning\":\"Unknown\"}")]
    [InlineData("{\"ClearWidthMeaning\":\"Banana\"}")]
    [InlineData("{\"ClearWidthMeaning\":5}")]
    [InlineData("{\"ClearWidthMeaning\":\"\"}")]
    public void Invalid_meaning_is_an_error_not_a_silent_default(string json) =>
        Assert.Throws<InvalidDataException>(() => AddinConfig.Parse(json));
}
