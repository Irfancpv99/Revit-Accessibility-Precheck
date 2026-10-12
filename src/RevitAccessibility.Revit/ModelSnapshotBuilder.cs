using Autodesk.Revit.DB;
using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Revit;

/// <summary>Reads doors into Core DTOs. Read-only: never opens a transaction.</summary>
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

        foreach (Element e in collector)
        {
            if (e is not FamilyInstance door) continue;
            doors.Add(new DoorDto(
                Uid: door.UniqueId,
                Level: LevelName(doc, door),
                ClearWidthMm: ReadMillimetres(door, _config.ClearWidthParameter),
                IsAccessDoor: ReadYesNo(door, _config.AccessDoorParameter),
                Mark: door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()));
        }
        return new BuildingModel(doors);
    }

    private static string LevelName(Document doc, FamilyInstance door)
    {
        var id = door.LevelId;
        return id != ElementId.InvalidElementId && doc.GetElement(id) is Level l ? l.Name : "(no level)";
    }

    // Instance parameter first, then type parameter. Returns null when absent or unset, never 0.
    private static Parameter? Find(FamilyInstance door, string name)
    {
        var p = door.LookupParameter(name);
        if (p is not null && p.HasValue) return p;
        var typeParam = door.Document.GetElement(door.GetTypeId())?.LookupParameter(name);
        return typeParam is not null && typeParam.HasValue ? typeParam : null;
    }

    private static double? ReadMillimetres(FamilyInstance door, string name)
    {
        var p = Find(door, name);
        if (p is null || p.StorageType != StorageType.Double) return null;
        return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), UnitTypeId.Millimeters);
    }

    private static bool? ReadYesNo(FamilyInstance door, string name)
    {
        var p = Find(door, name);
        if (p is null || p.StorageType != StorageType.Integer) return null;
        return p.AsInteger() != 0;
    }
}
