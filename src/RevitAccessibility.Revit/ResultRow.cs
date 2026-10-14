using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Reporting;

namespace RevitAccessibility.Revit;

public sealed class ResultRow
{
    public ResultRow(Finding f, string level, string mark)
    {
        Finding = f;
        Level = level;
        Mark = mark;
        Status = FindingCsvExporter.StatusText(f.Status);
        // Text + icon, never colour alone.
        StatusIcon = f.Status switch { FindingStatus.Pass => "✔", FindingStatus.Fail => "✖", _ => "?" };
        Severity = f.Severity.ToString();
        Message = f.Message;
        Clause = f.ClauseRef;
    }

    public Finding Finding { get; }
    public string Level { get; }
    public string Mark { get; }
    public string Status { get; }
    public string StatusIcon { get; }
    public string StatusDisplay => $"{StatusIcon} {Status}";
    public string Severity { get; }
    public string Message { get; }
    public string Clause { get; }
}
