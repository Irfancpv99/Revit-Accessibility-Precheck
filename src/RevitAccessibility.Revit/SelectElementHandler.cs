using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitAccessibility.Revit;

/// <summary>Selects an element by UniqueId. Resolved inside Execute because ElementId is not stable.</summary>
public sealed class SelectElementHandler : IExternalEventHandler
{
    public string? PendingUid { get; set; }

    public void Execute(UIApplication app)
    {
        var uid = PendingUid;
        PendingUid = null;
        var uiDoc = app.ActiveUIDocument;
        if (uid is null || uiDoc is null) return;

        var element = uiDoc.Document.GetElement(uid);
        if (element is null)
        {
            TaskDialog.Show("Accessibility Pre-Check", "That element no longer exists in the active document.");
            return;
        }
        uiDoc.Selection.SetElementIds([element.Id]);
        uiDoc.ShowElements(element.Id);
    }

    public string GetName() => "Accessibility Pre-Check: select element";
}
