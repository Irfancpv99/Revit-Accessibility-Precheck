# 08. Manual test procedure for Revit 2026

Automated tests cover Core only. Everything below needs a person with Revit 2026 and has **not been
run by the developer** unless the "Last run" column says so. Use only a test model you made yourself
or an approved **copy**. The check never modifies a model, and the model should not be marked as
modified afterwards.

## A. Environment

| # | Step | Expected | Last run |
|---|---|---|---|
| A1 | Note Revit build (Help > About) and that the add-in targets .NET 10 | Build 2026.5 or later on a .NET 10 runtime. An older 2026 update may not load a .NET 10 add-in. | Journal of 14 Oct 2026 showed build 20260731, API 26.5, .NET 10.0.9 |
| A2 | Close Revit, run `scripts\deploy.ps1 -Dotnet <.NET 10 SDK dotnet.exe>` | "Deployed to ..." and no error. If a file is locked the script says which. | Not run for the current build |
| A3 | Start Revit, open the test model, Add-Ins > External Tools > Accessibility Pre-Check | A window opens. If not, read `%APPDATA%\RevitAccessibility\log.txt`. | Run on earlier builds |
| A4 | Footer of the window | Shows the disclaimer text exactly. | Seen on earlier builds |
| A5 | Window banner | Yellow "UNVERIFIED" banner while the register is not Verified. | Seen on earlier builds |

## B. Configuration

`%APPDATA%\RevitAccessibility\config.json`:

```json
{
  "ClearWidthParameter": "Width",
  "AccessDoorParameter": "Access door",
  "ClearWidthMeaning": "NominalProxy"
}
```

| # | Step | Expected |
|---|---|---|
| B1 | Run with the file above | Under the summary: `Parameters read: clear width = 'Width' ...` |
| B2 | Rename the clear-width parameter in config to one that does not exist | Each door says `Parameter '<name>' was not found ...` (Cannot evaluate), and the log lists the parameters Revit exposes. |
| B3 | Break the JSON on purpose | The command fails with a message naming the config file; nothing silently falls back. |

## C. Door check

> The add-in now passes the declared width meaning from `config.json` (`ClearWidthMeaning`). **Not yet run in Revit:**
> this change was made without a Revit build. With the default `NominalProxy` every door must still give
> **Needs review**. Pass or Fail appears only when you declare `ClearOpening`, and that must be used for testing only
> until a real clear-opening measurement exists.

Model: door types with Width 700, 749, 750, 799, 800, 900 mm. Set **Access door**: tick for Yes; tick
and untick for No (an untouched Yes/No parameter stores nothing).

| # | Setup | Expected |
|---|---|---|
| C1 | `ClearWidthMeaning` = `NominalProxy` (default), any door | **Needs review**, message names parameter `Width` and says nominal width is not the clear opening. Never Pass or Fail. |
| C2 | `ClearWidthMeaning` = `ClearOpening` (test only), 700 mm, Access = Yes | Fail |
| C3 | 800 mm, Yes | Pass (boundary) |
| C4 | 799 mm, Yes | Fail |
| C5 | 749 mm, No | Fail |
| C6 | 750 mm, No | Pass (boundary) |
| C7 | Access door never touched | Cannot evaluate, message says the parameter has no value set |
| C8 | Unit check: a door of exactly 800 mm | Reads 800, not 262 or 0.8 |
| C9 | Export CSV | First line is the disclaimer; every row has all 18 columns; measured value has full precision |

## D. Model-unchanged check

| # | Step | Expected |
|---|---|---|
| D1 | After running the check and closing the window, look at the title bar and Save button | The model is not marked as modified; no Undo entries were added by the check. |

## E. Potential clash and linked models (Phase 2, not built)

These tests cannot be run until bounding boxes, exact intersection and link transforms are read from
Revit. They are listed so the intended behaviour is fixed in advance.

| # | Setup | Expected |
|---|---|---|
| E1 | Two crossing elements, exact test confirms | Needs review, "confirmed geometric intersection", check type Geometric |
| E2 | Two elements whose boxes overlap but solids do not | No finding |
| E3 | A door and its host wall | No finding (intentional connection) |
| E4 | An element with no geometry | Cannot evaluate; not a "no clash" result |
| E5 | Link set to unloaded | Elements of that link: Cannot evaluate with the reason |
| E6 | Link rotated 90 degrees and moved | Findings use host coordinates; compare with a manual measurement in Revit |
| E7 | Nested link | Same as E6 through two levels |

## F. The three supplied files

Follow `docs/07-federated-models.md`, "How to verify the supplied files". Record the outcome in the
table below.

| Item | Result | Date | Tester |
|---|---|---|---|
| ARC opens (copy), Revit version | | | |
| MEP opens (copy), workshared/detached | | | |
| STR `.rte` contains structural instances? | | | |
| Units and base point of each | | | |
| Aligned federation achieved with positioning | | | |

## D. Architect rule files (new, not yet run in Revit)

Rules are loaded from the shipped `rule-register.csv` plus every `*.json` file in `%APPDATA%\RevitAccessibility\rules\`
(the folder is not created for you). Format: see `docs/09-rule-definition-schema.md`.

| # | Step | Expected |
|---|---|---|
| D1 | Run with no `rules` folder | Same results as before; the window lists the placeholder rules (`CORR-W`, `RAMP-SLOPE`, `THRESH-H`) under "Rules not run" with reasons. |
| D2 | Add a valid `*.json` rule for a supported category (see doc 09) with a threshold you choose and a TEST-ONLY source type | Its findings appear; every message carries the UNVERIFIED label; the banner names the rule id. |
| D3 | Put a file with broken JSON in the folder | The window lists `(file <name>)` under "Rules not run" with the reason; other rules still run. |
| D4 | Add a rule with an id that already exists in the register | Listed as not run ("Duplicate rule id"); the shipped rule's threshold still applies. |
| D5 | Add a rule that uses an unimplemented method or rule type | Listed as not run with the `EXE-...` reason; no findings for it. |
| D6 | Check the status filter and summary | "Needs review" is selectable and counted separately from Cannot evaluate. |
