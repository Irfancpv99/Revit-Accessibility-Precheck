using Autodesk.Revit.DB;
using RevitAccessibility.Core.Config;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;

namespace RevitAccessibility.Revit;

/// <summary>
/// Reads doors into Core DTOs. Read-only: never opens a transaction.
/// A parameter that is missing, unset or of the wrong kind becomes null plus a note saying why. Never 0, never Pass.
/// </summary>
public sealed class ModelSnapshotBuilder
{
    private readonly AddinConfig _config;

    public ModelSnapshotBuilder(AddinConfig config) => _config = config;

    public BuildingModel Build(Document doc)
    {
        var doors = new List<DoorDto>();
        var collector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Doors)
            .WhereElementIsNotElementType();

        bool listedNames = false;
        foreach (Element e in collector)
        {
            if (e is not FamilyInstance door) continue;

            var width = ReadLengthMm(door, _config.ClearWidthParameter);
            var access = ReadYesNo(door, _config.AccessDoorParameter);

            // Once per run, list what Revit does expose when a configured name is wrong.
            if (!listedNames && (width.Note?.Contains("not found") == true || access.Note?.Contains("not found") == true))
            {
                listedNames = true;
                AddinLog.Write("  Parameters on the first affected door: " + string.Join("; ",
                    door.Parameters.Cast<Parameter>().Select(p => p.Definition.Name).Distinct().OrderBy(n => n)));
            }

            doors.Add(new DoorDto(
                Uid: door.UniqueId,
                Level: LevelName(doc, door),
                ClearWidthMm: width.Value,
                IsAccessDoor: access.Value,
                Mark: door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString(),
                AccessDoorNote: access.Note,
                ClearWidthNote: width.Note));
        }

        LogSummary(doors);
        return new BuildingModel(doors);
    }

    // Register category text -> Revit category. Anything else is reported as "not run", never skipped silently.
    private static readonly Dictionary<string, BuiltInCategory> SupportedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Doors"] = BuiltInCategory.OST_Doors,
        ["Windows"] = BuiltInCategory.OST_Windows,
        ["Walls"] = BuiltInCategory.OST_Walls,
        ["Rooms"] = BuiltInCategory.OST_Rooms,
        ["Floors"] = BuiltInCategory.OST_Floors,
        ["Stairs"] = BuiltInCategory.OST_Stairs,
        ["Ramps"] = BuiltInCategory.OST_Ramps,
        ["Railings"] = BuiltInCategory.OST_StairsRailing,
    };

    /// <summary>
    /// Doors plus the length parameters that register-driven rules need. Rules whose category is not supported
    /// are added to <paramref name="notRun"/> so the window can say so.
    /// </summary>
    public BuildingModel Build(Document doc, IReadOnlyList<ParameterRuleSpec> specs, List<string> notRun)
    {
        var doorsModel = Build(doc);
        var elements = new List<ElementDto>();

        foreach (var group in specs.GroupBy(sp => sp.Category.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (!SupportedCategories.TryGetValue(group.Key, out var bic))
            {
                foreach (var sp in group)
                    notRun.Add($"{sp.RuleId}: category '{group.Key}' is not supported yet (supported: {string.Join(", ", SupportedCategories.Keys)}).");
                continue;
            }

            var parameters = group.Select(sp => sp.Parameter).Distinct(StringComparer.Ordinal).ToList();
            foreach (Element e in new FilteredElementCollector(doc).OfCategory(bic).WhereElementIsNotElementType())
            {
                var measurements = parameters.ToDictionary(n => n, n =>
                {
                    var r = ReadLengthMm(e, n);
                    return new MeasurementDto(r.Value, r.Note);
                });
                elements.Add(new ElementDto(e.UniqueId, group.Key, LevelName(doc, e),
                    e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString(), measurements));
            }
        }

        if (elements.Count > 0)
            AddinLog.Write($"  Register-driven rules: {specs.Count} rule(s), {elements.Count} element measurement set(s).");
        return new BuildingModel(doorsModel.Doors, elements);
    }

    private readonly record struct Read<T>(T? Value, string? Note) where T : struct;

    private static string LevelName(Document doc, Element element)
    {
        var id = element.LevelId;
        return id != ElementId.InvalidElementId && doc.GetElement(id) is Level l ? l.Name : "(no level)";
    }

    // Instance parameter first, then type parameter. Reports which of the two it used, or why neither worked.
    private static (Parameter? Param, string? Note) Find(Element element, string name)
    {
        var instance = element.LookupParameter(name);
        if (instance is not null && instance.HasValue) return (instance, null);

        var typeParam = element.Document.GetElement(element.GetTypeId())?.LookupParameter(name);
        if (typeParam is not null && typeParam.HasValue) return (typeParam, null);

        if (instance is null && typeParam is null)
            return (null, $"Parameter '{name}' was not found on this element or its type (check config.json and the parameter name in Revit).");
        return (null, $"Parameter '{name}' exists but has no value set on this element.");
    }

    private static Read<double> ReadLengthMm(Element element, string name)
    {
        var (p, note) = Find(element, name);
        if (p is null) return new(null, note);
        if (p.StorageType != StorageType.Double || p.Definition.GetDataType() != SpecTypeId.Length)
            return new(null, $"Parameter '{name}' is not a Length parameter, so it cannot be read as a length.");
        return new(UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters), null);
    }

    private static Read<bool> ReadYesNo(Element element, string name)
    {
        var (p, note) = Find(element, name);
        if (p is null)
            return new(null, note is not null && note.Contains("no value")
                ? note + " Tick it (Yes) or tick and untick it (No) so a value is stored."
                : note);
        if (p.StorageType != StorageType.Integer || p.Definition.GetDataType() != SpecTypeId.Boolean.YesNo)
            return new(null, $"Parameter '{name}' is not a Yes/No parameter.");
        return new(p.AsInteger() != 0, null);
    }

    private void LogSummary(IReadOnlyList<DoorDto> doors)
    {
        AddinLog.Write($"Check run: {doors.Count} doors. Config: {_config.Source}; " +
                       $"width parameter '{_config.ClearWidthParameter}', access parameter '{_config.AccessDoorParameter}'.");
        int noAccess = doors.Count(d => d.IsAccessDoor is null);
        int noWidth = doors.Count(d => d.ClearWidthMm is null);
        AddinLog.Write($"  Unclassified: {noAccess}. No width: {noWidth}.");
        foreach (var d in doors.Where(d => d.AccessDoorNote is not null || d.ClearWidthNote is not null).Take(3))
            AddinLog.Write($"  Door {d.Mark}: {d.AccessDoorNote} {d.ClearWidthNote}".TrimEnd());
    }
}
