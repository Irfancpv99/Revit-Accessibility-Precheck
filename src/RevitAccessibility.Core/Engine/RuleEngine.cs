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

    // Fail first, then what needs a person, then what could not be evaluated, then passes.
    private static int Rank(FindingStatus s) => s switch
    {
        FindingStatus.Fail => 0,
        FindingStatus.NeedsReview => 1,
        FindingStatus.CannotEvaluate => 2,
        FindingStatus.Pass => 3,
        _ => 4,
    };

    public IReadOnlyList<Finding> Run(BuildingModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return _rules
            .SelectMany(r => r.Evaluate(model))
            .OrderBy(f => Rank(f.Status))
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.ElementUid, StringComparer.Ordinal)
            .ToList();
    }
}
