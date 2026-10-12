using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.Rules;

public interface IRule
{
    string Id { get; }
    IEnumerable<Finding> Evaluate(BuildingModel model);
}
