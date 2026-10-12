namespace RevitAccessibility.Core.Rules;

/// <summary>
/// Threshold values in MILLIMETRES. The register stores the regulation value in cm (80 cm = 800 mm),
/// so the loader must convert. Never hardcode these in rule logic.
/// </summary>
public sealed class DoorClearWidthThresholds
{
    public DoorClearWidthThresholds(double accessDoorMinMm, double otherDoorMinMm, string clauseRef)
    {
        if (!double.IsFinite(accessDoorMinMm) || accessDoorMinMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(accessDoorMinMm), "Must be a positive, finite value.");
        if (!double.IsFinite(otherDoorMinMm) || otherDoorMinMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(otherDoorMinMm), "Must be a positive, finite value.");
        if (string.IsNullOrWhiteSpace(clauseRef))
            throw new ArgumentException("Clause reference is required.", nameof(clauseRef));

        AccessDoorMinMm = accessDoorMinMm;
        OtherDoorMinMm = otherDoorMinMm;
        ClauseRef = clauseRef;
    }

    public double AccessDoorMinMm { get; }
    public double OtherDoorMinMm { get; }
    public string ClauseRef { get; }
}
