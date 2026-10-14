# CLAUDE.md: Revit Accessibility Pre-Check

Standing instructions for Claude Code. Read this file fully before any change.

## 1. What this project is

A pre-check tool that reads a Revit model, tests elements against accessibility rules from a rule register, and reports Pass, Fail, or Cannot evaluate for each element, with the regulation clause cited. It is NOT a compliance certificate. Every output must say so.

## 2. Decisions already made (do not change without asking the owner)

- **Platform:** Revit 2026 only, Windows, C#. No ArchiCAD. Core and Core.Tests: .NET 8. Revit add-in: .NET 10 (`net10.0-windows`), because Revit 2026.5 runs on .NET 10 and its API will not compile against .NET 8. Building the add-in needs the .NET 10 SDK. Changed 2026-10-12 with owner approval.
- **Scope:** public buildings first (DPR 503/1996). Private buildings are a later phase (DM 236/1989 as source).
- **Licence:** free for now; a paid version may come later. Do not add licence text or paid features without asking.
- **Verification:** every rule needs two sign-offs, a practising architect and a university professor. The professor is not named yet; do not invent names.
- **Door clear width (luce netta):**
  - Access door (entrance to building or unit): 80 cm = 800 mm.
  - Other doors: 75 cm = 750 mm.
  - Source: DM 236/1989 point 8.1.1, incorporated by DPR 503/1996. Article reference still to be confirmed. Status: Needs review.
- **Access door classification:** a shared parameter "Access door" (Yes/No) on door families. An unset value is NOT "No". Unset doors produce Cannot evaluate.
- **Units:** the register stores values in cm. Code uses millimetres. The loader must convert (80 cm -> 800 mm). Never mix units silently.

## 3. Hard rules

1. **Never invent regulation values.** Thresholds come only from `rules/rule-register.csv`. If a value is `[TBD]` or status is not Verified, the code may run it only in test/demo mode and must label the output as unverified.
2. **Never report silently.** Every element gets exactly one finding. Missing data is Cannot evaluate, never Pass.
3. **Core has no Revit references.** `RevitAccessibility.Core` must not reference any Autodesk assembly or package. This keeps rules testable without Revit.
4. **The check is read-only.** The check command uses `TransactionMode.ReadOnly`. Opening a transaction in the check path is a bug.
5. **Use `UniqueId`** for element references, never `ElementId` (it changes between sessions).
6. **Client files stay out of git.** Never commit `.rvt`, `.rfa`, `.rte`, `.ifc`, client schedules, or Autodesk DLLs. Never open a client file in a way that saves it. Work on copies.
7. **No secrets, no personal paths in code.** Use `%APPDATA%` and relative paths.
8. **Disclaimer on screen.** The results window footer must always show: "Pre-check only. Results must be reviewed by a qualified professional. This tool does not certify compliance."
9. **Ask before** changing rule values, adding a rule, changing the register structure, adding a dependency, or touching anything in `docs/` that states a regulation fact.

## 4. Repository layout

```
revit-accessibility-precheck/
  CLAUDE.md                          this file
  README.md
  .gitignore
  docs/                              project guides (English)
  rules/rule-register.csv            thresholds (edit values only with owner approval)
  test-model/known-results-test-cases.md
  src/
    RevitAccessibility.sln
    RevitAccessibility.Core/         net8.0, pure C#, no Revit
      Model/                         DoorDto, BuildingModel, Finding, Severity, FindingStatus
      Rules/                         IRule, DoorClearWidthRule, DoorClearWidthThresholds
      Engine/                        RuleEngine
    RevitAccessibility.Core.Tests/   tests (see section 6)
    RevitAccessibility.Revit/        net10.0-windows, Revit 2026 add-in (built; first-run testing)
  scripts/deploy.ps1                 builds and copies the add-in to the Revit add-in folder
```

## 5. Build and test commands

```
cd src
dotnet build RevitAccessibility.sln
dotnet test  RevitAccessibility.sln
```

