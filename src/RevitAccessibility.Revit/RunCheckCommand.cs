using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Register;
using RevitAccessibility.Core.Rules;

namespace RevitAccessibility.Revit;

[Transaction(TransactionMode.ReadOnly)]
public sealed class RunCheckCommand : IExternalCommand
{
    private static ResultsWindow? _window;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc is null)
            {
                TaskDialog.Show("Accessibility Pre-Check", "Open a project first.");
                return Result.Cancelled;
            }

            var config = AddinConfig.Load();
            var assemblyDir = Path.GetDirectoryName(typeof(RunCheckCommand).Assembly.Location)!;
            var register = RuleRegisterLoader.LoadFile(Path.Combine(assemblyDir, "rule-register.csv"));
            var thresholds = DoorClearWidthThresholds.FromRegister(register);

            var model = new ModelSnapshotBuilder(config).Build(uiDoc.Document);
            var findings = new RuleEngine([new DoorClearWidthRule(thresholds)]).Run(model);

            // ExternalEvent must be created here, inside a valid Revit API context.
            var handler = new SelectElementHandler();
            var externalEvent = ExternalEvent.Create(handler);

            _window?.Close();
            _window = new ResultsWindow(model, findings, thresholds.UnverifiedNote, handler, externalEvent);
            _window.Show(); // modeless
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            AddinLog.Write(ex.ToString());
            message = ex.Message;
            TaskDialog.Show("Accessibility Pre-Check", $"The check failed: {ex.Message}\n\nDetails: {AddinLog.Path_}");
            return Result.Failed;
        }
    }
}
