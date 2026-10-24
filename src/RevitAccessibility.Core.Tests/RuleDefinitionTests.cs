using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Register;
using RevitAccessibility.Core.Reporting;
using RevitAccessibility.Core.RuleDefinitions;
using RevitAccessibility.Core.Rules;
using Xunit;

namespace RevitAccessibility.Core.Tests;

/// <summary>SYNTHETIC definitions: thresholds here are arbitrary test values with no legal meaning.</summary>
public class RuleDefinitionTests
{
    internal static RuleDefinition Valid(Func<RuleDefinition, RuleDefinition>? change = null)
    {
        var d = new RuleDefinition(
            SchemaVersion: 1, RuleId: "TEST-LEN-1", RuleVersion: 1, Name: "Synthetic length rule",
            Domain: RuleDomain.Architectural, RuleType: RuleType.Dimension, SourceType: SourceType.TestOnly,
            ElementCategories: ["Walls"],
            Applicability: new RuleApplicability("TEST", "Test", Assumptions: ["synthetic"]),
            Measurement: new RuleMeasurement("w", RuleCapabilities.LengthParameter, "Width", "mm"),
            Evaluation: new RuleEvaluation(">=", 100, "mm"),
            Source: new RuleSourceRef("synthetic test"),
            Verification: new RuleVerification(),
            Lifecycle: new RuleLifecycle(LifecycleStatus.Draft, ["T1"]));
        return change is null ? d : change(d);
    }

    private static RuleValidationReport V(RuleDefinition d) => RuleValidator.Validate(d);

    // ---- the three problem categories are separate ----

    [Fact]
    public void Valid_rule_is_schema_valid_and_executable_but_still_unverified()
    {
        var r = V(Valid());
        Assert.True(r.IsSchemaValid);
        Assert.True(r.IsExecutable);
        Assert.Empty(r.SchemaErrors);
        Assert.Empty(r.ExecutionErrors);
        Assert.Contains(r.GovernanceWarnings, i => i.Code == "GOV-UNVERIFIED");
        Assert.Contains(r.GovernanceWarnings, i => i.Code == "GOV-NOT-APPROVED");
    }

    [Fact]
    public void Legally_documented_but_not_executable()
    {
        var d = Valid(x => x with
        {
            SourceType = SourceType.Legislation, RuleType = RuleType.Calculation,
            Source = new RuleSourceRef("Some decree", Clause: "art. 1"),
        });
        var r = V(d);
        Assert.True(r.IsSchemaValid);
        Assert.False(r.IsExecutable);
        Assert.Contains(r.ExecutionErrors, i => i.Code == "EXE-RULETYPE");
    }

    [Fact]
    public void Executable_but_governance_incomplete()
    {
        var r = V(Valid(x => x with { Applicability = null, Lifecycle = null }));
        Assert.True(r.IsExecutable);
        var codes = r.GovernanceWarnings.Select(i => i.Code).ToList();
        Assert.Contains("GOV-APPLICABILITY", codes);
        Assert.Contains("GOV-NO-TESTS", codes);
    }

    // ---- schema errors ----

    [Fact] public void Null_definition_is_a_schema_error() =>
        Assert.Equal("SCH-NULL", Assert.Single(RuleValidator.Validate(null).SchemaErrors).Code);

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  ")] [InlineData("lower-case")] [InlineData("A")] [InlineData("HAS SPACE")]
    public void Malformed_rule_id(string? id) =>
        Assert.Contains(V(Valid(x => x with { RuleId = id })).SchemaErrors, i => i.Code == "SCH-ID");

    [Fact] public void Unsupported_schema_version() =>
        Assert.Contains(V(Valid(x => x with { SchemaVersion = 2 })).SchemaErrors, i => i.Code == "SCH-VERSION");

    [Fact] public void Rule_version_must_be_positive() =>
        Assert.Contains(V(Valid(x => x with { RuleVersion = 0 })).SchemaErrors, i => i.Code == "SCH-RULE-VERSION");

    [Fact] public void Undefined_enum_values_are_rejected()
    {
        Assert.Contains(V(Valid(x => x with { Domain = (RuleDomain)99 })).SchemaErrors, i => i.Code == "SCH-DOMAIN");
        Assert.Contains(V(Valid(x => x with { RuleType = (RuleType)99 })).SchemaErrors, i => i.Code == "SCH-RULE-TYPE");
        Assert.Contains(V(Valid(x => x with { SourceType = (SourceType)99 })).SchemaErrors, i => i.Code == "SCH-SOURCE-TYPE");
    }

