using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Register;
using RevitAccessibility.Core.Rules;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class RuleRegisterLoaderTests
{
    private const string Header = "RuleID,Jurisdiction,BuildingType,Clause,Category,Parameter,Operator,Threshold,Unit,Severity,Status,VerifiedBy_Architect,VerifiedBy_Professor,Date,Notes\n";

    private static string Row(string id, string clause, string threshold, string unit, string status = "Needs review") =>
        $"{id},IT-National,Public,{clause},Doors,Clear Width,>=,{threshold},{unit},Error,{status},,,,note\n";

    [Fact]
    public void Centimetres_are_converted_to_millimetres()
    {
        var r = RuleRegisterLoader.Load(Header + Row("A", "DM 236", "80", "cm"));
        Assert.Empty(r.Errors);
        Assert.Equal(800, Assert.Single(r.Rules).ThresholdMm);
    }

    [Fact]
    public void Millimetres_are_not_scaled()
    {
        var r = RuleRegisterLoader.Load(Header + Row("A", "DM 236", "900", "mm"));
        Assert.Equal(900, Assert.Single(r.Rules).ThresholdMm);
    }

    [Fact]
    public void Decimal_values_use_invariant_culture()
    {
        var r = RuleRegisterLoader.Load(Header + Row("A", "DM 236", "7.5", "cm"));
        Assert.Equal(75, Assert.Single(r.Rules).ThresholdMm);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[TBD]")]
    [InlineData("eighty")]
    [InlineData("0")]
    [InlineData("-80")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Blank_or_non_numeric_threshold_is_rejected(string threshold)
    {
        var r = RuleRegisterLoader.Load(Header + Row("A", "DM 236", threshold, "cm"));
        Assert.Empty(r.Rules);
        Assert.Equal("A", Assert.Single(r.Errors).RuleId);
    }

    [Fact]
    public void Row_without_clause_is_rejected()
    {
        var r = RuleRegisterLoader.Load(Header + Row("A", "", "80", "cm"));
        Assert.Empty(r.Rules);
        Assert.Contains("clause", Assert.Single(r.Errors).Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_unit_is_rejected()
    {
        var r = RuleRegisterLoader.Load(Header + Row("A", "DM 236", "80", "inch"));
        Assert.Empty(r.Rules);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void Bad_rows_do_not_hide_good_rows()
    {
        var r = RuleRegisterLoader.Load(Header + Row("A", "DM 236", "80", "cm") + Row("B", "DM 236", "[TBD]", "mm"));
        Assert.Single(r.Rules);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void Quoted_fields_with_commas_and_quotes_are_parsed()
    {
        var csv = Header + "A,IT,Public,\"DM 236, p. 8.1.1 \"\"x\"\"\",Doors,W,>=,80,cm,Error,Verified,,,,\"note, with comma\"\n";
        var rule = Assert.Single(RuleRegisterLoader.Load(csv).Rules);
        Assert.Equal("DM 236, p. 8.1.1 \"x\"", rule.Clause);
        Assert.True(rule.IsVerified);
    }

    [Fact]
    public void Missing_required_column_throws()
    {
        Assert.Throws<FormatException>(() => RuleRegisterLoader.Load("RuleID,Clause\nA,x\n"));
    }

    [Fact]
    public void Empty_text_throws()
    {
        Assert.Throws<FormatException>(() => RuleRegisterLoader.Load(""));
    }

    [Fact]
    public void Crlf_line_endings_and_bom_are_handled()
    {
        var csv = "﻿" + (Header + Row("A", "DM 236", "80", "cm")).Replace("\n", "\r\n");
        Assert.Equal(800, Assert.Single(RuleRegisterLoader.Load(csv).Rules).ThresholdMm);
    }

    [Fact]
    public void Thresholds_from_register_give_800_and_750_and_are_labelled_unverified()
    {
        var csv = Header
            + Row(DoorClearWidthThresholds.EntryRuleId, "DM 236 p. 8.1.1", "80", "cm")
            + Row(DoorClearWidthThresholds.OtherRuleId, "DM 236 p. 8.1.1", "75", "cm");
        var t = DoorClearWidthThresholds.FromRegister(RuleRegisterLoader.Load(csv));
        Assert.Equal(800, t.AccessDoorMinMm);
        Assert.Equal(750, t.OtherDoorMinMm);
        Assert.NotNull(t.UnverifiedNote);

        var rule = new DoorClearWidthRule(t);
        var f = Assert.Single(rule.Evaluate(new BuildingModel([new DoorDto("u", "L0", 700, true)])));
        Assert.Contains("UNVERIFIED", f.Message);
    }

    [Fact]
    public void Verified_register_rows_carry_no_unverified_label()
    {
        var csv = Header
            + Row(DoorClearWidthThresholds.EntryRuleId, "DM 236", "80", "cm", "Verified")
            + Row(DoorClearWidthThresholds.OtherRuleId, "DM 236", "75", "cm", "Verified");
        var t = DoorClearWidthThresholds.FromRegister(RuleRegisterLoader.Load(csv));
        Assert.Null(t.UnverifiedNote);
    }

    [Fact]
    public void Thresholds_fail_loudly_when_a_register_row_is_missing_or_rejected()
    {
        var csv = Header
            + Row(DoorClearWidthThresholds.EntryRuleId, "DM 236", "80", "cm")
            + Row(DoorClearWidthThresholds.OtherRuleId, "DM 236", "[TBD]", "cm");
        Assert.Throws<InvalidOperationException>(() => DoorClearWidthThresholds.FromRegister(RuleRegisterLoader.Load(csv)));
    }

    [Fact]
    public void Real_register_file_loads_door_rules_and_rejects_tbd_placeholders()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "rules", "rule-register.csv")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var r = RuleRegisterLoader.LoadFile(Path.Combine(dir!.FullName, "rules", "rule-register.csv"));
        Assert.Equal(800, r.Find(DoorClearWidthThresholds.EntryRuleId)!.ThresholdMm);
        Assert.Equal(750, r.Find(DoorClearWidthThresholds.OtherRuleId)!.ThresholdMm);
        Assert.Equal(3, r.Errors.Count); // CORR-W, RAMP-SLOPE, THRESH-H are [TBD]
    }
}
