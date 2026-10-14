using System.Globalization;
using System.Text;
using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.Reporting;

/// <summary>
/// Facts about the run that produced a report, so a result can be tied to the exact tool, rules and model.
/// Every field is optional: a field that is not known is simply left out.
/// </summary>
public sealed record ReportContext(
    string? ToolVersion = null,
    string? RegisterVersion = null,
    string? ConfigSource = null,
    string? DocumentTitle = null,
    string? RevitVersion = null,
    DateTime? GeneratedUtc = null)
{
    public string Describe()
    {
        var parts = new List<string>();
        if (ToolVersion is not null) parts.Add($"tool {ToolVersion}");
        if (RegisterVersion is not null) parts.Add($"rule register {RegisterVersion}");
        if (ConfigSource is not null) parts.Add($"config {ConfigSource}");
        if (DocumentTitle is not null) parts.Add($"document {DocumentTitle}");
        if (RevitVersion is not null) parts.Add($"Revit {RevitVersion}");
        if (GeneratedUtc is not null) parts.Add($"generated {GeneratedUtc.Value.ToString("u", CultureInfo.InvariantCulture)}");
        return "Report context: " + (parts.Count == 0 ? "not recorded" : string.Join("; ", parts));
    }
}

/// <summary>Formats findings as CSV text. Every export starts with the disclaimer.</summary>
public static class FindingCsvExporter
{
    public const string Header =
        "Status,Severity,CheckType,RuleId,RuleVersion,Clause,VerificationStatus,Level,Mark,ElementUid,SourceDocument," +
        "RelatedElement,MeasuredValue,Unit,MeasurementSource,ExpectedCondition,MissingInputs,Message";

    public static string Export(IEnumerable<Finding> findings, BuildingModel model, ReportContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(model);

        var info = new Dictionary<string, (string Level, string Mark)>(StringComparer.Ordinal);
        foreach (var d in model.Doors) info.TryAdd(d.Uid, (d.Level, d.Mark ?? ""));
        foreach (var el in model.AllElements) info.TryAdd(el.Uid, (el.Level, el.Mark ?? ""));
        foreach (var g in model.AllGeoElements) info.TryAdd(g.Uid, (g.Level, g.Mark ?? ""));

        var sb = new StringBuilder();
        sb.Append(Cell(Disclaimer.Text)).Append("\r\n");
        if (context is not null) sb.Append(Cell(context.Describe())).Append("\r\n");
        sb.Append(Header).Append("\r\n");

        foreach (var f in findings)
        {
            var (level, mark) = info.GetValueOrDefault(f.ElementUid);
            var d = f.Detail;
            sb.Append(string.Join(",",
                Cell(StatusText(f.Status)),
                Cell(f.Severity.ToString()),
                Cell(d?.CheckType.ToString() ?? ""),
                Cell(f.RuleId),
                Cell(d?.RuleVersion ?? ""),
                Cell(f.ClauseRef),
                Cell(d?.VerificationStatus ?? ""),
                Cell(level ?? ""),
                Cell(mark ?? ""),
                Cell(f.ElementUid),
                Cell(d?.SourceDocument ?? ""),
                Cell(d?.RelatedElement ?? ""),
                Cell(d?.MeasuredValue is { } v ? v.ToString("R", CultureInfo.InvariantCulture) : ""), // full precision, not display-rounded
                Cell(d?.Unit ?? ""),
                Cell(d?.MeasurementSource ?? ""),
                Cell(d?.ExpectedCondition ?? ""),
                Cell(d?.MissingInputs is { Count: > 0 } m ? string.Join("; ", m) : ""),
                Cell(f.Message)));
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Exhaustive on purpose: a new status must be handled here, never silently shown as another one.</summary>
    public static string StatusText(FindingStatus s) => s switch
    {
        FindingStatus.Pass => "Pass",
        FindingStatus.Fail => "Fail",
        FindingStatus.CannotEvaluate => "Cannot evaluate",
        FindingStatus.NeedsReview => "Needs review",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unhandled finding status."),
    };

    // Quotes the cell and neutralises spreadsheet formula injection (=, +, -, @ at the start).
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
