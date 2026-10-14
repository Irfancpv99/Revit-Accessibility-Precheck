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
/// <param name="ClearWidthSource">Which parameter the width came from and what it means. Null = not declared.</param>
/// <param name="DocumentId">Id of the document the door belongs to (see <c>DocumentDto</c>); null for a single-model run.</param>
public sealed record DoorDto(
    string Uid,
    string Level,
    double? ClearWidthMm,
    bool? IsAccessDoor,
    string? Mark = null,
    string? AccessDoorNote = null,
    string? ClearWidthNote = null,
    MeasurementInfo? ClearWidthSource = null,
    string? DocumentId = null);

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
    IReadOnlyDictionary<string, MeasurementDto> Measurements,
    string? DocumentId = null);

public enum GeometryStatus { Available, Missing, Invalid, Unsupported }

/// <summary>
/// An element's geometry summary for coordination checks. <paramref name="BoxMm"/> is already in HOST
/// coordinates (the Revit side transforms linked elements with <c>RigidTransform</c>); it is null when
/// <paramref name="Status"/> is not Available. Missing geometry is never treated as "no clash".
/// </summary>
/// <param name="Uid">UniqueId of the element inside its own document.</param>
/// <param name="DocumentId">Which document the element lives in. The pair (DocumentId, Uid) is the element's identity.</param>
/// <param name="HostUid">UniqueId of the element this one is hosted in (for example the wall of a door), if any. Used to exclude intentional connections.</param>
public sealed record GeoElementDto(
    string Uid,
    string DocumentId,
    string Category,
    string Level,
    string? Mark,
    Geometry.Aabb? BoxMm,
    GeometryStatus Status,
    string? GeometryNote = null,
    string? HostUid = null)
{
    public string Key => DocumentId + ":" + Uid;
}

/// <summary>Result of an exact geometric test for one candidate pair, performed on the Revit side.</summary>
public enum PairConfirmation { NotAttempted, Intersects, NoIntersection, Failed }

public sealed record PairConfirmationDto(string KeyA, string KeyB, PairConfirmation Result, double? OverlapVolumeMm3 = null, string? Note = null);

public sealed record BuildingModel(
    IReadOnlyList<DoorDto> Doors,
    IReadOnlyList<ElementDto>? Elements = null,
    IReadOnlyList<GeoElementDto>? GeoElements = null,
    IReadOnlyList<PairConfirmationDto>? Confirmations = null)
{
    public IReadOnlyList<ElementDto> AllElements => Elements ?? [];
    public IReadOnlyList<GeoElementDto> AllGeoElements => GeoElements ?? [];
    public IReadOnlyList<PairConfirmationDto> AllConfirmations => Confirmations ?? [];
}
