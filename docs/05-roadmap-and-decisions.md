# 5. Roadmap and Decisions

## 5.1 Phases

| Phase | Goal | Done when |
|---|---|---|
| 0. Verify | Confirm rule values and parameter names | Doc 04 complete for DOOR-CLEAR-W |
| 1. Architect workflow (Dynamo) | Working graph + Excel report | Test model gives expected results |
| 2. Core engine (C#) | Tested rules without Revit | `dotnet test` passes without Revit |
| 3. Revit add-in MVP | Results window and selection in Revit | Acceptance criteria in doc 03 met |
| 4. More rules | Corridors, ramps, thresholds, WCs | Each rule verified and tested |
| 5. Interchange | BCF export; optional IFC support | Issues open in a BCF viewer |
| 6. Public release | GitHub sample with licence and docs | Clean repo, no client data |

Phase 1 can start before phase 2. Phase 2 is needed only when the rule set grows.

## 5.2 Decisions made

| Decision | Reason |
|---|---|
| Rules stored in a register, not hardcoded | Regulation changes without code changes; reviewable by non-developers |
| Core free of Revit references | Rules testable without Revit |
| "Cannot evaluate" is a separate status | Missing data must never look like a pass |
| Pre-check wording and permanent disclaimer | Avoid implying certification |
| Dynamo first for architects | Usable without programming skills |
| Revit 2026 as the first target | Matches installed software |
| UniqueId used for element references | Survives save and reopen |
| Revit only (no ArchiCAD) for now | Dynamo and Revit API only; keeps scope achievable |
| Free to use now; paid version planned later | Schools and students first; commercial terms to be decided before any paid release |
| Two verifiers per rule: practising architect and university professor | Practice and teaching checked independently |
| Italian regulatory base first (DM 236/1989, DPR 503/1996, UNI EN 17210), with a Jurisdiction column | Matches Italian practice and schools |

## 5.3 Open questions

Answered:
- Revit only: yes (see 5.2).
- Free now, paid later: yes (see 5.2).
- Verifiers: practising architect and university professor (see doc 04, section 4.1).
- Building types: the rule set applies to all building types. Each rule is tagged
  BuildingType = All in the register. Per-type thresholds can be added as separate rows.
- First verifier (university and professor): deferred until after the MVP.

Still open (answer before phase 1 ends):
1. Door clear width: values entered as 80 cm (entrance) and 75 cm (other doors), taken from DM 236/1989 p. 8.1.1. Still to confirm from the official text: the DPR 503/1996 article that incorporates it, and the p. 8.0.1 measurement definition. Verifiers to check.
2. What is the exact door clear-width parameter name in the project families?
3. Which licence will the GitHub sample use, given the planned paid version later?

Scope note: DPR 503/1996 governs public buildings; DM 236/1989 governs private and
residential buildings. Private buildings are a later phase, and their rules go in
separate rows with BuildingType = Private, using DM 236/1989 as the source.

## 5.5 Commercial note for the later paid version

- The Revit API is governed by Autodesk's terms. Check whether selling a tool that uses
  it is permitted, and under which programme, before charging anyone.
- The licence chosen for the open GitHub sample (for example MIT) allows others to use
  and sell the code. If a paid tier is planned, decide now whether the core stays open
  and the paid part is services, rule sets, or support, or whether the code will later
  become closed.
- Free use by students does not automatically permit commercial use by a firm.

## 5.6 Things to avoid

- Publishing client models or Autodesk DLLs.
- Adding rules before the first rule is verified and tested.
- Building a web or cloud version before the desktop tool is trusted.
- Showing a percentage score of "compliance". It implies a judgement the tool cannot make.
