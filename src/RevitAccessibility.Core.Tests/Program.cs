// Minimal test runner with no external packages, because NuGet is unreachable in the build environment.
// Migrate to xUnit (dotnet new xunit) once packages can be restored.
// All widths and thresholds are in millimetres: 80 cm = 800 mm, 75 cm = 750 mm.
using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;

int passed = 0, failed = 0;

void Test(string name, Action body)
{
    try { body(); passed++; Console.WriteLine($"PASS  {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL  {name}\n      {ex.Message}"); }
}

static void Equal<T>(T expected, T actual, string what = "value")
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{what}: expected {expected}, got {actual}");
}

static Finding Single(IEnumerable<Finding> findings)
{
    var list = findings.ToList();
    if (list.Count != 1) throw new Exception($"expected 1 finding, got {list.Count}");
    return list[0];
}

static DoorClearWidthRule Rule() => new(new DoorClearWidthThresholds(800, 750, "DM 236/1989 p. 8.1.1 [test]"));
static BuildingModel One(double? width, bool? access) =>
    new([new DoorDto("u1", "L0", width, access)]);
static FindingStatus StatusOf(double? width, bool? access) => Single(Rule().Evaluate(One(width, access))).Status;

// Access door: 800 mm minimum (80 cm)
Test("access door 799 mm fails", () => Equal(FindingStatus.Fail, StatusOf(799, true)));
Test("access door exactly 800 mm passes (boundary)", () => Equal(FindingStatus.Pass, StatusOf(800, true)));
Test("access door 801 mm passes", () => Equal(FindingStatus.Pass, StatusOf(801, true)));

// Other door: 750 mm minimum (75 cm)
Test("other door 749 mm fails", () => Equal(FindingStatus.Fail, StatusOf(749, false)));
Test("other door exactly 750 mm passes (boundary)", () => Equal(FindingStatus.Pass, StatusOf(750, false)));

// Same width, different classification
Test("790 mm fails for access door but passes for other door", () =>
{
    Equal(FindingStatus.Fail, StatusOf(790, true), "access");
    Equal(FindingStatus.Pass, StatusOf(790, false), "other");
});

// Missing data must never pass
Test("missing width is CannotEvaluate, not Pass", () => Equal(FindingStatus.CannotEvaluate, StatusOf(null, true)));
Test("unclassified door is CannotEvaluate, even if width is fine", () => Equal(FindingStatus.CannotEvaluate, StatusOf(1000, null)));
Test("unclassified door with no width yields one CannotEvaluate, not two", () => Equal(1, Rule().Evaluate(One(null, null)).Count()));
Test("zero width is CannotEvaluate", () => Equal(FindingStatus.CannotEvaluate, StatusOf(0, false)));
Test("negative width is CannotEvaluate", () => Equal(FindingStatus.CannotEvaluate, StatusOf(-800, true)));
Test("NaN width is CannotEvaluate", () => Equal(FindingStatus.CannotEvaluate, StatusOf(double.NaN, true)));
Test("infinite width is CannotEvaluate", () => Equal(FindingStatus.CannotEvaluate, StatusOf(double.PositiveInfinity, false)));

// Every door produces exactly one finding
Test("empty model yields no findings", () => Equal(0, Rule().Evaluate(new BuildingModel([])).Count()));
Test("each door yields exactly one finding (mixed list)", () =>
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
    Equal(5, findings.Count, "count");
    Equal(2, findings.Count(f => f.Status == FindingStatus.Fail), "fail");
    Equal(1, findings.Count(f => f.Status == FindingStatus.Pass), "pass");
    Equal(2, findings.Count(f => f.Status == FindingStatus.CannotEvaluate), "cannot");
});
Test("findings carry the clause reference, rule id and element uid", () =>
{
    var f = Single(Rule().Evaluate(One(700, true)));
    Equal("DM 236/1989 p. 8.1.1 [test]", f.ClauseRef, "clause");
    Equal("u1", f.ElementUid, "uid");
    Equal("DOOR-CLEAR-W", f.RuleId, "rule");
});

// Thresholds must be valid
Test("zero threshold is rejected", () =>
{
    try { _ = new DoorClearWidthThresholds(0, 750, "x"); throw new Exception("no exception"); }
    catch (ArgumentOutOfRangeException) { }
});
Test("blank clause reference is rejected", () =>
{
    try { _ = new DoorClearWidthThresholds(800, 750, " "); throw new Exception("no exception"); }
    catch (ArgumentException) { }
});

// Engine
Test("engine orders Fail before CannotEvaluate before Pass", () =>
{
    var engine = new RuleEngine([Rule()]);
    var model = new BuildingModel(
    [
        new DoorDto("p", "L0", 900, false),
        new DoorDto("c", "L0", null, false),
        new DoorDto("f", "L0", 500, false),
    ]);
    var ordered = engine.Run(model).Select(f => f.Status).ToList();
    Equal(FindingStatus.Fail, ordered[0], "first");
    Equal(FindingStatus.CannotEvaluate, ordered[1], "second");
    Equal(FindingStatus.Pass, ordered[2], "third");
});
Test("engine rejects null rules and null model", () =>
{
    try { _ = new RuleEngine(null!); throw new Exception("no exception"); } catch (ArgumentNullException) { }
    try { _ = new RuleEngine([Rule()]).Run(null!); throw new Exception("no exception"); } catch (ArgumentNullException) { }
});

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
