# 07. Preparing for linked and federated models

**Status: architecture prepared and tested in Core. Not implemented in the Revit add-in. Not validated
on any real linked model.** No federated model exists yet, and none of the three supplied files has
been opened or inspected by the developer. Do not read this document as a claim that they have.

## The three supplied files

| Role | File | Notes |
|---|---|---|
| Architectural | ARC model file (`.rvt`) | A project file by extension. Contents not inspected. |
| MEP | MEP model file (`.rvt`) | Its file name says it was "dissociato" (detached), so it was probably detached from a central model. Contents not inspected. |
| Structural | STR file (`.rte`) | **`.rte` is normally a Revit template, not a project.** Whether it holds real structural elements is unknown. |

The real file names are deliberately not recorded here: they identify a client project and this
repository is public.

Their coordinates, units, link transforms and mutual compatibility are unvalidated.

## Design rules

1. **Native-model checks never depend on a federated model.** A check on one open project works on
   its own.
2. **Never combine raw coordinates from different documents.** Every cross-document comparison goes
   through a rigid transform into the host's coordinates (`LinkResolver`).
3. **Identity is (document id, UniqueId).** The same UniqueId in two documents is two elements. Two
   instances of one linked file are two documents.
4. **Unavailable is reported, not skipped.** An unloaded, missing or unsupported link, a missing
   transform, a cycle or a duplicate id makes that document's elements *Cannot evaluate*. It never
   makes them "no clash".
5. **Rigid transforms only.** Scaled, sheared or mirrored transforms are rejected.

## Coordinate assumption (to be confirmed in Revit)

The transform for a link is the one Revit gives for the link instance: it maps the linked file's
internal coordinates into its parent's internal coordinates. Core composes these along nested links
to host coordinates. **Shared coordinates are not used.** If two files were positioned in the host
using shared coordinates, the link instance transform already reflects that, but this must be
confirmed on a real model before the cross-model check is trusted.

Translations are millimetres. Revit gives feet, so the Revit side must convert the translation too,
not only the points.

## What is handled in Core today

| Situation | Behaviour |
|---|---|
| Host only | Identity transform |
| Nested links | Transforms composed in the right order (tested with rotation) |
| Unloaded, missing or unsupported link | Document unusable, reason given |
| Link below an unloaded link | Unusable, reason says an ancestor is the problem |
| Missing transform, unknown parent | Unusable, reason given |
| Cycle, duplicate id, no host, several hosts, depth over 32 | Unusable, reason given |
| Scaled, sheared or mirrored transform | Rejected |
| Duplicate elements | Reported, then used once |
| Duplicate pair findings | A pair is reported once regardless of order |

## What still has to be built (Phase 2, needs Revit 2026 + the Revit API)

* Read document identity (title, path, Revit version/build) for the host and every link instance,
  including whether it is loaded.
* Read the link instance transform, convert it to millimetres and pass it to `LinkResolver`.
* Read bounding boxes, transform linked ones into host coordinates, flag invalid geometry.
* Perform the exact intersection test on the Revit API thread for candidate pairs only, and return
  `PairConfirmationDto`s.
* Show the new `Needs review` status and the related element in the window; select an element in a
  linked document (today select-in-model only finds elements in the active document).

## How to verify the supplied files (safe procedure)

Do this on **copies**. Never open an original in a way that saves it, and never save over a source.

1. **Copy** the three files to a working folder, for example `C:\work\federation-test\`, and keep the
   originals read-only elsewhere.
2. **ARC and MEP (`.rvt`):** in Revit use *File > Open*, tick **Detach from Central** if the file is
   workshared (choose *Detach and preserve worksets*), and leave *Audit* off. Open the copies only.
   Note the Revit version that saved each file (a file from an older Revit is upgraded in memory;
   do not save it back).
3. **Check units and origin** in each: *Manage > Project Units*, and in a plan view show the Project
   Base Point and Survey Point. Write down the project units and where the two points sit.
4. **Structural `.rte`:** open the *copy* as a file. A template usually has views, levels and loaded
   families but **no model elements**. To check, create a schedule of Structural Framing, Structural
   Columns and Walls in the copy (do not save it). If it lists no instances, the file is a template,
   not a structural model.
   * If it is only a template: ask the colleague for the real structural **`.rvt`** (or the original
     federated model). Do not rename `.rte` to `.rvt`, and do not treat a converted template as a model.
   * If it does contain structural instances: *File > Save As > Project* into the working folder as a
     new `.rvt`, and continue with that copy.
5. **Build a throwaway host** (never one of the sources): start a new empty project, then
   *Insert > Link Revit* for each of the three, using **Auto - Origin to Origin** first. Save this new
   host in the working folder.
6. **Check alignment** in plan and 3D: do column grids, levels and cores line up across the models?
   If not, they use different origins; try **Auto - Project Base Point to Project Base Point** or
   **Shared Coordinates** *only if* the files are known to share coordinates. Record which positioning
   gave an aligned result. A misaligned federation gives meaningless clash results.
7. Record: the three Revit versions, the units, which positioning worked, and any warnings.

Until steps 1 to 7 are done and recorded, no cross-model result should be trusted.
