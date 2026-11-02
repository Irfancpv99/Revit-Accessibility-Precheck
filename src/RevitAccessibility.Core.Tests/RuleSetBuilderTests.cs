using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Register;
using RevitAccessibility.Core.Reporting;
using RevitAccessibility.Core.RuleDefinitions;
using RevitAccessibility.Core.Rules;
using Xunit;
using static RevitAccessibility.Core.Tests.RuleDefinitionTests;

namespace RevitAccessibility.Core.Tests;

public class RuleSetBuilderTests
{
    private static ElementDto Wall(string uid, double? widthMm, string? note = null) =>
        new(uid, "Walls", "L1", uid, new Dictionary<string, MeasurementDto> { ["Width"] = new(widthMm, note) });

    private static IReadOnlyList<Finding> Run(RuleSetBuildResult set, BuildingModel model) => new RuleEngine(set.Rules).Run(model);

    [Fact] public void Executable_definition_becomes_a_rule_that_runs()
    {
        var set = RuleSetBuilder.Build([Valid()]);
        Assert.Empty(set.NotRun);
        var f = Run(set, new BuildingModel([], [Wall("w1", 150), Wall("w2", 99)]));
        Assert.Equal([FindingStatus.Fail, FindingStatus.Pass], f.Select(x => x.Status));
        Assert.All(f, x => Assert.Contains("UNVERIFIED", x.Message)); // never presented as approved
    }

    [Fact] public void Invalid_and_unsupported_definitions_are_reported_not_dropped()
    {
        var bad = Valid(x => x with { RuleId = "TEST-BAD", Evaluation = x.Evaluation! with { Threshold = null } });
        var unsupported = Valid(x => x with { RuleId = "TEST-GEO", RuleType = RuleType.Geometry });
        var set = RuleSetBuilder.Build([bad, unsupported, Valid()]);

        Assert.Single(set.Rules);
        Assert.Equal(["TEST-BAD", "TEST-GEO"], set.NotRun.Select(n => n.RuleId).Order());
        Assert.Contains("SCH-THRESHOLD", set.NotRun.Single(n => n.RuleId == "TEST-BAD").Reason);
        Assert.Contains("EXE-RULETYPE", set.NotRun.Single(n => n.RuleId == "TEST-GEO").Reason);
        Assert.Equal(3, set.Reports.Count);
    }

    [Fact] public void Unsupported_rule_never_produces_findings()
    {
        var set = RuleSetBuilder.Build([Valid(x => x with { RuleType = RuleType.Calculation })]);
        Assert.Empty(set.Rules);
        Assert.Empty(Run(set, new BuildingModel([], [Wall("w1", 5000)])));
        Assert.Single(set.NotRun);
    }

    [Fact] public void Duplicate_rule_id_is_reported()
    {
        var set = RuleSetBuilder.Build([Valid(), Valid()]);
        Assert.Single(set.Rules);
        Assert.Contains("Duplicate", Assert.Single(set.NotRun).Reason);
    }

    [Fact] public void Missing_data_is_cannot_evaluate_never_pass_and_every_element_gets_one_finding()
    {
        var set = RuleSetBuilder.Build([Valid()]);
        var model = new BuildingModel([], [Wall("a", null, "not set"), Wall("b", 0), Wall("c", double.NaN), Wall("d", 120), Wall("e", 10)]);
        var f = Run(set, model);
        Assert.Equal(5, f.Count);
        Assert.Equal(3, f.Count(x => x.Status == FindingStatus.CannotEvaluate));
        Assert.Equal(1, f.Count(x => x.Status == FindingStatus.Pass));
        Assert.Equal(1, f.Count(x => x.Status == FindingStatus.Fail));
    }

    [Fact] public void Empty_model_gives_no_findings() =>
        Assert.Empty(Run(RuleSetBuilder.Build([Valid()]), new BuildingModel([])));