    [Theory]
    [InlineData("=~")] [InlineData("")] [InlineData(null)] [InlineData("between")]
    public void Unknown_operator_is_a_schema_error(string? op) =>
        Assert.Contains(V(Valid(x => x with { Evaluation = x.Evaluation! with { Operator = op } })).SchemaErrors, i => i.Code == "SCH-OPERATOR");

    [Theory]
    [InlineData(null)] [InlineData(0.0)] [InlineData(-5.0)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Missing_or_invalid_threshold(double? t) =>
        Assert.Contains(V(Valid(x => x with { Evaluation = x.Evaluation! with { Threshold = t } })).SchemaErrors, i => i.Code == "SCH-THRESHOLD");

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("furlong")] [InlineData("m")]
    public void Unsupported_unit(string? unit) =>
        Assert.Contains(V(Valid(x => x with { Evaluation = x.Evaluation! with { ThresholdUnit = unit } })).SchemaErrors, i => i.Code == "SCH-UNIT");

    [Fact] public void Missing_sections_are_schema_errors()
    {
        var r = V(Valid(x => x with { Measurement = null, Evaluation = null, Source = null, ElementCategories = [] }));
        var codes = r.SchemaErrors.Select(i => i.Code).ToList();
        Assert.Contains("SCH-MEASUREMENT", codes);
        Assert.Contains("SCH-EVALUATION", codes);
        Assert.Contains("SCH-SOURCE", codes);
        Assert.Contains("SCH-CATEGORIES", codes);
        Assert.False(r.IsExecutable);
    }

    [Fact] public void Legislation_must_cite_a_clause()
    {
        var r = V(Valid(x => x with { SourceType = SourceType.Legislation, Source = new RuleSourceRef("A decree") }));
        Assert.Contains(r.SchemaErrors, i => i.Code == "SCH-CLAUSE");
    }

    [Fact] public void Project_requirement_needs_no_clause() =>
        Assert.True(V(Valid(x => x with { SourceType = SourceType.ProjectRequirement })).IsSchemaValid);

    [Fact] public void Verified_cannot_be_claimed_without_signoff()
    {
        var r = V(Valid(x => x with { Verification = new RuleVerification(VerificationState.Verified) }));
        Assert.Contains(r.SchemaErrors, i => i.Code == "SCH-VERIFIED-UNSIGNED");
        Assert.False(r.IsExecutable);
    }

    [Fact] public void Verified_with_full_signoff_has_no_unverified_warning()
    {
        var d = Valid(x => x with
        {
            SourceType = SourceType.Legislation, Source = new RuleSourceRef("Decree", Clause: "art. 1"),
            Verification = new RuleVerification(VerificationState.Verified, "Architect A", "Professor P", "2026-01-01"),
        });
        var r = V(d);
        Assert.True(r.IsSchemaValid);
        Assert.DoesNotContain(r.GovernanceWarnings, i => i.Code == "GOV-UNVERIFIED");
    }

    // ---- execution errors (well-formed but unsupported) ----

    [Theory]
    [InlineData(RuleType.Parameter)] [InlineData(RuleType.Geometry)] [InlineData(RuleType.Calculation)] [InlineData(RuleType.ManualReview)]
    public void Unimplemented_rule_types_do_not_run(RuleType t) =>
        Assert.Contains(V(Valid(x => x with { RuleType = t })).ExecutionErrors, i => i.Code == "EXE-RULETYPE");

    [Fact] public void Unknown_measurement_method_does_not_run()
    {
        var r = V(Valid(x => x with { Measurement = x.Measurement! with { Method = "clear-opening-geometry" } }));
        Assert.True(r.IsSchemaValid);
        Assert.False(r.IsExecutable);
        Assert.Contains(r.ExecutionErrors, i => i.Code == "EXE-METHOD");
    }

    [Theory] [InlineData(">")] [InlineData("<")] [InlineData("==")] [InlineData("!=")]
    public void Recognised_but_unimplemented_operator_does_not_run(string op)
    {
        var r = V(Valid(x => x with { Evaluation = x.Evaluation! with { Operator = op } }));
        Assert.True(r.IsSchemaValid);
        Assert.Contains(r.ExecutionErrors, i => i.Code == "EXE-OPERATOR");
    }

    [Fact] public void Length_parameter_needs_a_parameter_name() =>
        Assert.Contains(V(Valid(x => x with { Measurement = x.Measurement! with { Parameter = null } })).ExecutionErrors, i => i.Code == "EXE-PARAMETER");

