# 3. Developer Guide: C# Revit Add-in

Target: Revit 2026, .NET 8, Windows, developed in VS Code (CLI build) or Visual Studio.

## 3.1 Solution layout

```
RevitAccessibility.sln
  RevitAccessibility.Core/           class library, net8.0, NO Autodesk references
  RevitAccessibility.Core.Tests/     xUnit, net8.0
  RevitAccessibility.Revit/          class library, net8.0-windows, Revit API
  scripts/deploy.ps1                 copies build output to the Revit add-in folder
```

Core must never reference a Revit assembly. If it does, its tests need Revit installed.

## 3.2 Create the solution

```bash
dotnet new sln -n RevitAccessibility
dotnet new classlib -n RevitAccessibility.Core -f net8.0
dotnet new xunit -n RevitAccessibility.Core.Tests -f net8.0
dotnet new classlib -n RevitAccessibility.Revit -f net8.0-windows
dotnet sln add RevitAccessibility.Core/RevitAccessibility.Core.csproj
dotnet sln add RevitAccessibility.Core.Tests/RevitAccessibility.Core.Tests.csproj
dotnet sln add RevitAccessibility.Revit/RevitAccessibility.Revit.csproj
dotnet add RevitAccessibility.Core.Tests reference RevitAccessibility.Core
dotnet add RevitAccessibility.Revit reference RevitAccessibility.Core
dotnet test
```

`dotnet test` should pass the template placeholder test before any code is written.

## 3.3 Revit API reference

Use the Revit API NuGet package that targets Revit 2026 (verify the package page for
the exact name and version). Do not commit Autodesk DLLs to the repository.

```bash
dotnet add RevitAccessibility.Revit package <Revit-API-package> --version <2026-version>
```

In `RevitAccessibility.Revit.csproj` set:

```xml
<UseWPF>true</UseWPF>
<Platforms>x64</Platforms>
<PlatformTarget>x64</PlatformTarget>
<Nullable>enable</Nullable>
```

## 3.4 Core: neutral model and rules

Model (no Revit types):

```csharp
namespace RevitAccessibility.Core.Model;

public sealed record DoorDto(string Uid, string Level, double? ClearWidthMm);
public sealed record BuildingModel(IReadOnlyList<DoorDto> Doors);

public enum Severity { Info, Warning, Error }
public enum FindingStatus { Pass, Fail, CannotEvaluate }

public sealed record Finding(
    string RuleId, string ClauseRef, Severity Severity,
    FindingStatus Status, string ElementUid, string Message);
```

Rule interface and the first rule:

```csharp
namespace RevitAccessibility.Core.Rules;

public interface IRule
{
    string Id { get; }
    string ClauseRef { get; }
    IEnumerable<Finding> Evaluate(BuildingModel model);
}

// minWidthMm and clauseRef come from rule-register.csv, never from literals in code.
public sealed class DoorClearWidthRule(double minWidthMm, string clauseRef) : IRule
{
    public string Id => "DOOR-CLEAR-WIDTH";
    public string ClauseRef => clauseRef;

    public IEnumerable<Finding> Evaluate(BuildingModel model)
    {
        foreach (var door in model.Doors)
        {
            if (door.ClearWidthMm is null)
                yield return new Finding(Id, ClauseRef, Severity.Info,
                    FindingStatus.CannotEvaluate, door.Uid,
                    $"Door on level '{door.Level}' has no clear-width value.");
            else if (door.ClearWidthMm < minWidthMm)
                yield return new Finding(Id, ClauseRef, Severity.Error,
                    FindingStatus.Fail, door.Uid,
                    $"Clear width {door.ClearWidthMm:0} mm is below the minimum {minWidthMm:0} mm (level '{door.Level}').");
        }
    }
}
```

Engine:

```csharp
public sealed class RuleEngine(IEnumerable<IRule> rules)
{
    public IReadOnlyList<Finding> Run(BuildingModel model) =>
        rules.SelectMany(r => r.Evaluate(model))
             .OrderByDescending(f => f.Severity)
             .ThenBy(f => f.RuleId)
             .ToList();
}
```

