using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.Rules;

/// <summary>
/// Checks the clear width (luce netta) of each door against the access-door or other-door threshold.
/// Every door produces exactly one finding: Pass, Fail, or CannotEvaluate. Nothing is dropped silently.
/// </summary>
public sealed class DoorClearWidthRule : IRule
{
    private readonly DoorClearWidthThresholds _thresholds;

    public DoorClearWidthRule(DoorClearWidthThresholds thresholds)
    {
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
    }

    public string Id => "DOOR-CLEAR-W";

    public IEnumerable<Finding> Evaluate(BuildingModel model)
    {
        foreach (var door in model.Doors)
            yield return EvaluateDoor(door);
    }

    private Finding EvaluateDoor(DoorDto door)
    {
        if (door.IsAccessDoor is null)
            return CannotEvaluate(door, $"Door on level '{door.Level}' is not classified as access or other door.{WithNote(door.AccessDoorNote)}");

        var width = door.ClearWidthMm;
        if (width is null)
            return CannotEvaluate(door, $"Door on level '{door.Level}' has no clear-width value.{WithNote(door.ClearWidthNote)}");

        if (!double.IsFinite(width.Value) || width.Value <= 0)
            return CannotEvaluate(door, $"Door on level '{door.Level}' has an invalid clear width ({width.Value}).");

        bool isAccess = door.IsAccessDoor.Value;
        double required = isAccess ? _thresholds.AccessDoorMinMm : _thresholds.OtherDoorMinMm;
        string kind = isAccess ? "access door" : "door";

        return MeasurementTolerance.AtLeast(width.Value, required)
            ? new Finding(Id, _thresholds.ClauseRef, Severity.Info, FindingStatus.Pass, door.Uid,
                Label($"Clear width {width.Value:0.##} mm meets the minimum {required:0.##} mm for {kind}."))
            : new Finding(Id, _thresholds.ClauseRef, Severity.Error, FindingStatus.Fail, door.Uid,
                Label($"Clear width {width.Value:0.##} mm is below the minimum {required:0.##} mm for {kind} (level '{door.Level}')."));
    }

    private Finding CannotEvaluate(DoorDto door, string message) =>
        new(Id, _thresholds.ClauseRef, Severity.Info, FindingStatus.CannotEvaluate, door.Uid, Label(message));

    private static string WithNote(string? note) => string.IsNullOrWhiteSpace(note) ? "" : " " + note.Trim();

    private string Label(string message) =>
        _thresholds.UnverifiedNote is null ? message : $"{message} [{_thresholds.UnverifiedNote}]";
}