    [Fact] public void Unsupported_category_does_not_run() =>
        Assert.Contains(V(Valid(x => x with { ElementCategories = ["Spaceships"] })).ExecutionErrors, i => i.Code == "EXE-CATEGORY");

    [Fact] public void Custom_tolerance_is_refused_not_ignored()
    {
        Assert.Contains(V(Valid(x => x with { Evaluation = x.Evaluation! with { ToleranceMm = 2.0 } })).ExecutionErrors, i => i.Code == "EXE-TOLERANCE");
        Assert.True(V(Valid(x => x with { Evaluation = x.Evaluation! with { ToleranceMm = MeasurementTolerance.Mm } })).IsExecutable);
    }

    [Fact] public void Deprecated_rule_does_not_run() =>
        Assert.Contains(V(Valid(x => x with { Lifecycle = new RuleLifecycle(LifecycleStatus.Deprecated, ["T"]) })).ExecutionErrors, i => i.Code == "EXE-DEPRECATED");

    // ---- door clear-width definition semantics ----

    internal static RuleDefinition Door(string id, string yesNo, double mm, MeasurementMeaning? meaning = MeasurementMeaning.ClearOpening,
        string status = "Needs review") =>
        new(1, id, 1, "Door", RuleDomain.Accessibility, RuleType.Dimension, SourceType.Legislation, ["Doors"],
            new RuleApplicability(RequiresClassification: new ClassificationCondition(RuleCapabilities.AccessDoorClassification, yesNo)),
            new RuleMeasurement("clear-width", RuleCapabilities.DoorClearWidth, null, "mm", meaning),
            new RuleEvaluation(">=", mm, "mm"), new RuleSourceRef("Test decree", Clause: "p. X [test]"),
            new RuleVerification(status == "Needs review" ? VerificationState.NeedsReview : VerificationState.Unverified),
            new RuleLifecycle(LifecycleStatus.Draft));

    [Fact] public void Door_definition_must_declare_clear_opening_meaning()
    {
        var r = V(Door("TEST-D-A", "yes", 800, meaning: MeasurementMeaning.NominalProxy));
        Assert.Contains(r.ExecutionErrors, i => i.Code == "EXE-MEANING");
        Assert.Contains(V(Door("TEST-D-A", "yes", 800, meaning: null)).ExecutionErrors, i => i.Code == "EXE-MEANING");
    }

    [Fact] public void Door_definition_must_state_classification()
    {
        var d = Door("TEST-D-A", "maybe", 800);
        Assert.Contains(V(d).ExecutionErrors, i => i.Code == "EXE-CLASSIFICATION");
        Assert.Contains(V(d with { Applicability = null }).ExecutionErrors, i => i.Code == "EXE-CLASSIFICATION");
    }

    // ---- JSON loading is strict ----

    [Fact] public void Json_round_trips()
    {
        var json = RuleDefinitionJson.Serialize([Valid()]);
        var back = RuleDefinitionJson.Parse(json);
        Assert.Equal("TEST-LEN-1", Assert.Single(back).RuleId);
        Assert.True(V(back[0]).IsExecutable);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"rules\":[null]}")]
    [InlineData("{\"rules\":[],\"extra\":1}")]
    public void Json_rejects_malformed_files(string json) =>
        Assert.Throws<RuleDefinitionFormatException>(() => RuleDefinitionJson.Parse(json));

    [Fact] public void Json_rejects_unknown_rule_fields_and_numeric_enums()
    {
        var good = RuleDefinitionJson.Serialize([Valid()]);
        Assert.Throws<RuleDefinitionFormatException>(() => RuleDefinitionJson.Parse(good.Replace("\"ruleId\"", "\"expression\": \"1+1\", \"ruleId\"")));
        Assert.Throws<RuleDefinitionFormatException>(() => RuleDefinitionJson.Parse(good.Replace("\"Dimension\"", "1")));
    }

    [Fact] public void Missing_nested_object_loads_then_fails_validation_without_crashing()
    {
        var json = "{\"rules\":[{\"schemaVersion\":1,\"ruleId\":\"TEST-X\",\"ruleVersion\":1,\"name\":\"n\",\"domain\":\"Accessibility\",\"ruleType\":\"Dimension\",\"sourceType\":\"TestOnly\"}]}";
        var r = V(RuleDefinitionJson.Parse(json).Single());
        Assert.False(r.IsExecutable);
        Assert.NotEmpty(r.SchemaErrors);
    }
}
