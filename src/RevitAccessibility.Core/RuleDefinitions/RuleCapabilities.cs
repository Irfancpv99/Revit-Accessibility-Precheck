using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.RuleDefinitions;

/// <summary>One measurement method the engine really implements.</summary>
public sealed record SupportedMethod(string Method, RuleType RuleType, IReadOnlySet<string> Operators, bool NeedsParameter);

/// <summary>
/// The allowlist of what can actually run. A definition that asks for anything outside it is not executable.
/// Add an entry here only together with the engine code and tests that implement it.
/// </summary>
public sealed class RuleCapabilities
{
    public const string LengthParameter = "length-parameter";
    public const string DoorClearWidth = "door-clear-width";
    public const string AccessDoorClassification = "access-door";

    private static readonly HashSet<string> GteLte = new(StringComparer.Ordinal) { ">=", "<=" };
    private static readonly HashSet<string> Gte = new(StringComparer.Ordinal) { ">=" };

    /// <summary>Operators the schema recognises. Recognised does not mean executable.</summary>
    public static readonly IReadOnlySet<string> KnownOperators =
        new HashSet<string>(StringComparer.Ordinal) { ">=", "<=", ">", "<", "==", "!=" };

    public static readonly IReadOnlySet<string> KnownUnits =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mm", "cm" };

    public IReadOnlyDictionary<string, SupportedMethod> Methods { get; } =
        new Dictionary<string, SupportedMethod>(StringComparer.Ordinal)
        {
            [LengthParameter] = new(LengthParameter, RuleType.Dimension, GteLte, NeedsParameter: true),
            [DoorClearWidth] = new(DoorClearWidth, RuleType.Dimension, Gte, NeedsParameter: false),
        };

    /// <summary>
    /// Categories the Revit adapter can read for length-parameter rules. KEEP IN SYNC with
    /// ModelSnapshotBuilder.SupportedCategories in the add-in (that project cannot be referenced from Core).
    /// </summary>
    public IReadOnlySet<string> SupportedCategories { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Doors", "Windows", "Walls", "Rooms", "Floors", "Stairs", "Ramps", "Railings" };

    public static RuleCapabilities Default { get; } = new();
}
