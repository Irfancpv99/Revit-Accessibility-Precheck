// All widths and thresholds are in millimetres: 80 cm = 800 mm, 75 cm = 750 mm.
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class DoorClearWidthRuleTests
{
    private static DoorClearWidthRule Rule() => new(new DoorClearWidthThresholds(800, 750, "DM 236/1989 p. 8.1.1 [test]"));

    private static BuildingModel One(double? width, bool? access) =>
        new([new DoorDto("u1", "L0", width, access)]);

    private static FindingStatus StatusOf(double? width, bool? access) =>
        Assert.Single(Rule().Evaluate(One(width, access))).Status;

    // Access door: 800 mm minimum (80 cm)
    [Fact] public void Access_door_799_mm_fails() => Assert.Equal(FindingStatus.Fail, StatusOf(799, true));
    [Fact] public void Access_door_exactly_800_mm_passes_boundary() => Assert.Equal(FindingStatus.Pass, StatusOf(800, true));
    [Fact] public void Access_door_801_mm_passes() => Assert.Equal(FindingStatus.Pass, StatusOf(801, true));

    // Other door: 750 mm minimum (75 cm)
    [Fact] public void Other_door_749_mm_fails() => Assert.Equal(FindingStatus.Fail, StatusOf(749, false));
    [Fact] public void Other_door_exactly_750_mm_passes_boundary() => Assert.Equal(FindingStatus.Pass, StatusOf(750, false));

    [Fact]
    public void Width_790_mm_fails_for_access_door_but_passes_for_other_door()
    {
        Assert.Equal(FindingStatus.Fail, StatusOf(790, true));
        Assert.Equal(FindingStatus.Pass, StatusOf(790, false));
    }

    // Missing data must never pass
    [Fact] public void Missing_width_is_cannot_evaluate_not_pass() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(null, true));
    [Fact] public void Unclassified_door_is_cannot_evaluate_even_if_width_is_fine() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(1000, null));
    [Fact] public void Unclassified_door_with_no_width_yields_one_finding() => Assert.Single(Rule().Evaluate(One(null, null)));
    [Fact] public void Zero_width_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(0, false));
    [Fact] public void Negative_width_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(-800, true));
    [Fact] public void NaN_width_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(double.NaN, true));
    [Fact] public void Infinite_width_is_cannot_evaluate() => Assert.Equal(FindingStatus.CannotEvaluate, StatusOf(double.PositiveInfinity, false));

    // Every door produces exactly one finding
    [Fact] public void Empty_model_yields_no_findings() => Assert.Empty(Rule().Evaluate(new BuildingModel([])));

    [Fact]
    public void Each_door_yields_exactly_one_finding_mixed_list()
    {
        var model = new BuildingModel(
        [
            new DoorDto("a", "L0", 700, true),   // Fail (700 < 800)
            new DoorDto("b", "L0", 800, true),   // Pass
            new DoorDto("c", "L1", null, false), // CannotEvaluate (no width)
            new DoorDto("d", "L1", 900, null),   // CannotEvaluate (not classified)
            new DoorDto("e", "L2", 740, false),  // Fail (740 < 750)
        ]);
        var findings = Rule().Evaluate(model).ToList();
        Assert.Equal(5, findings.Count);
        Assert.Equal(2, findings.Count(f => f.Status == FindingStatus.Fail));
        Assert.Equal(1, findings.Count(f => f.Status == FindingStatus.Pass));
        Assert.Equal(2, findings.Count(f => f.Status == FindingStatus.CannotEvaluate));
    }

    [Fact]
    public void Findings_carry_the_clause_reference_rule_id_and_element_uid()
    {
        var f = Assert.Single(Rule().Evaluate(One(700, true)));
        Assert.Equal("DM 236/1989 p. 8.1.1 [test]", f.ClauseRef);
        Assert.Equal("u1", f.ElementUid);
        Assert.Equal("DOOR-CLEAR-W", f.RuleId);
    }

    // Thresholds must be valid
    [Fact] public void Zero_threshold_is_rejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new DoorClearWidthThresholds(0, 750, "x"));
    [Fact] public void Blank_clause_reference_is_rejected() => Assert.Throws<ArgumentException>(() => new DoorClearWidthThresholds(800, 750, " "));

    // Reasons for missing data are shown to the user.
    [Fact]
    public void Access_door_note_is_included_when_door_is_unclassified()
    {
        var model = new BuildingModel([new DoorDto("u1", "L0", 900, null, "D1", AccessDoorNote: "Parameter 'Access door' has no value set.")]);
        var f = Assert.Single(Rule().Evaluate(model));
        Assert.Equal(FindingStatus.CannotEvaluate, f.Status);
        Assert.Contains("Parameter 'Access door' has no value set.", f.Message);
    }

    [Fact]
    public void Clear_width_note_is_included_when_width_is_missing()
    {
        var model = new BuildingModel([new DoorDto("u1", "L0", null, true, "D1", ClearWidthNote: "Parameter 'Width' not found.")]);
        var f = Assert.Single(Rule().Evaluate(model));
        Assert.Equal(FindingStatus.CannotEvaluate, f.Status);
        Assert.Contains("Parameter 'Width' not found.", f.Message);
    }

    [Fact]
    public void Notes_never_turn_a_missing_value_into_a_pass()
    {
        var model = new BuildingModel([new DoorDto("u1", "L0", null, null, null, "a", "b")]);
        Assert.Equal(FindingStatus.CannotEvaluate, Assert.Single(Rule().Evaluate(model)).Status);
    }
}
