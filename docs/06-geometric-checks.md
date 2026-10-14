# 06. Checks: specification, limits and tests

This document states, for every check, what it needs, how it measures, when it applies, what it
reports, what it cannot do, and which tests cover it. It describes **what the code does today**.
Where something is designed but not built, it says so.

All lengths inside Core are **millimetres in the host model's coordinates**. Revit stores feet; the
Revit side converts once (`UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters)`).
Core never sees feet.

## Result statuses

| Status | Meaning | Counts as a pass? |
|---|---|---|
| Pass | The measurement meets the requirement. | Yes |
| Fail | The measurement does not meet the requirement. | No |
| Cannot evaluate | An input is missing or invalid. The reason is listed in `MissingInputs`. | No |
| Needs review | A person must look: a potential clash, or a measurement whose meaning is not the legal quantity. | No |

Two different things are both called "needs review" in this project. Do not confuse them:

* A **result** of *Needs review* is about one element in one run.
* A **register status** of *Needs review* means a legal *value* has not yet been verified by the two
  verifiers. Findings that use such a value say `UNVERIFIED` in their text and in the
  `VerificationStatus` column, whatever their result.

## Fields on every finding (`FindingDetail`)

Rule version, check type (Regulatory or Geometric), source document, linked document (if the element
comes from a link), measured value and unit, measurement source and method, expected condition,
verification status of the legal value, missing inputs, and the related element for pair checks.
They appear in the window's detail pane and in the CSV. The CSV can also start with a report-context
line (tool version, rule-register version, config source, document title, Revit version, time).

## Check 1: door clear width (regulatory, values unverified)

| | |
|---|---|
| Rule id / version | `DOOR-CLEAR-W`, version 2 |
| Required input | Per door: classification (Access door Yes/No), a width in mm, and the **declared meaning** of that width. |
| Measurement method | The configured Revit length parameter (instance first, then type), converted to mm once. Compared **before** display rounding, with a tolerance of 0.0005 mm. |
| Applicability | Every door instance. Thresholds come only from the register: access door 800 mm, other doors 750 mm (status *Needs review*). |
| Expected result | Declared clear opening: Pass if width ≥ threshold, otherwise Fail. Nominal or undeclared width: **Needs review**, never Pass or Fail. Missing classification, missing width, or invalid width (0, negative, NaN, infinity): Cannot evaluate. |
| Why the tolerance | Revit stores feet. 800 mm can come back as 799.9999999 mm and would wrongly fail. 0.0005 mm is far below any real modelling difference, and a real shortfall of 0.001 mm still fails. |
| Known limits | The tool cannot know whether a parameter really holds the clear opening (luce netta). The owner declares it in `config.json` (`"ClearWidthMeaning": "ClearOpening"`). The default is `NominalProxy`, so out of the box the check gives Needs review. A Yes/No parameter that was never touched in Revit has no stored value and is reported as unclassified. The legal values themselves are unverified. |
| Tests | `DoorProvenanceTests`, `DoorClearWidthRuleTests`, `DoorToleranceTests`, `ClearWidthMeaningConfigTests` |

**Not implemented:** reading the clear opening from geometry (frame depth, leaf thickness, door
swing). Until then a real clear opening must come from a parameter the designer maintains.

## Check 2: register-driven length rule (regulatory, no rows are live yet)

| | |
|---|---|
| Rule id / version | The register row id, version 1 |
| Required input | A register row with Category, Parameter, operator `>=` or `<=`, a numeric threshold, unit `cm` or `mm`, severity, clause. |
| Measurement method | The named Revit **Length** parameter on the element or its type, converted to mm once. |
| Applicability | Categories the Revit side supports: Doors, Windows, Walls, Rooms, Floors, Stairs, Ramps, Railings. |
| Expected result | Pass or Fail by the operator. A missing value, a value of 0, a negative or non-finite value, or an unread parameter: Cannot evaluate. Rows whose status is not Verified are labelled UNVERIFIED. |
| Known limits | Lengths only (no slopes or percentages). A value of 0 is treated as "not measured", which also means a legitimately flush threshold of 0 mm reads as Cannot evaluate. This is deliberate: never silently pass. Today every non-door register row is `[TBD]`, so **no generic rule actually runs**; they appear under "Rules not run". |
| Tests | `ParameterRuleTests`, `ParameterRuleFactoryTests` |