Note: the engine above does not yet emit Pass findings. Decide whether Pass findings
are needed for the audit trail (the UI "Show passed" toggle requires them). If yes, add
a Pass branch to the rule.

## 3.5 Tests (write first, then implement)

Required cases for every rule:

1. Below the threshold: Fail.
2. Exactly at the threshold: no Fail.
3. One unit below the threshold: Fail.
4. Missing value: CannotEvaluate, never silently passed.
5. Empty model: no findings.
6. Mixed list: correct count per status.

```csharp
[Fact]
public void Missing_width_is_cannot_evaluate_not_pass()
{
    var rule = new DoorClearWidthRule(800, "TEST");
    var f = Assert.Single(rule.Evaluate(new BuildingModel([new DoorDto("u1", "L0", null)])));
    Assert.Equal(FindingStatus.CannotEvaluate, f.Status);
}
```

Run: `dotnet test`. Core tests must pass on a machine without Revit installed.

## 3.6 Revit adapter

The adapter is the only code that touches the Revit API.

- Collect doors with `FilteredElementCollector`, category `OST_Doors`, instances only.
- Read the clear-width parameter by its verified name. Handle a missing parameter by
  returning null (which becomes CannotEvaluate).
- Convert units: Revit stores lengths in feet internally. Use
  `UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters)`.
- Store `UniqueId`, not `ElementId`. `UniqueId` survives saving and reopening.

Verify on a real door: an 800 mm door must read 800 mm, not 262.

## 3.7 Command rules

- `[Transaction(TransactionMode.ReadOnly)]`. The check must not modify the model. If a
  transaction is ever opened, it is a bug.
- Catch exceptions, show a message, return `Result.Failed`.

## 3.8 Results window

- Modeless WPF window (so the user can keep working in Revit).
- Bind a `DataGrid` to a filtered `ICollectionView`.
- Severity and status shown as text plus icon, never colour alone.
- Detail pane for the selected finding.
- Disclaimer in the footer at all times.
- To select an element in Revit from a modeless window, use an `ExternalEvent`. Resolve
  `UniqueId` to `ElementId` inside the handler's `Execute`, not in the window.

## 3.9 Add-in manifest

File `RevitAccessibility.addin`. Generate the `AddInId` GUID once and never change it:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Command">
    <Name>Accessibility Pre-Check</Name>
    <Assembly>RevitAccessibility.Revit.dll</Assembly>
    <AddInId>GENERATE-ONCE-AND-KEEP</AddInId>
    <FullClassName>RevitAccessibility.Revit.Commands.RunCheckCommand</FullClassName>
    <VendorId>YOUR-ID</VendorId>
    <VendorDescription>Accessibility Pre-Check</VendorDescription>
  </AddIn>
</RevitAddIns>
```

The `Assembly` value should be the full path to the DLL, or a path relative to the
`.addin` file as your deploy script arranges it.

## 3.10 Deploy

`scripts/deploy.ps1` copies the build output to:

- `%APPDATA%\Autodesk\Revit\Addins\2026\RevitAccessibility\` (DLLs)
- `%APPDATA%\Autodesk\Revit\Addins\2026\` (the `.addin` file)

Build: `dotnet build RevitAccessibility.Revit -c Debug`
Deploy: `powershell -ExecutionPolicy Bypass -File scripts/deploy.ps1`
Restart Revit 2026 and find the command under Add-Ins.

A 2026 add-in does not load in Revit 2024. Keep separate builds per version.

## 3.11 Acceptance criteria (MVP)

- The check makes no model changes (undo stack unchanged).
- Every door appears: Fail, Cannot evaluate, or Pass. None silently dropped.
- Selecting a finding selects that door in Revit.
- All Core tests pass without Revit installed.
- Validated against the reference cases in `test-model/known-results-test-cases.md`.

## 3.12 Repository hygiene

`.gitignore` must exclude `bin/`, `obj/`, `.vs/`, `*.user`, `*.suo`, `*.rvt`, `*.rfa`,
`*.rte`, `*.ifc`, and `RevitAPI.dll` / `RevitAPIUI.dll`.

Run `git status` before the first commit and read every line.
