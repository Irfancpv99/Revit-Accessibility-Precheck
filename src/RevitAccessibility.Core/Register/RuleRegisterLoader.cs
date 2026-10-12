using System.Globalization;
using System.Text;

namespace RevitAccessibility.Core.Register;

/// <summary>One valid register row. <see cref="ThresholdMm"/> is already converted to millimetres.</summary>
public sealed record RegisterRule(
    string RuleId,
    string Clause,
    string Operator,
    double ThresholdMm,
    string Severity,
    string Status)
{
    public bool IsVerified => string.Equals(Status, "Verified", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A register row that was rejected, with the reason. Rejected rows are never silently dropped.</summary>
public sealed record RegisterError(int Line, string RuleId, string Reason);

public sealed record RegisterLoadResult(IReadOnlyList<RegisterRule> Rules, IReadOnlyList<RegisterError> Errors)
{
    public RegisterRule? Find(string ruleId) =>
        Rules.FirstOrDefault(r => string.Equals(r.RuleId, ruleId, StringComparison.Ordinal));
}

/// <summary>
/// Reads rules/rule-register.csv. The register stores values in cm (or mm); this loader converts to mm.
/// Rows with a blank or non-numeric threshold (including [TBD]), an unknown unit, or no clause are rejected.
/// </summary>
public static class RuleRegisterLoader
{
    private static readonly string[] RequiredColumns = ["RuleID", "Clause", "Operator", "Threshold", "Unit", "Severity", "Status"];

    public static RegisterLoadResult LoadFile(string path) => Load(File.ReadAllText(path));

    public static RegisterLoadResult Load(string csvText)
    {
        ArgumentNullException.ThrowIfNull(csvText);

        var records = ParseCsv(csvText);
        if (records.Count == 0)
            throw new FormatException("Register is empty: no header row.");

        var header = records[0].Fields;
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Count; i++) index[header[i].Trim()] = i;
        foreach (var col in RequiredColumns)
            if (!index.ContainsKey(col))
                throw new FormatException($"Register is missing required column '{col}'.");

        var rules = new List<RegisterRule>();
        var errors = new List<RegisterError>();

        foreach (var record in records.Skip(1))
        {
            string Get(string col) => index[col] < record.Fields.Count ? record.Fields[index[col]].Trim() : "";

            string id = Get("RuleID");
            if (id.Length == 0)
            {
                errors.Add(new RegisterError(record.Line, "", "Row has no RuleID."));
                continue;
            }

            string clause = Get("Clause");
            if (clause.Length == 0)
            {
                errors.Add(new RegisterError(record.Line, id, "Row has no clause reference."));
                continue;
            }

            string raw = Get("Threshold");
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                || !double.IsFinite(value) || value <= 0)
            {
                errors.Add(new RegisterError(record.Line, id,
                    raw.Length == 0 ? "Threshold is blank." : $"Threshold '{raw}' is not a positive number."));
                continue;
            }

            string unit = Get("Unit");
            double? factor = unit.ToLowerInvariant() switch
            {
                "cm" => 10.0,
                "mm" => 1.0,
                _ => null,
            };
            if (factor is null)
            {
                errors.Add(new RegisterError(record.Line, id, $"Unit '{unit}' is not supported (expected cm or mm)."));
                continue;
            }

            rules.Add(new RegisterRule(id, clause, Get("Operator"), value * factor.Value, Get("Severity"), Get("Status")));
        }

        return new RegisterLoadResult(rules, errors);
    }

    private sealed record Record(int Line, List<string> Fields);

    // Minimal RFC 4180 parser: quoted fields, doubled quotes, commas and newlines inside quotes.
    private static List<Record> ParseCsv(string text)
    {
        var records = new List<Record>();
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false, any = false;
        int line = 1, recordLine = 1;

        void EndField() { fields.Add(sb.ToString()); sb.Clear(); }
        void EndRecord()
        {
            EndField();
            if (!(fields.Count == 1 && fields[0].Length == 0 && !any))
                records.Add(new Record(recordLine, fields));
            fields = new List<string>();
            any = false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (i == 0 && c == '﻿') continue;

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else { if (c == '\n') line++; sb.Append(c); }
                continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; any = true; break;
                case ',': EndField(); any = true; break;
                case '\r': break;
                case '\n':
                    EndRecord(); line++; recordLine = line; break;
                default: sb.Append(c); any = true; break;
            }
        }
        if (sb.Length > 0 || fields.Count > 0 || any) EndRecord();
        return records;
    }
}
