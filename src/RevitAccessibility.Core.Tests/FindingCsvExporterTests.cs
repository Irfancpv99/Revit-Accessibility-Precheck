using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Reporting;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class FindingCsvExporterTests
{
    [Fact]
    public void Export_starts_with_disclaimer_and_has_one_row_per_finding()
    {
        var model = new BuildingModel([new DoorDto("u1", "L0", 700, true, "D-01")]);
        var findings = new[] { new Finding("R", "Clause, with comma", Severity.Error, FindingStatus.Fail, "u1", "msg \"q\"") };
        var lines = FindingCsvExporter.Export(findings, model).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains(Disclaimer.Text, lines[0]);
        Assert.Contains("\"D-01\"", lines[2]);
        Assert.Contains("\"Clause, with comma\"", lines[2]);
        Assert.Contains("\"msg \"\"q\"\"\"", lines[2]);
    }

    [Fact]
    public void Formula_like_cells_are_neutralised()
    {
        var model = new BuildingModel([new DoorDto("u1", "L0", 700, true, "=HYPERLINK(\"x\")")]);
        var csv = FindingCsvExporter.Export([new Finding("R", "C", Severity.Info, FindingStatus.Pass, "u1", "m")], model);
        Assert.Contains("\"'=HYPERLINK", csv);
    }

    [Fact]
    public void Finding_for_unknown_element_still_exports()
    {
        var csv = FindingCsvExporter.Export([new Finding("R", "C", Severity.Info, FindingStatus.CannotEvaluate, "zz", "m")], new BuildingModel([]));
        Assert.Contains("\"Cannot evaluate\"", csv);
    }
}
