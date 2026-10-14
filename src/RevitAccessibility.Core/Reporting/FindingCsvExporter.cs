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

        var info = new Dictionary<string, (string Level, string Mark)>(StringComparer.Ordinal);
        foreach (var d in model.Doors) info.TryAdd(d.Uid, (d.Level, d.Mark ?? ""));
        foreach (var el in model.AllElements) info.TryAdd(el.Uid, (el.Level, el.Mark ?? ""));
        var sb = new StringBuilder();
        sb.Append(Cell(Disclaimer.Text)).Append("\r\n");
        sb.Append("Status,Severity,RuleId,Clause,Level,Mark,ElementUid,Message\r\n");

        foreach (var f in findings)
        {
            var (level, mark) = info.GetValueOrDefault(f.ElementUid);
            sb.Append(string.Join(",",
                Cell(StatusText(f.Status)), Cell(f.Severity.ToString()), Cell(f.RuleId), Cell(f.ClauseRef),
                Cell(level ?? ""), Cell(mark ?? ""), Cell(f.ElementUid), Cell(f.Message)));
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
