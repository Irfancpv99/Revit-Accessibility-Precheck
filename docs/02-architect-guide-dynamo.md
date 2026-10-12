# 2. Architect Guide: Running the Check with Dynamo (No Coding)

This guide is for architects and compliance reviewers. You do not need to write any code.

## 2.1 What you need

- Revit 2026 with the project open.
- Dynamo (included with Revit; open it from the Manage tab, or the Dynamo icon).
- Dynamo Player (included with Dynamo; runs a finished graph without editing it).
- The rule register file `rules/rule-register.csv`, opened in Excel.
- The Dynamo graph file (`.dyn`), provided by the developer. *The graph is not built yet; see section 2.7.*

## 2.2 Step 1: Quick check with a Revit schedule (10 minutes, no Dynamo)

Use this first to confirm the door parameter exists and to see roughly how many doors fail.

1. In Revit, go to **View > Schedules > Schedule/Quantities**.
2. Choose **Category: Doors**, then OK.
3. Add the field **Clear Width** (or the name your families use). Add **Level** and **Mark**.
4. Open the schedule's **Filter** settings and add: Clear Width is less than [threshold].
   Use the threshold in the rule register (once it is verified).
5. Read the list. Each row is a door that may fail.

Limits: this shows only doors, only one rule, and no regulation citation. Use it as a
first look, not as a report.

## 2.3 Step 2: Open the rule register

Open `rules/rule-register.csv` in Excel. Each row is one rule:

| Column | Meaning |
|---|---|
| RuleID | Unique code for the rule |
| Clause | The regulation article it comes from |
| Category | The element type (e.g., Doors) |
| Parameter | The Revit parameter name to read |
| Operator | The comparison (>= or <=) |
| Threshold | The limit value |
| Unit | Usually mm |
| Severity | Error (Fail) or Info |
| Status | Placeholder, Verified, or Needs review |
| VerifiedBy and Date | Who checked the value, and when |

**Important:** rows marked **Placeholder** must not be used for a real project. Their
thresholds are not yet verified.

## 2.4 Step 3: Run the full check

1. Open the Revit project.
2. Open **Dynamo Player** (Manage tab > Visual Programming > Dynamo Player).
3. Click **Open**, choose the graph file provided by the developer, then click **Run**.
4. When the graph finishes, it will:
   - Write an Excel report (default location shown in the graph).
   - Colour failing elements in the active view, if the graph includes that step.
5. Note the time you ran it and the Revit file name. Keep this with the report.

## 2.5 Step 4: Read the report

The Excel report has one row per checked element:

| Column | Meaning |
|---|---|
| ElementId | Revit element ID; use this to find the element |
| Level | Building level |
| Mark | The door tag (e.g., D-0142) |
| RuleID | Which rule was checked |
| Clause | Regulation article |
| Measured | The value read from the model |
| Required | The threshold |
| Status | Fail, Cannot evaluate, or Pass |

How to work through it:

1. Filter **Status** to **Fail** first.
2. Sort by **Level**, then go through the doors on each floor.
3. For each Fail, find the door (by Mark or ElementId) and decide: fix it, or record
   why it is acceptable and who approved it.
4. Then review **Cannot evaluate** rows. These are usually a missing parameter in the
   door family, not a design fault. Pass them to the BIM team.

## 2.6 Step 5: Record your decisions

Keep a simple log next to the report (a separate sheet is best). For each Fail, record:

- Date, reviewer name, element Mark
- Decision: Fixed / Accepted with reason / Referred to specialist
- If accepted: the reason and the professional who approved it

## 2.7 Things you cannot do yourself (ask the developer)

- Build or change the Dynamo graph itself.
- Add a new rule type that the graph does not support.
- Fix a Dynamo error after a Revit upgrade.
- Map a new parameter name when a firm uses different family conventions.

You can change thresholds yourself, but only in the rule register, and only after the
checklist in doc 04 is complete.

## 2.8 Troubleshooting

| Problem | Likely cause | What to do |
|---|---|---|
| Many doors show "Cannot evaluate" | Door family lacks the Clear Width parameter | Ask BIM team to add it; check parameter name |
| Graph fails to open | Different Revit or Dynamo version | Use the Revit version the graph was built for |
| Report is empty | No doors in the project, or wrong category | Check the project has doors and the graph is set to Doors |
| Measured values look wrong (e.g., 262 instead of 800) | Unit conversion problem | Stop and report to developer; do not use the results |
| Results differ from schedule | Schedule and graph read different parameters | Compare the parameter names; report to developer |

## 2.9 Golden rules for reviewers

1. The tool points to possible problems; it does not decide compliance.
2. Never use a threshold marked Placeholder.
3. "Cannot evaluate" is a data problem to fix, not a pass.
4. Keep records of every decision.
