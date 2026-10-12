using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;

namespace RevitAccessibility.Core.Engine;

public sealed class RuleEngine
{
    private readonly IReadOnlyList<IRule> _rules;

    public RuleEngine(IEnumerable<IRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.ToList();
    }

    public IReadOnlyList<Finding> Run(BuildingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return _rules
            .SelectMany(r => r.Evaluate(model))
            .OrderByDescending(f => f.Status == FindingStatus.Fail)
            .ThenByDescending(f => f.Status == FindingStatus.CannotEvaluate)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.ElementUid, StringComparer.Ordinal)
            .ToList();
    }
}
