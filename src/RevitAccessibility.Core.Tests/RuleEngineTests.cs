using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class RuleEngineTests
{
    private static DoorClearWidthRule Rule() => new(new DoorClearWidthThresholds(800, 750, "DM 236/1989 p. 8.1.1 [test]"), WidthTrust.TrustSuppliedValue);

    [Fact]
    public void Engine_orders_Fail_before_CannotEvaluate_before_Pass()
    {
        var engine = new RuleEngine([Rule()]);
        var model = new BuildingModel(
        [
            new DoorDto("p", "L0", 900, false),
            new DoorDto("c", "L0", null, false),
            new DoorDto("f", "L0", 500, false),
        ]);
        var ordered = engine.Run(model).Select(f => f.Status).ToList();
        Assert.Equal(FindingStatus.Fail, ordered[0]);
        Assert.Equal(FindingStatus.CannotEvaluate, ordered[1]);
        Assert.Equal(FindingStatus.Pass, ordered[2]);
    }

    [Fact]
    public void Engine_rejects_null_rules_and_null_model()
    {
        Assert.Throws<ArgumentNullException>(() => new RuleEngine(null!));
        Assert.Throws<ArgumentNullException>(() => new RuleEngine([Rule()]).Run(null!));
    }
}