    [Fact] public void Centimetre_threshold_is_converted_to_millimetres_once()
    {
        var d = Valid(x => x with { Evaluation = new RuleEvaluation(">=", 8, "cm") });
        var f = Run(RuleSetBuilder.Build([d]), new BuildingModel([], [Wall("a", 80), Wall("b", 79.9)]));
        Assert.Equal([FindingStatus.Fail, FindingStatus.Pass], f.Select(x => x.Status));
        Assert.Contains("80", f.Single(x => x.Status == FindingStatus.Pass).Message);
    }

    // ---- unpaired door rule ----

    [Fact] public void Unpaired_door_definition_is_not_run()
    {
        var set = RuleSetBuilder.Build([Door("TEST-D-A", "yes", 800)]);
        Assert.Empty(set.Rules);
        Assert.Contains("exactly one", Assert.Single(set.NotRun).Reason);
    }

    [Fact] public void Paired_door_definitions_build_one_door_rule_that_keeps_all_safeguards()
    {
        var set = RuleSetBuilder.Build([Door("TEST-D-A", "yes", 800), Door("TEST-D-O", "no", 750)]);
        Assert.Empty(set.NotRun);
        var rule = Assert.IsType<DoorClearWidthRule>(Assert.Single(set.Rules));

        var model = new BuildingModel([
            new DoorDto("unclassified", "L1", 900, null),
            new DoorDto("nominal", "L1", 900, true, ClearWidthSource: new("Width", "type", "m", MeasurementMeaning.NominalProxy)),
            new DoorDto("clear-ok", "L1", 800, true, ClearWidthSource: new("Clear", "instance", "m", MeasurementMeaning.ClearOpening)),
            new DoorDto("clear-low", "L1", 749, false, ClearWidthSource: new("Clear", "instance", "m", MeasurementMeaning.ClearOpening)),
        ]);
        var byUid = rule.Evaluate(model).ToDictionary(f => f.ElementUid);
        Assert.Equal(FindingStatus.CannotEvaluate, byUid["unclassified"].Status);
        Assert.Equal(FindingStatus.NeedsReview, byUid["nominal"].Status);
        Assert.Equal(FindingStatus.Pass, byUid["clear-ok"].Status);
        Assert.Equal(FindingStatus.Fail, byUid["clear-low"].Status);
    }

    // ---- the existing register behaves identically through the new path ----

