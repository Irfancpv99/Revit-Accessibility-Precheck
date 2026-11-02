# Rule definitions, validation and execution readiness (schema version 1)

Status: implemented in Core and unit-tested. The Revit add-in's `RunCheckCommand` now builds its rules through `RuleLibrary` (**written without a Revit build; not yet built or run in Revit**). No Rule Builder UI exists yet: architects currently write the JSON files by hand.

## What it adds

`src/RevitAccessibility.Core/RuleDefinitions/`:

| File | Purpose |
|---|---|
| `RuleDefinition.cs` | Versioned, declarative rule: identity, domain, rule type, source type, categories, applicability, measurement, evaluation, source reference, verification, lifecycle. Data only; no expressions or code. |
| `RuleCapabilities.cs` | Allowlist of what the engine really implements: methods `length-parameter` and `door-clear-width`; operators per method; categories the Revit adapter can read. |
| `RuleValidator.cs` | Produces a report with three separate lists: **Schema** errors, **Execution** errors, **Governance** warnings. `IsSchemaValid` and `IsExecutable` are separate; neither says anything about legal approval. |
| `RuleDefinitionJson.cs` | Strict JSON reader/writer (camelCase). Unknown fields, numeric enum values and malformed files are rejected. |
| `LegacyRegisterAdapter.cs` | Expresses `rules/rule-register.csv` rows as definitions. The CSV stays the single source of truth; nothing is copied or invented. Rows the loader rejected (for example `[TBD]` thresholds) are returned as "not run" with the reason. |
| `RuleLibrary.cs` | Loads the shipped register plus the architect's `*.json` files in `%APPDATA%\RevitAccessibility\rules\`; the register loads first so a user file can never replace a shipped rule with the same id. Unreadable or malformed files are reported as not run. Also builds the UNVERIFIED banner text. |
| `RuleSetBuilder.cs` | Validates definitions and builds runnable rules. Anything not schema-valid and executable is listed in `NotRun` with the validator's reasons, never dropped. |

## The three kinds of problem

- **Schema (`SCH-*`)**: malformed definition (bad id, missing section, unknown operator, non-positive threshold, unknown unit, legislation without a clause, `Verified` without clause, both reviewers and date).
- **Execution (`EXE-*`)**: well-formed but not runnable (rule type other than Dimension, unknown method, operator not implemented for the method, unsupported category, custom tolerance, deprecated version, door rule without clear-opening meaning or classification).
- **Governance (`GOV-*`)**: may run, but needs attention (not verified, not approved, no test cases, incomplete applicability, test-only).

## What runs today

- Rule type **Dimension** only, with method `length-parameter` (operators `>=`, `<=`; one Revit length parameter per category) or `door-clear-width` (operator `>=`; needs one access-door and one other-door definition).
- Legal verification does not block execution: unverified rules still run and every finding is labelled UNVERIFIED, as before.
- A definition cannot make something executable that the engine does not implement: unknown methods, operators, rule types and tolerances are refused with a reason.

## Rule lifecycle versus verification versus finding status

Three independent things: `lifecycle.status` (Draft, TechnicallyValidated, LegalReview, Approved, Deprecated), `verification.status` (Unverified, NeedsReview, Verified; Verified requires clause, architect and professor reviewers and a date) and the per-element finding status (Pass, Fail, Cannot evaluate, Needs review).

## Known limits

- `RuleCapabilities.SupportedCategories` duplicates the category list in `ModelSnapshotBuilder` and must be kept in sync.
- A user door rule pairs with the shipped door rules; adding a differently named door rule makes the door rules "not run" (reported, with the reason).
- The CSV does not hold applicability, so legacy-adapted rules always carry a `GOV-APPLICABILITY` warning. This is deliberate.
- The "rules not run" list is shown in the results window but is not yet written into the CSV export.
- Calculation, geometry, parameter-presence and manual-review rule types are defined in the schema but not implemented.
- A source inventory (documents, edition, extraction quality, currentness) is not built yet.
