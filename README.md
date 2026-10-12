# Revit Accessibility Pre-Check (Sample Project)

A pre-check tool that reads a Revit model and reports elements that may not meet
accessibility requirements, with the regulation clause cited for each finding.

**This tool is a pre-check. It does not certify compliance.** Every result must be
reviewed by a qualified professional before it is relied on.

## Status

| Item | Status |
|---|---|
| Project plan and guides | Complete (this folder) |
| Rule register (thresholds) | **Placeholders only.** No value is verified yet. |
| Architect workflow (Dynamo + Excel) | Documented; graph not yet built |
| Core rule engine (C#) | Implemented with door clear-width rule; 20 tests pass (see `src/`) |
| Revit add-in (C#) | Planned; not yet implemented |

## Who this is for

| Reader | Start here |
|---|---|
| Architect or compliance reviewer (no coding) | `docs/02-architect-guide-dynamo.md` |
| Developer building the add-in | `docs/03-developer-guide-csharp.md` |
| Anyone verifying rule values | `docs/04-rule-verification-checklist.md` |
| Project owner planning next steps | `docs/05-roadmap-and-decisions.md` |
| Overview of the whole project | `docs/01-project-overview.md` |

## Folder contents

```
revit-accessibility-precheck/
  README.md                          this file
  docs/
    01-project-overview.md           what the tool does, architecture, risks
    02-architect-guide-dynamo.md     step-by-step for non-programmers (Dynamo)
    03-developer-guide-csharp.md     step-by-step for the C# add-in
    04-rule-verification-checklist.md how to verify each regulation value
    05-roadmap-and-decisions.md      phases, decisions, open questions
  rules/
    rule-register.csv                the rule table (edit values here, not in code)
  test-model/
    known-results-test-cases.md      reference cases to validate the checker
```

## Before publishing to GitHub

- Do not commit client or friend project files (`.rvt`, `.rfa`, `.ifc`).
- Do not commit Autodesk DLLs. Use the Revit API NuGet package instead.
- Keep every unverified threshold marked as a placeholder.
- Choose a licence (MIT is a common choice for a sample).

## Licence

Not yet chosen. Add a `LICENSE` file before publishing.
Autodesk Revit API use is governed by Autodesk's own terms.
