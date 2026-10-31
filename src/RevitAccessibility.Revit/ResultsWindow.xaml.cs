using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Reporting;

namespace RevitAccessibility.Revit;

public partial class ResultsWindow : Window
{
    private const string AllLabel = "(all)";
    private readonly BuildingModel _model;
    private readonly IReadOnlyList<Finding> _findings;
    private readonly List<ResultRow> _rows;
    private readonly SelectElementHandler _handler;
    private readonly ExternalEvent _event;
    private readonly bool _ready;

    public ResultsWindow(BuildingModel model, IReadOnlyList<Finding> findings, string? unverifiedNote,
        string parameterInfo, IReadOnlyList<string> notRun, SelectElementHandler handler, ExternalEvent externalEvent)
    {
        InitializeComponent();
        _model = model;
        _findings = findings;
        _handler = handler;
        _event = externalEvent;

        var info = new Dictionary<string, (string Level, string Mark)>(StringComparer.Ordinal);
        foreach (var d in model.Doors) info.TryAdd(d.Uid, (d.Level, d.Mark ?? ""));
        foreach (var el in model.AllElements) info.TryAdd(el.Uid, (el.Level, el.Mark ?? ""));
        _rows = findings.Select(f =>
        {
            var (level, mark) = info.GetValueOrDefault(f.ElementUid);
            return new ResultRow(f, level ?? "", mark ?? "");
        }).ToList();

        Footer.Text = Disclaimer.Text; // mandatory, always visible
        ParameterInfo.Text = parameterInfo;
        if (notRun.Count > 0)
        {
            NotRunText.Text = "Rules not run: " + string.Join(" | ", notRun);
            NotRunText.Visibility = Visibility.Visible;
        }
        if (unverifiedNote is not null)
        {
            UnverifiedText.Text = unverifiedNote + " Do not rely on these results.";
            UnverifiedBanner.Visibility = Visibility.Visible;
        }

        LevelFilter.ItemsSource = new[] { AllLabel }.Concat(_rows.Select(r => r.Level).Distinct().OrderBy(x => x)).ToList();
        LevelFilter.SelectedIndex = 0;
        StatusFilter.ItemsSource = new[] { AllLabel, "Fail", "Needs review", "Cannot evaluate", "Pass" };
        StatusFilter.SelectedIndex = 0;

        _ready = true;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (!_ready) return;
        string level = LevelFilter.SelectedItem as string ?? AllLabel;
        string status = StatusFilter.SelectedItem as string ?? AllLabel;
        string search = SearchBox.Text.Trim();
        bool showPassed = ShowPassed.IsChecked == true;

        var shown = _rows.Where(r =>
            (showPassed || r.Finding.Status != FindingStatus.Pass || status == "Pass") &&
            (level == AllLabel || r.Level == level) &&
            (status == AllLabel || r.Status == status) &&
            (search.Length == 0 ||
                r.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Mark.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();

        Grid.ItemsSource = shown;
        int fail = _rows.Count(r => r.Finding.Status == FindingStatus.Fail);
        int review = _rows.Count(r => r.Finding.Status == FindingStatus.NeedsReview);
        int cannot = _rows.Count(r => r.Finding.Status == FindingStatus.CannotEvaluate);
        int pass = _rows.Count(r => r.Finding.Status == FindingStatus.Pass);
        SummaryText.Text = $"{_rows.Count} checks: ✖ {fail} Fail, ! {review} Needs review, ? {cannot} Cannot evaluate, ✔ {pass} Pass. Showing {shown.Count}.";
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilters();

    private void OnRowSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not ResultRow r) { Detail.Text = ""; return; }
        Detail.Text = new StringBuilder()
            .AppendLine($"Status: {r.StatusDisplay}   Severity: {r.Severity}")
            .AppendLine($"Rule: {r.Finding.RuleId}")
            .AppendLine($"Clause: {r.Clause}")
            .AppendLine($"Level: {r.Level}   Mark: {r.Mark}")
            .AppendLine(r.Message).ToString();
    }

    private void OnSelectInModel(object sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not ResultRow r) return;
        _handler.PendingUid = r.Finding.ElementUid;
        _event.Raise();
    }

    private void OnExportCsv(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "accessibility-precheck.csv" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, FindingCsvExporter.Export(_findings, _model), new UTF8Encoding(true));
        }
        catch (Exception ex)
        {
            AddinLog.Write(ex.ToString());
            MessageBox.Show(this, $"Could not save the file: {ex.Message}", "Accessibility Pre-Check");
        }
    }
}
