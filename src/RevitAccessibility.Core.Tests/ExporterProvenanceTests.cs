using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Reporting;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class ExporterProvenanceTests
{
    private static string[] Lines(string csv) => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    // Counts cells of a CSV row where every cell is quoted (except the header).
    private static int CountCells(string row) => row.Split("\",\"").Length;

    private static Finding Full(FindingStatus status = FindingStatus.Fail) =>
        new("DOOR-CLEAR-W", "DM 236/1989", Severity.Error, status, "u1", "msg",
            new FindingDetail("2", CheckType.Regulatory, "DOC-1", null, 799.9999999, "mm", "type parameter 'Width' (NominalProxy)",
                "at least 800 mm (access door)", "Needs review", ["declared clear-opening measurement"], "H:b"));

    [Theory]
    [InlineData(FindingStatus.Pass, "Pass")]
    [InlineData(FindingStatus.Fail, "Fail")]
    [InlineData(FindingStatus.CannotEvaluate, "Cannot evaluate")]
    [InlineData(FindingStatus.NeedsReview, "Needs review")]
    public void Every_status_has_its_own_text(FindingStatus status, string text) =>
        Assert.Equal(text, FindingCsvExporter.StatusText(status));

    [Fact]
    public void Status_texts_are_all_different()
    {
        var all = Enum.GetValues<FindingStatus>().Select(FindingCsvExporter.StatusText).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void An_unhandled_status_throws_instead_of_being_shown_as_another_status() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FindingCsvExporter.StatusText((FindingStatus)99));

    [Fact]
    public void Header_and_rows_have_the_same_number_of_columns()
    {
        var lines = Lines(FindingCsvExporter.Export([Full()], new BuildingModel([])));
        int headerCols = lines[1].Split(',').Length;
        Assert.Equal(headerCols, CountCells(lines[2]));
        Assert.Equal(18, headerCols);
    }

    [Fact]
    public void Row_without_detail_still_has_every_column()
    {
        var f = new Finding("R", "C", Severity.Info, FindingStatus.Pass, "u", "m");
        var lines = Lines(FindingCsvExporter.Export([f], new BuildingModel([])));
        Assert.Equal(18, CountCells(lines[2]));
    }

    [Fact]
    public void Provenance_fields_are_exported()
    {
        var csv = FindingCsvExporter.Export([Full()], new BuildingModel([]));
        Assert.Contains("\"Regulatory\"", csv);
        Assert.Contains("\"2\"", csv);
        Assert.Contains("\"Needs review\"", csv);
        Assert.Contains("\"DOC-1\"", csv);
        Assert.Contains("\"H:b\"", csv);
        Assert.Contains("\"mm\"", csv);
        Assert.Contains("NominalProxy", csv);
        Assert.Contains("at least 800 mm (access door)", csv);
        Assert.Contains("declared clear-opening measurement", csv);
    }

    [Fact]
    public void Measured_value_keeps_full_precision()
    {
        var csv = FindingCsvExporter.Export([Full()], new BuildingModel([]));
        Assert.Contains("\"799.9999999\"", csv);
    }

    [Fact]
    public void Measured_value_uses_a_dot_whatever_the_culture()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
            Assert.Contains("\"799.9999999\"", FindingCsvExporter.Export([Full()], new BuildingModel([])));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Formula_injection_is_neutralised_in_the_new_fields()
    {
        var f = new Finding("R", "C", Severity.Info, FindingStatus.Pass, "u", "m",
            new FindingDetail("1", CheckType.Regulatory, MeasurementSource: "=cmd|' /C calc'!A0", ExpectedCondition: "+1+1", RelatedElement: "@SUM(A1)"));
        var csv = FindingCsvExporter.Export([f], new BuildingModel([]));
        Assert.Contains("\"'=cmd", csv);
        Assert.Contains("\"'+1+1\"", csv);
        Assert.Contains("\"'@SUM(A1)\"", csv);
    }

    [Fact]
    public void Disclaimer_is_the_first_line_with_and_without_context()
    {
        var plain = Lines(FindingCsvExporter.Export([], new BuildingModel([])));
        var withCtx = Lines(FindingCsvExporter.Export([], new BuildingModel([]), new ReportContext("1.2.3")));
        Assert.Contains(Disclaimer.Text, plain[0]);
        Assert.Contains(Disclaimer.Text, withCtx[0]);
    }

    [Fact]
    public void Context_line_records_tool_register_config_document_and_revit()
    {
        var ctx = new ReportContext("1.2.3", "fa21679 / 2026-10-14", "C:/cfg.json", "Model A", "2026.5", new DateTime(2026, 10, 14, 6, 0, 0, DateTimeKind.Utc));
        var lines = Lines(FindingCsvExporter.Export([], new BuildingModel([]), ctx));
        Assert.Contains("tool 1.2.3", lines[1]);
        Assert.Contains("rule register fa21679", lines[1]);
        Assert.Contains("config C:/cfg.json", lines[1]);
        Assert.Contains("document Model A", lines[1]);
        Assert.Contains("Revit 2026.5", lines[1]);
        Assert.Contains("2026-10-14", lines[1]);
        Assert.StartsWith("Status,", lines[2]);
    }

    [Fact]
    public void Empty_context_says_not_recorded() =>
        Assert.Contains("not recorded", new ReportContext().Describe());

    [Fact]
    public void Level_and_mark_are_found_for_geometric_elements()
    {
        var model = new BuildingModel([], null, [new GeoElementDto("g1", "H", "Ducts", "L7", "DUCT-9", null, GeometryStatus.Missing)]);
        var f = new Finding("C", "N/A", Severity.Info, FindingStatus.CannotEvaluate, "g1", "m");
        var row = Lines(FindingCsvExporter.Export([f], model))[2];
        Assert.Contains("\"L7\"", row);
        Assert.Contains("\"DUCT-9\"", row);
    }
}
