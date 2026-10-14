using RevitAccessibility.Core.Engine;
using RevitAccessibility.Core.Geometry;
using RevitAccessibility.Core.Model;
using RevitAccessibility.Core.Rules;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class ClashRuleTests
{
    private static readonly ClashRuleSpec Spec = new("CLASH-DUCT-BEAM", "1", "Ducts", "Beams", ToleranceMm: 1.0);

    private static Aabb Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
        Aabb.Create(new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));

    private static GeoElementDto El(string uid, string category, Aabb? box, string doc = "H", string? host = null,
        GeometryStatus status = GeometryStatus.Available, string? note = null, string? mark = null) =>
        new(uid, doc, category, "L1", mark, box, status, note, host);

    private static BuildingModel Model(IEnumerable<GeoElementDto> els, IEnumerable<PairConfirmationDto>? conf = null) =>
        new([], null, els.ToList(), conf?.ToList());

    private static readonly Aabb Duct = Box(0, 0, 0, 1000, 200, 200);
    private static readonly Aabb BeamOverlapping = Box(400, 50, 100, 500, 150, 400);
    private static readonly Aabb BeamFar = Box(5000, 0, 0, 5100, 100, 100);

    // ---- Scan ----

    [Fact]
    public void Overlapping_boxes_of_the_two_categories_are_a_candidate()
    {
        var scan = ClashDetector.Scan([El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)], Spec);
        Assert.Single(scan.Candidates);
    }

    [Fact]
    public void Separate_boxes_are_not_a_candidate() =>
        Assert.Empty(ClashDetector.Scan([El("d", "Ducts", Duct), El("b", "Beams", BeamFar)], Spec).Candidates);

    [Fact]
    public void Touching_boxes_are_not_a_candidate() =>
        Assert.Empty(ClashDetector.Scan([El("d", "Ducts", Box(0, 0, 0, 10, 10, 10)), El("b", "Beams", Box(10, 0, 0, 20, 10, 10))], Spec).Candidates);

    [Fact]
    public void Overlap_within_tolerance_is_not_a_candidate() =>
        Assert.Empty(ClashDetector.Scan([El("d", "Ducts", Box(0, 0, 0, 10, 10, 10)), El("b", "Beams", Box(9.5, 0, 0, 20, 10, 10))], Spec).Candidates);

    [Fact]
    public void Elements_outside_the_two_categories_are_ignored() =>
        Assert.Empty(ClashDetector.Scan([El("d", "Ducts", Duct), El("w", "Walls", BeamOverlapping)], Spec).Candidates);

    [Fact]
    public void Category_match_ignores_case_and_spaces() =>
        Assert.Single(ClashDetector.Scan([El("d", "ducts ", Duct), El("b", "BEAMS", BeamOverlapping)], Spec).Candidates);

    [Fact]
    public void Hosted_element_is_an_intentional_connection_not_a_candidate()
    {
        var spec = new ClashRuleSpec("C", "1", "Doors", "Walls");
        var scan = ClashDetector.Scan([El("door", "Doors", Duct, host: "wall"), El("wall", "Walls", BeamOverlapping)], spec);
        Assert.Empty(scan.Candidates);
    }

    [Fact]
    public void Same_uid_in_a_different_document_is_not_treated_as_the_host()
    {
        var spec = new ClashRuleSpec("C", "1", "Doors", "Walls");
        var scan = ClashDetector.Scan([El("door", "Doors", Duct, "H", host: "wall"), El("wall", "Walls", BeamOverlapping, "LINK")], spec);
        Assert.Single(scan.Candidates);
    }

    [Fact]
    public void Same_category_pairs_are_reported_once()
    {
        var spec = new ClashRuleSpec("C", "1", "Ducts", "Ducts");
        var scan = ClashDetector.Scan([El("a", "Ducts", Duct), El("b", "Ducts", BeamOverlapping)], spec);
        Assert.Single(scan.Candidates);
    }

    [Fact]
    public void An_element_is_never_paired_with_itself()
    {
        var spec = new ClashRuleSpec("C", "1", "Ducts", "Ducts");
        Assert.Empty(ClashDetector.Scan([El("a", "Ducts", Duct)], spec).Candidates);
    }

    [Fact]
    public void Same_identity_supplied_twice_is_reported_and_used_once()
    {
        var scan = ClashDetector.Scan([El("d", "Ducts", Duct), El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)], Spec);
        Assert.Single(scan.Candidates);
        Assert.Single(scan.Duplicates);
    }

    [Fact]
    public void Same_uid_in_two_documents_are_two_different_elements()
    {
        var scan = ClashDetector.Scan([El("x", "Ducts", Duct, "H"), El("x", "Ducts", Duct, "LINK"), El("b", "Beams", BeamOverlapping)], Spec);
        Assert.Equal(2, scan.Candidates.Count);
        Assert.Empty(scan.Duplicates);
    }

    [Fact]
    public void Elements_without_usable_geometry_are_listed_not_skipped()
    {
        var scan = ClashDetector.Scan([El("d", "Ducts", null, status: GeometryStatus.Missing), El("b", "Beams", BeamOverlapping)], Spec);
        Assert.Single(scan.Unusable);
        Assert.Empty(scan.Candidates);
    }

    [Fact]
    public void Available_status_without_a_box_is_treated_as_unusable()
    {
        var scan = ClashDetector.Scan([El("d", "Ducts", null, status: GeometryStatus.Available)], Spec);
        Assert.Single(scan.Unusable);
    }

    [Fact]
    public void Pair_key_does_not_depend_on_order() =>
        Assert.Equal(ClashDetector.PairKey("a", "b"), ClashDetector.PairKey("b", "a"));

    [Fact]
    public void Scanning_a_large_model_is_fast_enough()
    {
        var els = new List<GeoElementDto>();
        for (int i = 0; i < 1500; i++)
        {
            els.Add(El("d" + i, "Ducts", Box(i * 100, 0, 0, i * 100 + 90, 100, 100)));
            els.Add(El("b" + i, "Beams", Box(i * 100 + 500000, 0, 0, i * 100 + 500090, 100, 100)));
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var scan = ClashDetector.Scan(els, Spec);
        sw.Stop();
        Assert.Empty(scan.Candidates);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"took {sw.ElapsedMilliseconds} ms");
    }

    // ---- Rule: classification ----

    [Fact]
    public void Candidate_without_confirmation_is_a_potential_clash_for_review_not_a_failure()
    {
        var f = Assert.Single(new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)])));
        Assert.Equal(FindingStatus.NeedsReview, f.Status);
        Assert.Contains("Potential clash", f.Message);
        Assert.Contains("not tested", f.Message);
    }

    [Fact]
    public void Confirmed_intersection_still_needs_review_and_is_not_a_legal_failure()
    {
        var conf = new PairConfirmationDto("H:d", "H:b", PairConfirmation.Intersects, OverlapVolumeMm3: 1234.5);
        var f = Assert.Single(new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)], [conf])));
        Assert.Equal(FindingStatus.NeedsReview, f.Status);
        Assert.NotEqual(FindingStatus.Fail, f.Status);
        Assert.Contains("Confirmed geometric intersection", f.Message);
        Assert.Contains("not a legal finding", f.Message);
        Assert.Equal("mm3", f.Detail!.Unit);
        Assert.Equal(1234.5, f.Detail.MeasuredValue);
    }

    [Fact]
    public void Confirmation_given_in_either_order_is_matched()
    {
        var conf = new PairConfirmationDto("H:b", "H:d", PairConfirmation.NoIntersection);
        Assert.Empty(new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)], [conf])));
    }

    [Fact]
    public void Exact_test_that_rules_the_pair_out_removes_the_finding() =>
        Assert.Empty(new ClashRule(Spec).Evaluate(Model(
            [El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)],
            [new PairConfirmationDto("H:d", "H:b", PairConfirmation.NoIntersection)])));

    [Fact]
    public void Failed_exact_test_still_reports_for_manual_review()
    {
        var conf = new PairConfirmationDto("H:d", "H:b", PairConfirmation.Failed, Note: "invalid solid");
        var f = Assert.Single(new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)], [conf])));
        Assert.Equal(FindingStatus.NeedsReview, f.Status);
        Assert.Contains("invalid solid", f.Message);
    }

    [Fact]
    public void Missing_geometry_is_cannot_evaluate_and_says_it_is_not_a_no_clash_result()
    {
        var f = Assert.Single(new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", null, status: GeometryStatus.Invalid, note: "empty solid")])));
        Assert.Equal(FindingStatus.CannotEvaluate, f.Status);
        Assert.Contains("not a 'no clash' result", f.Message);
        Assert.Contains("empty solid", f.Message);
    }

    [Fact]
    public void Duplicate_identity_is_reported_as_cannot_evaluate()
    {
        var findings = new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", Duct), El("d", "Ducts", Duct)])).ToList();
        var f = Assert.Single(findings);
        Assert.Equal(FindingStatus.CannotEvaluate, f.Status);
        Assert.Contains("more than once", f.Message);
    }

    [Fact]
    public void Empty_model_yields_no_findings() =>
        Assert.Empty(new ClashRule(Spec).Evaluate(new BuildingModel([])));

    [Fact]
    public void A_clash_rule_never_produces_pass_or_fail()
    {
        var els = new[]
        {
            El("d1", "Ducts", Duct), El("b1", "Beams", BeamOverlapping), El("b2", "Beams", BeamFar),
            El("d2", "Ducts", null, status: GeometryStatus.Missing),
        };
        Assert.All(new ClashRule(Spec).Evaluate(Model(els)),
            f => Assert.True(f.Status is FindingStatus.NeedsReview or FindingStatus.CannotEvaluate));
    }

    // ---- Rule: traceability ----

    [Fact]
    public void Finding_carries_geometric_traceability()
    {
        var f = Assert.Single(new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", Duct, mark: "D-1"), El("b", "Beams", BeamOverlapping, mark: "B-9")])));
        var d = f.Detail!;
        Assert.Equal(CheckType.Geometric, d.CheckType);
        Assert.Equal("1", d.RuleVersion);
        Assert.Equal("H", d.SourceDocument);
        Assert.Null(d.LinkedDocument);
        Assert.Equal("H:b", d.RelatedElement);
        Assert.Equal("mm", d.Unit);
        Assert.Contains("broad phase", d.MeasurementSource);
        Assert.Contains("n/a", d.VerificationStatus);
        Assert.Equal("CLASH-DUCT-BEAM", f.RuleId);
        Assert.Contains("N/A", f.ClauseRef);
        Assert.Equal(Severity.Warning, f.Severity);
    }

    [Fact]
    public void Cross_document_pair_names_the_linked_document()
    {
        var f = Assert.Single(new ClashRule(Spec).Evaluate(Model([El("d", "Ducts", Duct, "H"), El("b", "Beams", BeamOverlapping, "STR")])));
        Assert.Equal("STR", f.Detail!.LinkedDocument);
    }

    [Fact]
    public void Invalid_spec_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ClashRule(new ClashRuleSpec("", "1", "A", "B")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClashRule(new ClashRuleSpec("R", "1", "A", "B", -1)));
    }

    // ---- Separation of geometric and regulatory findings ----

    [Fact]
    public void Geometric_and_regulatory_findings_stay_distinguishable_in_one_engine_run()
    {
        var door = new DoorDto("u1", "L1", 700, true, "D1",
            ClearWidthSource: new MeasurementInfo("Clear", "type", "test", MeasurementMeaning.ClearOpening));
        var thresholds = new DoorClearWidthThresholds(800, 750, "c", verificationStatus: "Needs review");
        var model = new BuildingModel([door], null,
            [El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)]);

        var findings = new RuleEngine([new DoorClearWidthRule(thresholds), new ClashRule(Spec)]).Run(model);

        Assert.Equal(2, findings.Count);
        var reg = Assert.Single(findings, f => f.Detail!.CheckType == CheckType.Regulatory);
        var geo = Assert.Single(findings, f => f.Detail!.CheckType == CheckType.Geometric);
        Assert.Equal(FindingStatus.Fail, reg.Status);
        Assert.Equal(FindingStatus.NeedsReview, geo.Status);
        Assert.Equal("Needs review", reg.Detail!.VerificationStatus);
    }

    [Fact]
    public void Engine_does_not_duplicate_findings_when_rules_do_not_overlap()
    {
        var model = Model([El("d", "Ducts", Duct), El("b", "Beams", BeamOverlapping)]);
        var findings = new RuleEngine([new ClashRule(Spec)]).Run(model);
        Assert.Single(findings);
    }
}
