# Known-Results Test Cases

Use a test model you created yourself (never a client model). Each door below must
produce exactly the stated result. Values are for testing the checker only; they are
not regulation thresholds.

Test thresholds used here: **minimum 800 mm** (demo value only, not verified).

| Test door | Level | Clear width (mm) | Expected status | Expected message type |
|---|---|---|---|---|
| T-01 | L0 | 700 | Fail | Below minimum |
| T-02 | L0 | 800 | Pass | Meets minimum (boundary) |
| T-03 | L0 | 799 | Fail | Below minimum (one unit under) |
| T-04 | L1 | 900 | Pass | Meets minimum |
| T-05 | L1 | (parameter absent) | Cannot evaluate | Missing data |
| T-06 | L1 | (parameter present, empty) | Cannot evaluate | Missing data |
| T-07 | L2 | 1 | Fail | Very small value (checks unit handling) |

## Unit check

Create one door 800 mm wide in the model. The check must read 800, not 262 (feet
misread) and not 0.8 (metres misread). If the value is wrong, stop and fix the unit
conversion before any other test.

## Pass criteria

- Every test door appears in the output.
- Each status matches the expected column.
- No door is missing from the output.
- The check leaves the model unchanged.

## Record of runs

| Date | Revit version | Checker version | Result | Tester |
|---|---|---|---|---|
| | | | | |
