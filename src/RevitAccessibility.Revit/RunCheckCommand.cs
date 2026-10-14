using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitAccessibility.Core.Config;
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

            var ruleSet = ParameterRuleFactory.FromRegister(register,
                [DoorClearWidthThresholds.EntryRuleId, DoorClearWidthThresholds.OtherRuleId]);
            var notRun = new List<string>(ruleSet.NotRun);

            var model = new ModelSnapshotBuilder(config).Build(uiDoc.Document, ruleSet.Rules.Select(r => r.Spec).ToList(), notRun);
            var rules = new List<IRule> { new DoorClearWidthRule(thresholds) };
            rules.AddRange(ruleSet.Rules);
            var findings = new RuleEngine(rules).Run(model);
            foreach (var n in notRun) AddinLog.Write("  Rule not run: " + n);

            // ExternalEvent must be created here, inside a valid Revit API context.
            var handler = new SelectElementHandler();
            var externalEvent = ExternalEvent.Create(handler);

            string parameterInfo = $"Parameters read: clear width = '{config.ClearWidthParameter}', access door = '{config.AccessDoorParameter}' (config: {config.Source})";

            _window?.Close();
            _window = new ResultsWindow(model, findings, thresholds.UnverifiedNote, parameterInfo, notRun, handler, externalEvent);
            new System.Windows.Interop.WindowInteropHelper(_window) { Owner = commandData.Application.MainWindowHandle };
            _window.Show(); // modeless
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            AddinLog.Write(ex.ToString());
            message = ex.Message;
            TaskDialog.Show("Accessibility Pre-Check", $"The check failed: {ex.Message}\n\nDetails: {AddinLog.FilePath}");
            return Result.Failed;
        }
    }
}