Current state: Core and its tests exist and passed 20/20 in a sandbox that had no NuGet access. The sandbox used a plain console test runner. On the owner's machine, migrate the test project to xUnit:
`dotnet new xunit` into a new project, port the tests, delete the plain runner. Keep the same test cases.

## 6. Test requirements

Every rule needs tests for:
- Below threshold: Fail.
- Exactly at threshold: Pass (boundary).
- One unit below threshold: Fail.
- Missing value: Cannot evaluate.
- Unclassified door: Cannot evaluate.
- Invalid value (zero, negative, NaN, infinity): Cannot evaluate.
- Empty model: no findings.
- Mixed list: correct count per status, one finding per element.

Also test the register loader (once written): cm to mm conversion, rejection of blank or non-numeric values, and rejection of rows without a clause reference.

Do not claim tests passed unless they ran and passed. Report the exact command and result.

## 7. Revit add-in (next phase, to build)

- Project `RevitAccessibility.Revit`, net10.0-windows, WPF enabled, x64.
- Reference the Revit 2026 API from the owner's own Revit install. No Autodesk-published API package exists on nuget.org (checked 2026-10-12). The path comes from `RevitPath.local.props` (git-ignored; copy `RevitPath.local.props.example`) or the `REVIT_2026_DIR` environment variable. Never commit a machine path or an Autodesk DLL.
- Components:
  - `ModelSnapshotBuilder`: reads doors (category OST_Doors, instances only), level name, Mark, clear-width parameter, and the Access door parameter. Converts feet to mm with `UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters)`. Missing parameter returns null, never 0.
  - `RunCheckCommand`: `[Transaction(TransactionMode.ReadOnly)]`, builds the snapshot, runs the engine, opens the results window.
  - `ResultsWindow`: modeless WPF window. DataGrid with filters (level, severity, search, show passed), summary bar, detail pane, footer disclaimer, CSV export. Severity and status shown as text and icon, never colour alone.
  - Selecting an element in the model uses `ExternalEvent`. Resolve `UniqueId` to `ElementId` inside the handler's `Execute`.
- Parameter names (clear width, Access door) come from configuration, not hardcoded, because the owner will confirm them in Revit.
- `RevitAccessibility.addin` manifest: fixed `AddInId` GUID, generated once and never changed.
- `scripts/deploy.ps1` copies to `%APPDATA%\Autodesk\Revit\Addins\2026\RevitAccessibility\` and the `.addin` file to `%APPDATA%\Autodesk\Revit\Addins\2026\`.

## 8. Revit interaction protocol

Claude Code runs on the owner's laptop and can build, test, and run scripts. It cannot see the Revit window.

- Claude Code may build the add-in and run deploy.ps1 when asked.
- Running the add-in inside Revit is done by the owner. Claude Code must ask the owner to run it and report what happened, including any error text and any log file contents.
- Revit's journal file (`%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2026\Journals`) and the add-in's own log file are the first places to look when something fails. Ask the owner to paste the relevant lines.
- Never run the add-in against a client model that is not an explicitly approved copy.

## 9. Working style

- Make the smallest change that meets the task. No unrelated refactors.
- Show a short plan before changes that touch more than two files.
- After changes: build, run tests, report results honestly, and list anything not verified.
- Keep docs and the register consistent with code. If a rule changes, update the register, the test file, and the relevant doc.
- Italian UI text is a later task. Code comments, docs, and commit messages are in English.

## 10. Known open items (owner to resolve)

1. Official text of DPR 503/1996 article that incorporates DM 236/1989 point 8.1.1, and the point 8.0.1 definition of luce netta. Verifiers to confirm.
2. Exact Revit parameter name for clear width in the project families.
3. Access door shared parameter: owner to create it in the family template.
4. Whether the rule register should use one rule ID with two thresholds (current code) or two IDs (register currently lists DOOR-CLEAR-W-ENTRY and DOOR-CLEAR-W-OTHER). Decide and make register and code match.
5. GitHub licence.
6. Professor verifier: after MVP.
