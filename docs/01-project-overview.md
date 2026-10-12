# 1. Project Overview

## 1.1 Purpose

Architects and designers need to find accessibility problems early, while a model is
still cheap to change. This project provides a pre-check that:

1. Reads building elements from a Revit model (starting with doors).
2. Compares measured values with the thresholds in a rule register.
3. Reports each element as **Pass**, **Fail**, or **Cannot evaluate**.
4. Cites the regulation clause behind every finding.
5. Lets the user see, filter, and export the results.

## 1.2 What it is not

- It is not a certificate of compliance.
- It is not a replacement for a professional review.
- It does not change the model.
- It does not check rules that need information the model does not contain.

## 1.3 Three result types

| Status | Meaning | Action |
|---|---|---|
| Fail | The measured value breaks a rule threshold. | Review and fix, or record why not. |
| Cannot evaluate | The model lacks the data needed to decide. | Add the parameter or fix the family. |
| Pass | The value meets the threshold. | None (hidden by default). |

"Cannot evaluate" must never be shown as "Pass". A silent pass is the most dangerous
failure of a compliance tool.

## 1.4 Delivery options

| Option | Who builds it | Who maintains it | Use when |
|---|---|---|---|
| A. Dynamo graph + Excel rule register | Developer builds once | Architects edit Excel | Immediate use by design teams |
| B. C# Revit add-in with a tested rule engine | Developer | Developer | Long-term product, shipped to others |
| C. pyRevit Python script | Developer | Developer | Quick scripting alternative to C# |
| D. Commercial checker (e.g., Solibri) | Vendor setup | Vendor and firm | Firm already owns the software |

The recommended sequence is **A first**, because it needs no software development
for day-to-day use, then **B** once the rules are stable.

## 1.5 Architecture (for option B)

```
Revit model
   |
   v
Adapter (Revit API)      reads doors, parameters, levels; converts units
   |
   v
Neutral model (DTOs)     plain data, no Revit references
   |
   v
Rule engine (Core)       applies IRule implementations; returns Findings
   |
   v
Results UI               grid, filters, detail pane, CSV export
```

Key rule: the **Core** library must not reference any Autodesk assembly. This is what
makes the rules testable without Revit installed.

## 1.6 Main risks

| Risk | Effect | Mitigation |
|---|---|---|
| Wrong threshold value | Wrong results for every project | Verification checklist (doc 04); no value used unverified |
| Inconsistent family parameters | Many "Cannot evaluate" results | Model-quality pre-check; parameter mapping per firm |
| Designers rely on the tool as certification | Liability and safety issues | Permanent disclaimer; "pre-check" wording |
| Revit version differences | Add-in or Dynamo graph breaks | Lock Revit version per project |
| Client data published on GitHub | Confidentiality breach | Strict .gitignore; sample models only |
| Tool ignored because too noisy | Problems not caught | Clear severity; hide passes; stable rule set |

## 1.7 Glossary

- **BIM**: Building Information Modeling.
- **Clear width**: the usable opening width of a door, after the frame.
- **Dynamo**: visual programming tool included with Revit.
- **DTO**: a plain data object passed between layers.
- **Rule register**: the table of thresholds, clauses, and severities.
- **BCF**: BIM Collaboration Format, an open standard for exchanging issues.
