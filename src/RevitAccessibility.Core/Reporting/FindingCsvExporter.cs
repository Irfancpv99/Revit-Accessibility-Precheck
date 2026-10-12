using System.Text;
using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.Reporting;

/// <summary>Formats findings as CSV text. Every export starts with the disclaimer.</summary>
public static class FindingCsvExporter
{
    public static string Export(IEnumerable<Finding> findings, BuildingModel model)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(model);

        var doors = model.Doors.ToDictionary(d => d.Uid, StringComparer.Ordinal);
        var sb = new StringBuilder();
        sb.Append(Cell(Disclaimer.Text)).Append("\r\n");
        sb.Append("Status,Severity,RuleId,Clause,Level,Mark,ElementUid,Message\r\n");

        foreach (var f in findings)
        {
            doors.TryGetValue(f.ElementUid, out var door);
            sb.Append(string.Join(",",
                Cell(StatusText(f.Status)), Cell(f.Severity.ToString()), Cell(f.RuleId), Cell(f.ClauseRef),
                Cell(door?.Level ?? ""), Cell(door?.Mark ?? ""), Cell(f.ElementUid), Cell(f.Message)));
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    public static string StatusText(FindingStatus s) => s switch
    {
        FindingStatus.Pass => "Pass",
        FindingStatus.Fail => "Fail",
        _ => "Cannot evaluate",
    };

    // Quotes the cell and neutralises spreadsheet formula injection (=, +, -, @ at the start).
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
