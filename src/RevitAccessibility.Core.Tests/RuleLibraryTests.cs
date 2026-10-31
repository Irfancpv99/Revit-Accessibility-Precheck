using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.RuleDefinitions;
using RevitAccessibility.Core.Rules;
using Xunit;
using static RevitAccessibility.Core.Tests.RuleDefinitionTests;

namespace RevitAccessibility.Core.Tests;

public sealed class RuleLibraryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ra-lib-" + Guid.NewGuid().ToString("N"));
    private readonly string _csv;

    public RuleLibraryTests()
    {
        Directory.CreateDirectory(_dir);
        _csv = Path.Combine(_dir, "register.csv");
        // Synthetic register: arbitrary values, no legal meaning.
        File.WriteAllText(_csv,
            "RuleID,Clause,Category,Parameter,Operator,Threshold,Unit,Severity,Status\n" +
            "REG-WALL-W,test clause,Walls,Width,>=,10,cm,Error,Needs review\n" +
            "REG-TBD,[TBD],Walls,Thickness,>=,[TBD],mm,Error,Placeholder\n");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Users() { var d = Path.Combine(_dir, "user"); Directory.CreateDirectory(d); return d; }

    private static ElementDto Wall(string uid, double? width) =>
        new(uid, "Walls", "L1", uid, new Dictionary<string, MeasurementDto> { ["Width"] = new(width) });

    [Fact] public void Register_only_when_no_user_folder()
    {
        var set = RuleLibrary.Load(_csv, null);
        Assert.Single(set.Rules);
        Assert.Contains(set.NotRun, n => n.RuleId == "REG-TBD");
        Assert.Empty(RuleLibrary.Load(_csv, Path.Combine(_dir, "does-not-exist")).Rules.Skip(1));
    }

    [Fact] public void Missing_register_is_an_error_not_an_empty_rule_set() =>
        Assert.ThrowsAny<IOException>(() => RuleLibrary.Load(Path.Combine(_dir, "nope.csv"), null));

    [Fact] public void User_rule_file_is_loaded_and_runs_alongside_the_register()
    {
        File.WriteAllText(Path.Combine(Users(), "mine.json"), RuleDefinitionJson.Serialize([Valid(x => x with { RuleId = "USER-LEN" })]));
        var set = RuleLibrary.Load(_csv, Users());
        Assert.Equal(2, set.Rules.Count);

        var findings = new RuleEngine(set.Rules).Run(new BuildingModel([], [Wall("a", 150)]));
        Assert.Equal(["REG-WALL-W", "USER-LEN"], findings.Select(f => f.RuleId).Order());
        Assert.All(findings, f => Assert.Equal(FindingStatus.Pass, f.Status));
    }

    [Fact] public void Malformed_user_file_is_reported_and_other_files_still_load()
    {
        File.WriteAllText(Path.Combine(Users(), "a-bad.json"), "{ not json");
        File.WriteAllText(Path.Combine(Users(), "b-good.json"), RuleDefinitionJson.Serialize([Valid(x => x with { RuleId = "USER-OK" })]));
        var set = RuleLibrary.Load(_csv, Users());
        Assert.Contains(set.NotRun, n => n.RuleId == "(file a-bad.json)");
        Assert.Contains(set.Rules, _ => true);
        Assert.Equal(2, set.Rules.Count);
    }

    [Fact] public void User_rule_cannot_replace_a_shipped_rule_with_the_same_id()
    {
        File.WriteAllText(Path.Combine(Users(), "dup.json"),
            RuleDefinitionJson.Serialize([Valid(x => x with { RuleId = "REG-WALL-W", Evaluation = x.Evaluation! with { Threshold = 1 } })]));
        var set = RuleLibrary.Load(_csv, Users());
        Assert.Single(set.Rules);
        Assert.Contains("Duplicate", set.NotRun.Single(n => n.RuleId == "REG-WALL-W").Reason);

        // the shipped threshold (100 mm) still applies: 50 mm must fail
        var f = Assert.Single(new RuleEngine(set.Rules).Run(new BuildingModel([], [Wall("a", 50)])));
        Assert.Equal(FindingStatus.Fail, f.Status);
    }

    [Fact] public void Unsupported_user_rule_is_listed_with_its_reason()
    {
        File.WriteAllText(Path.Combine(Users(), "geo.json"),
            RuleDefinitionJson.Serialize([Valid(x => x with { RuleId = "USER-GEO", RuleType = RuleType.Geometry })]));
        var set = RuleLibrary.Load(_csv, Users());
        Assert.Contains("EXE-RULETYPE", set.NotRun.Single(n => n.RuleId == "USER-GEO").Reason);
    }

    [Fact] public void Non_json_files_are_ignored() 
    {
        File.WriteAllText(Path.Combine(Users(), "notes.txt"), "{ not json");
        Assert.Single(RuleLibrary.Load(_csv, Users()).Rules);
    }

    [Fact] public void Banner_names_unverified_rules_that_ran_and_only_those()
    {
        File.WriteAllText(Path.Combine(Users(), "mine.json"), RuleDefinitionJson.Serialize([Valid(x => x with { RuleId = "USER-LEN" }), Valid(x => x with { RuleId = "USER-GEO", RuleType = RuleType.Geometry })]));
        var banner = RuleLibrary.UnverifiedBanner(RuleLibrary.Load(_csv, Users()));
        Assert.NotNull(banner);
        Assert.Contains("REG-WALL-W", banner);
        Assert.Contains("USER-LEN", banner);
        Assert.DoesNotContain("USER-GEO", banner);
    }

    [Fact] public void No_banner_when_nothing_ran()
    {
        File.WriteAllText(_csv, "RuleID,Clause,Category,Parameter,Operator,Threshold,Unit,Severity,Status\n");
        Assert.Null(RuleLibrary.UnverifiedBanner(RuleLibrary.Load(_csv, null)));
    }
}

public class ShippedExampleRuleTests
{
    [Fact] public void Test_only_wall_rule_file_loads_validates_and_runs_as_documented()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "test-model", "user-rule-wall-width.TEST-ONLY.json"))) dir = dir.Parent;
        var def = Assert.Single(RuleDefinitionJson.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "test-model", "user-rule-wall-width.TEST-ONLY.json"))));

        var report = RuleValidator.Validate(def);
        Assert.True(report.IsExecutable);
        Assert.Contains(report.GovernanceWarnings, w => w.Code == "GOV-UNVERIFIED");

        var set = RuleSetBuilder.Build([def]);
        var f = new RevitAccessibility.Core.Engine.RuleEngine(set.Rules).Run(new BuildingModel([],
            [Element("w200", 200), Element("w250", 250), Element("w300", 300), Element("none", null)]));
        var byUid = f.ToDictionary(x => x.ElementUid);
        Assert.Equal(FindingStatus.Fail, byUid["w200"].Status);
        Assert.Equal(FindingStatus.Pass, byUid["w250"].Status);
        Assert.Equal(FindingStatus.Pass, byUid["w300"].Status);
        Assert.Equal(FindingStatus.CannotEvaluate, byUid["none"].Status);
        Assert.All(f, x => Assert.Contains("UNVERIFIED", x.Message));
    }

    private static ElementDto Element(string uid, double? width) =>
        new(uid, "Walls", "L1", uid, new Dictionary<string, MeasurementDto> { ["Width"] = new(width) });
}
