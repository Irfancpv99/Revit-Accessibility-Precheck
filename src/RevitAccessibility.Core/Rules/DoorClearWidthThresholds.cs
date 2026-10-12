using RevitAccessibility.Core.Register;

namespace RevitAccessibility.Core.Rules;

/// <summary>
/// Threshold values in MILLIMETRES. The register stores the regulation value in cm (80 cm = 800 mm),
/// so the loader must convert. Never hardcode these in rule logic.
/// </summary>
public sealed class DoorClearWidthThresholds
{
    public DoorClearWidthThresholds(double accessDoorMinMm, double otherDoorMinMm, string clauseRef, string? unverifiedNote = null)
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
        UnverifiedNote = string.IsNullOrWhiteSpace(unverifiedNote) ? null : unverifiedNote;
    }

    public double AccessDoorMinMm { get; }
    public double OtherDoorMinMm { get; }
    public string ClauseRef { get; }

    /// <summary>Non-null when the values are not Verified in the register. Appended to every finding message.</summary>
    public string? UnverifiedNote { get; }

    public const string EntryRuleId = "DOOR-CLEAR-W-ENTRY";
    public const string OtherRuleId = "DOOR-CLEAR-W-OTHER";

    /// <summary>
    /// Builds thresholds from the two register rows (already converted to mm by the loader).
    /// Throws if either row is missing or was rejected, so a rule never runs on an invented value.
    /// </summary>
    public static DoorClearWidthThresholds FromRegister(RegisterLoadResult register)
    {
        ArgumentNullException.ThrowIfNull(register);
        var entry = register.Find(EntryRuleId)
            ?? throw new InvalidOperationException($"Register has no valid row for {EntryRuleId}.");
        var other = register.Find(OtherRuleId)
            ?? throw new InvalidOperationException($"Register has no valid row for {OtherRuleId}.");

        string? note = null;
        if (!entry.IsVerified || !other.IsVerified)
            note = $"UNVERIFIED: register status '{entry.Status}' / '{other.Status}'. Not a verified regulation value.";

        return new DoorClearWidthThresholds(entry.ThresholdMm, other.ThresholdMm, entry.Clause, note);
    }
}