    private static string RegisterPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "rules", "rule-register.csv"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "rules", "rule-register.csv");
    }

    [Fact] public void Existing_register_door_rows_give_findings_identical_to_the_legacy_path()
    {
        var register = RuleRegisterLoader.LoadFile(RegisterPath());
        var legacy = new DoorClearWidthRule(DoorClearWidthThresholds.FromRegister(register));

        var adapted = LegacyRegisterAdapter.Adapt(register);
        var set = RuleSetBuilder.Build(adapted.Definitions, alreadyNotRun: adapted.NotRun);
        var viaDefinitions = Assert.Single(set.Rules.OfType<DoorClearWidthRule>());

        var clear = new MeasurementInfo("Clear", "instance", "Revit length parameter converted to mm once", MeasurementMeaning.ClearOpening);
        var nominal = new MeasurementInfo("Width", "type", "Revit length parameter converted to mm once", MeasurementMeaning.NominalProxy);
        var model = new BuildingModel([
            new DoorDto("a", "L1", 800, true, ClearWidthSource: clear), new DoorDto("b", "L1", 799, true, ClearWidthSource: clear),
            new DoorDto("c", "L1", 750, false, ClearWidthSource: clear), new DoorDto("d", "L1", 749, false, ClearWidthSource: clear),
            new DoorDto("e", "L1", 900, true, ClearWidthSource: nominal), new DoorDto("f", "L1", null, true),
            new DoorDto("g", "L1", 900, null), new DoorDto("h", "L1", 0, false, ClearWidthSource: clear),
        ]);

        Assert.Equal(legacy.Evaluate(model).ToList(), viaDefinitions.Evaluate(model).ToList(), new FindingComparer());
    }

    [Fact] public void Existing_register_placeholder_rows_are_reported_as_not_run_with_reasons()
    {
        var register = RuleRegisterLoader.LoadFile(RegisterPath());
        var adapted = LegacyRegisterAdapter.Adapt(register);
        var set = RuleSetBuilder.Build(adapted.Definitions, alreadyNotRun: adapted.NotRun);

        var ids = set.NotRun.Select(n => n.RuleId).ToHashSet();
        Assert.Contains("CORR-W", ids);
        Assert.Contains("RAMP-SLOPE", ids);
        Assert.Contains("THRESH-H", ids);
        Assert.DoesNotContain("DOOR-CLEAR-W-ENTRY", ids);
        Assert.Empty(set.Rules.OfType<ParameterRule>()); // no placeholder rule can execute
    }

    [Fact] public void Existing_register_door_definitions_are_executable_but_unverified()
    {
        var adapted = LegacyRegisterAdapter.Adapt(RuleRegisterLoader.LoadFile(RegisterPath()));
        foreach (var d in adapted.Definitions.Where(d => d.RuleId!.StartsWith("DOOR-CLEAR-W")))
        {
            var r = RuleValidator.Validate(d);
            Assert.True(r.IsExecutable);
            Assert.Contains(r.GovernanceWarnings, i => i.Code == "GOV-UNVERIFIED");
            Assert.Contains(r.GovernanceWarnings, i => i.Code == "GOV-APPLICABILITY"); // the CSV does not hold it; nothing invented
            Assert.Equal(VerificationState.NeedsReview, d.Verification!.Status);
        }
    }

    [Fact] public void Adapter_reports_unknown_severity_instead_of_defaulting()
    {
        var csv = "RuleID,Clause,Category,Parameter,Operator,Threshold,Unit,Severity,Status\nX-1,cl,Walls,Width,>=,10,cm,Critical,Needs review\n";
        var adapted = LegacyRegisterAdapter.Adapt(RuleRegisterLoader.Load(csv));
        Assert.Empty(adapted.Definitions);
        Assert.Contains("Severity", Assert.Single(adapted.NotRun).Reason);
    }

    // ---- results reach the CSV with provenance ----

    [Fact] public void Findings_from_definitions_export_with_version_source_and_disclaimer()
    {
        var set = RuleSetBuilder.Build([Door("TEST-D-A", "yes", 800), Door("TEST-D-O", "no", 750)]);
        var model = new BuildingModel([new DoorDto("u1", "L1", 790, true, "=cmd", ClearWidthSource: new("Clear", "instance", "m", MeasurementMeaning.ClearOpening), DocumentId: "doc")]);
        var csv = FindingCsvExporter.Export(new RuleEngine(set.Rules).Run(model), model);
        Assert.Contains(Disclaimer.Text, csv);
        Assert.Contains("\"Fail\"", csv);
        Assert.Contains("\"790\"", csv);
        Assert.Contains("\"'=cmd\"", csv);
        Assert.Contains("p. X [test]", csv);
    }

    private sealed class FindingComparer : IEqualityComparer<Finding>
    {
        public bool Equals(Finding? a, Finding? b) => a == b || (a is not null && b is not null
            && a.RuleId == b.RuleId && a.ClauseRef == b.ClauseRef && a.Severity == b.Severity && a.Status == b.Status
            && a.ElementUid == b.ElementUid && a.Message == b.Message
            && a.Detail?.RuleVersion == b.Detail?.RuleVersion && a.Detail?.MeasuredValue == b.Detail?.MeasuredValue
            && a.Detail?.MeasurementSource == b.Detail?.MeasurementSource && a.Detail?.ExpectedCondition == b.Detail?.ExpectedCondition
            && a.Detail?.VerificationStatus == b.Detail?.VerificationStatus
            && (a.Detail?.MissingInputs ?? []).SequenceEqual(b.Detail?.MissingInputs ?? []));
        public int GetHashCode(Finding f) => HashCode.Combine(f.RuleId, f.ElementUid, f.Status);
    }
}