## Check 3: potential clash between two categories (geometric)

This is a **coordination** check. It cites no law and its result is never a legal Fail.

| | |
|---|---|
| Rule id / version | Supplied by the rule specification, for example `CLASH-DUCT-BEAM`, version 1 |
| Required input | For each element in the two categories: identity (document id + UniqueId), an axis-aligned bounding box in **host millimetres**, and a geometry status. Optionally the UniqueId of the element it is hosted in. |
| Measurement method | **Step 1, broad phase (Core, built):** the boxes must overlap by *more than* the tolerance on all three axes. Touching boxes do not count. **Step 2, exact test (Revit side, not yet built):** an exact solid-intersection test confirms or rules out each candidate pair. Core accepts the result per pair. |
| Applicability | One rule per pair of categories. The tolerance (default 1 mm) is a modelling tolerance agreed by the BIM coordinator. It is not a regulation value. |
| Excluded on purpose | An element and itself. An element and the element it is hosted in, within the same document (a door in its wall is an intentional connection). |
| Expected result | No overlap, or exact test says no intersection: **no finding**. Overlap with no exact test: **Needs review**, "potential clash, broad-phase only". Exact test confirms: **Needs review**, "confirmed geometric intersection, review whether permitted". Exact test failed: Needs review. Geometry missing, invalid or unsupported for an in-scope element: **Cannot evaluate**, and the message says it is not a "no clash" result. The same identity supplied twice: reported once, then used once. |
| Known limits | A bounding-box overlap is **not** proof of a clash, and the rule never calls it one. Rotated elements have large boxes, so the broad phase can report extra candidates (it never misses real ones). The scan is O(n × m) with a cheap rejection, fine for thousands of elements per category; a spatial grid is the upgrade path. **The absence of a finding is not a pass.** Only pairs that overlap produce findings. There is no rule for permitted penetrations beyond the hosted-element exclusion. |
| Tests | `ClashRuleTests` (candidate selection, tolerance, exclusions, duplicates, unusable geometry, classification, traceability, a 3,000-element performance check) |

**Not implemented:** reading bounding boxes and the exact intersection from Revit. That is Phase 2
and needs a machine where the add-in can be compiled and run.

## Check 4: spatial relationships (design only)

Relationships such as "a door lies inside a wall" or "an element belongs to a room" need reliable
solid or room geometry and a verified coordinate system. They are **not** implemented. The hosted
relationship used by Check 3 comes from Revit's host information, not from geometry.

## Geometry building blocks (built and tested)

| Type | Purpose |
|---|---|
| `Aabb` | Validated box; refuses non-finite values and min > max. |
| `RigidTransform` | Rotation plus translation in mm. Rejects scaling, shearing, mirroring and non-finite values. Composes, inverts, and transforms a box (conservatively, so a rotated box only grows). |
| `LinkResolver` | Composes link transforms through nested links into host coordinates. Reports unloaded, missing or unsupported links, missing transforms, unknown parents, cycles, duplicate ids and chains deeper than 32. |
| `GeoElementDto` | An element's geometry summary; identity is (document id, UniqueId). |

Tests: `AabbTests`, `RigidTransformTests`, `LinkResolverTests`.

## Status of verification

* Core logic above: built and covered by automated tests (see the README for the current count).
* Everything that runs inside Revit (model extraction, exact intersection, link handling in a real
  model): **not yet built or run**. See `docs/07-federated-models.md` and
  `docs/08-revit-manual-test-procedure.md`.
