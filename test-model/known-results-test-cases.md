# Known-Results Test Cases

Use a test model you created yourself (never a client model). Each door below must
produce exactly the stated result. Values are for testing the checker only.

Thresholds come from `rules/rule-register.csv` (currently status "Needs review", so every
result is labelled UNVERIFIED): **access door 800 mm, other door 750 mm.**

## Model setup (once)

1. Set Project Units > Length to Millimeters (display only; the tool always reads true values).
2. Manage > Shared Parameters > Browse: `test-model/AccessibilityParameters.txt`.
3. Manage > Project Parameters > Add > Shared parameter > **Access door**: Instance, group Identity Data, category **Doors** only.
4. The default Revit door has no "Clear Width" parameter. For testing, `%APPDATA%\RevitAccessibility\config.json` points the tool at the type parameter **Width**:
   `{ "ClearWidthParameter": "Width", "AccessDoorParameter": "Access door" }`.
   Nominal Width is not the same as luce netta; real families need a proper clear-width parameter.
5. Create one door type per row below (Edit Type > Duplicate, set Width) and place one door of each.

**Access door is a Yes/No parameter that starts with no stored value.** Tick it for Yes. For No,
tick and then untick it, so that a value is stored. A door that was never touched is "unset" and
reports Cannot evaluate, by design.

## Declared meaning of the width (important)

A door type's `Width` is a **nominal** width. It is not the legal clear opening (luce netta). The
core rule therefore only gives Pass or Fail when the width's meaning has been declared to be the
clear opening, with `"ClearWidthMeaning": "ClearOpening"` in `config.json`. The default is
`"NominalProxy"`, and then every door with a width reports **Needs review**, never Pass or Fail.

* With the default config, expect **Needs review** on every door in the table below that has a
  Yes or No stored.
* To exercise the thresholds in a test, set `ClearWidthMeaning` to `ClearOpening` for the test only
  and treat the results as test results. Do not use that setting with a nominal parameter on a real
  project.
* **The add-in does not pass this declared meaning to the rule yet** (planned, see
  `docs/07-federated-models.md`, "What still has to be built"). Until it does, every door with a width
  is reported as Needs review. The Pass/Fail rows below describe the behaviour once that is built and
  are verified only by the automated Core tests, not yet in Revit.

## Cases

| Test door | Width type (mm) | Access door | Expected status | Why |
|---|---|---|---|---|
| T-01 | 700 | Yes | Fail | Below 800 |
| T-02 | 800 | Yes | Pass | Exactly at the access threshold (boundary) |
| T-03 | 799 | Yes | Fail | One unit under 800 |
| T-04 | 749 | No | Fail | One unit under 750 |
| T-05 | 750 | No | Pass | Exactly at the other-door threshold (boundary) |
| T-06 | 790 | Yes | Fail | Same width as T-07, stricter class |
| T-07 | 790 | No | Pass | Same width as T-06, other-door class |
| T-08 | 900 | (never touched) | Cannot evaluate | Unclassified: message says the parameter has no value set |
| T-09 | 900 | Yes, with the parameter name wrong in config.json | Cannot evaluate | Message says the parameter was not found |

## Unit check

Create one door type with Width 800 mm. The check must read 800, not 262 (feet misread)
and not 0.8 (metres misread). If the value is wrong, stop and fix the unit conversion
before any other test.

## Pass criteria

- Every test door appears in the output.
- Each status matches the expected column.
- Every Cannot evaluate message says why (parameter not found, no value set, or wrong type).
- The window shows the UNVERIFIED banner, the parameters used, and the disclaimer footer.
- The CSV export starts with the disclaimer line.
- The check leaves the model unchanged (the model is not marked as modified afterwards).

## Record of runs

| Date | Revit version | Checker version | Result | Tester |
|---|---|---|---|---|
| | | | | |
