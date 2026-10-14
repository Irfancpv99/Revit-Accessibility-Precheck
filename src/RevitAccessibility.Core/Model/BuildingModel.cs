namespace RevitAccessibility.Core.Model;

/// <summary>
/// A door as read from the model. Null means "not set in the model", never "passes".
/// </summary>
/// <param name="Uid">Stable element identifier (Revit UniqueId in the add-in).</param>
/// <param name="Level">Building level name, used for reporting only.</param>
/// <param name="ClearWidthMm">Clear width (luce netta) in millimetres, or null if the parameter is missing.</param>
/// <param name="IsAccessDoor">
/// True: designer marked the door as an access door (80 cm rule).
/// False: designer marked it as another door (75 cm rule).
/// Null: not classified; the result is "Cannot evaluate".
/// </param>
/// <param name="Mark">Door Mark for reporting only; may be null.</param>
/// <param name="AccessDoorNote">Why <paramref name="IsAccessDoor"/> is null (for example parameter not found). Reporting only.</param>
/// <param name="ClearWidthNote">Why <paramref name="ClearWidthMm"/> is null (for example parameter not found). Reporting only.</param>
public sealed record DoorDto(
    string Uid,
    string Level,
    double? ClearWidthMm,
    bool? IsAccessDoor,
    string? Mark = null,
    string? AccessDoorNote = null,
    string? ClearWidthNote = null);

/// <summary>One measured value in millimetres, or null plus the reason it could not be read.</summary>
public sealed record MeasurementDto(double? ValueMm, string? Note = null);

/// <summary>
/// A non-door element with the length parameters that register rules need, keyed by parameter name.
/// Used by the generic <c>ParameterRule</c>.
/// </summary>
public sealed record ElementDto(
    string Uid,
    string Category,
    string Level,
    string? Mark,
    IReadOnlyDictionary<string, MeasurementDto> Measurements);

public sealed record BuildingModel(IReadOnlyList<DoorDto> Doors, IReadOnlyList<ElementDto>? Elements = null)
{
    public IReadOnlyList<ElementDto> AllElements => Elements ?? [];
}
