using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.Rules;

/// <summary>How much the rule trusts the width it is given.</summary>
public enum WidthTrust
{
    /// <summary>
    /// Default and the only setting for real use. A width is judged Pass or Fail only if its source is declared
    /// to be the clear opening (luce netta). A nominal or undeclared width gives Needs review.
    /// </summary>
    RequireDeclaredClearOpening,

    /// <summary>The caller asserts the supplied value is the clear opening. For unit tests of the threshold logic only.</summary>
    TrustSuppliedValue,
}

/// <summary>
/// Checks the clear width (luce netta) of each door against the access-door or other-door threshold.
/// Every door produces exactly one finding: Pass, Fail, CannotEvaluate or NeedsReview. Nothing is dropped silently.
/// Version 2: a nominal width is never silently judged as a clear opening.
/// </summary>
public sealed class DoorClearWidthRule : IRule
{
    public const string Version = "2";

    private readonly DoorClearWidthThresholds _thresholds;
    private readonly WidthTrust _trust;

    public DoorClearWidthRule(DoorClearWidthThresholds thresholds, WidthTrust trust = WidthTrust.RequireDeclaredClearOpening)
    {
        _thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
        _trust = trust;
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
            return CannotEvaluate(door,
                $"Door on level '{door.Level}' is not classified as access or other door.{WithNote(door.AccessDoorNote)}",
                "access-door classification");

        var width = door.ClearWidthMm;
        if (width is null)
            return CannotEvaluate(door,
                $"Door on level '{door.Level}' has no clear-width value.{WithNote(door.ClearWidthNote)}",
                "clear width");

        if (!double.IsFinite(width.Value) || width.Value <= 0)
            return CannotEvaluate(door, $"Door on level '{door.Level}' has an invalid clear width ({width.Value}).", "valid clear width");

        bool isAccess = door.IsAccessDoor.Value;
        double required = isAccess ? _thresholds.AccessDoorMinMm : _thresholds.OtherDoorMinMm;
        string kind = isAccess ? "access door" : "door";
        bool ok = MeasurementTolerance.AtLeast(width.Value, required);

        if (_trust == WidthTrust.RequireDeclaredClearOpening && door.ClearWidthSource?.Meaning != MeasurementMeaning.ClearOpening)
        {
            string source = door.ClearWidthSource is null ? "an undeclared source" : door.ClearWidthSource.Describe();
            return new Finding(Id, _thresholds.ClauseRef, Severity.Warning, FindingStatus.NeedsReview, door.Uid,
                Label($"Needs review: width {width.Value:0.##} mm was read from {source}. A nominal or undeclared width is not the legal clear opening, " +
                      $"so no Pass or Fail is given. Indicative only: it is {(ok ? "at or above" : "below")} the {required:0.##} mm minimum for {kind}. " +
                      "Measure the clear opening or declare a clear-opening parameter."),
                Detail(door, width.Value, required, kind, missing: ["declared clear-opening measurement"]));
        }

        return ok
            ? new Finding(Id, _thresholds.ClauseRef, Severity.Info, FindingStatus.Pass, door.Uid,
                Label($"Clear width {width.Value:0.##} mm meets the minimum {required:0.##} mm for {kind}."),
                Detail(door, width.Value, required, kind))
            : new Finding(Id, _thresholds.ClauseRef, Severity.Error, FindingStatus.Fail, door.Uid,
                Label($"Clear width {width.Value:0.##} mm is below the minimum {required:0.##} mm for {kind} (level '{door.Level}')."),
                Detail(door, width.Value, required, kind));
    }

    private FindingDetail Detail(DoorDto door, double measured, double required, string kind, IReadOnlyList<string>? missing = null) =>
        new(Version, CheckType.Regulatory,
            SourceDocument: door.DocumentId,
            MeasuredValue: measured,
            Unit: "mm",
            MeasurementSource: door.ClearWidthSource?.Describe() ?? "not declared",
            ExpectedCondition: $"at least {required:0.##} mm ({kind})",
            VerificationStatus: _thresholds.VerificationStatus,
            MissingInputs: missing);

    private Finding CannotEvaluate(DoorDto door, string message, string missingInput) =>
        new(Id, _thresholds.ClauseRef, Severity.Info, FindingStatus.CannotEvaluate, door.Uid, Label(message),
            new FindingDetail(Version, CheckType.Regulatory, SourceDocument: door.DocumentId,
                MeasurementSource: door.ClearWidthSource?.Describe(), VerificationStatus: _thresholds.VerificationStatus,
                MissingInputs: [missingInput]));

    private static string WithNote(string? note) => string.IsNullOrWhiteSpace(note) ? "" : " " + note.Trim();

    private string Label(string message) =>
        _thresholds.UnverifiedNote is null ? message : $"{message} [{_thresholds.UnverifiedNote}]";
}
