using RevitAccessibility.Core.Geometry;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class LinkResolverTests
{
    private static DocumentDto Host(string id = "H") => new(id, "Host", null, LinkStatus.Host, null);

    private static DocumentDto Link(string id, string parent, RigidTransform? t, LinkStatus status = LinkStatus.Loaded) =>
        new(id, "Link " + id, parent, status, t);

    private static RigidTransform Move(double x, double y, double z) => RigidTransform.FromTranslation(new Vec3(x, y, z));

    private static void AssertNear(Vec3 e, Vec3 a)
    {
        Assert.InRange(a.X, e.X - 1e-9, e.X + 1e-9);
        Assert.InRange(a.Y, e.Y - 1e-9, e.Y + 1e-9);
        Assert.InRange(a.Z, e.Z - 1e-9, e.Z + 1e-9);
    }

    [Fact]
    public void Host_alone_resolves_to_identity()
    {
        var r = LinkResolver.Resolve([Host()]);
        Assert.True(r["H"].IsUsable);
        AssertNear(new Vec3(1, 2, 3), r["H"].ToHost!.Apply(new Vec3(1, 2, 3)));
    }

    [Fact]
    public void Direct_link_uses_its_own_transform()
    {
        var r = LinkResolver.Resolve([Host(), Link("A", "H", Move(1000, 0, 0))]);
        AssertNear(new Vec3(1000, 0, 0), r["A"].ToHost!.Apply(new Vec3(0, 0, 0)));
    }

    [Fact]
    public void Nested_links_compose_in_the_right_order()
    {
        // B sits inside A (moved +10 in x). A sits inside Host, rotated 90 degrees about Z.
        // Point (1,0,0) in B: -> in A (11,0,0) -> in Host (0,11,0).
        var r = LinkResolver.Resolve(
        [
            Host(),
            Link("A", "H", RigidTransform.RotationZ(90, new Vec3(0, 0, 0))),
            Link("B", "A", Move(10, 0, 0)),
        ]);
        Assert.True(r["B"].IsUsable);
        AssertNear(new Vec3(0, 11, 0), r["B"].ToHost!.Apply(new Vec3(1, 0, 0)));
    }

    [Fact]
    public void Three_level_chain_resolves()
    {
        var r = LinkResolver.Resolve([Host(), Link("A", "H", Move(1, 0, 0)), Link("B", "A", Move(10, 0, 0)), Link("C", "B", Move(100, 0, 0))]);
        AssertNear(new Vec3(111, 0, 0), r["C"].ToHost!.Apply(new Vec3(0, 0, 0)));
    }

    [Theory]
    [InlineData(LinkStatus.Unloaded)]
    [InlineData(LinkStatus.Missing)]
    [InlineData(LinkStatus.Unsupported)]
    public void Link_that_is_not_loaded_is_reported_not_guessed(LinkStatus status)
    {
        var r = LinkResolver.Resolve([Host(), Link("A", "H", Move(1, 0, 0), status)]);
        Assert.False(r["A"].IsUsable);
        Assert.Contains(status.ToString().ToLowerInvariant(), r["A"].Problem);
    }

    [Fact]
    public void Link_below_an_unloaded_ancestor_is_unusable()
    {
        var r = LinkResolver.Resolve([Host(), Link("A", "H", Move(1, 0, 0), LinkStatus.Unloaded), Link("B", "A", Move(2, 0, 0))]);
        Assert.False(r["B"].IsUsable);
        Assert.Contains("ancestor", r["B"].Problem);
    }

    [Fact]
    public void Link_without_a_transform_is_unusable() =>
        Assert.False(LinkResolver.Resolve([Host(), Link("A", "H", null)])["A"].IsUsable);

    [Fact]
    public void Link_with_unknown_parent_is_unusable()
    {
        var r = LinkResolver.Resolve([Host(), Link("A", "nope", Move(1, 0, 0))]);
        Assert.False(r["A"].IsUsable);
        Assert.Contains("not found", r["A"].Problem);
    }

    [Fact]
    public void Cyclic_links_are_detected()
    {
        var r = LinkResolver.Resolve([Host(), Link("A", "B", Move(1, 0, 0)), Link("B", "A", Move(1, 0, 0))]);
        Assert.False(r["A"].IsUsable);
        Assert.False(r["B"].IsUsable);
        Assert.Contains("cycle", r["A"].Problem);
    }

    [Fact]
    public void Duplicate_document_ids_are_reported_for_that_id_only()
    {
        var r = LinkResolver.Resolve([Host(), Link("A", "H", Move(1, 0, 0)), Link("A", "H", Move(2, 0, 0)), Link("B", "H", Move(3, 0, 0))]);
        Assert.False(r["A"].IsUsable);
        Assert.Contains("more than once", r["A"].Problem);
        Assert.True(r["B"].IsUsable);
    }

    [Fact]
    public void Two_instances_of_the_same_file_are_distinct_documents()
    {
        var r = LinkResolver.Resolve([Host(), Link("inst1", "H", Move(0, 0, 0)), Link("inst2", "H", Move(5000, 0, 0))]);
        AssertNear(new Vec3(5000, 0, 0), r["inst2"].ToHost!.Apply(new Vec3(0, 0, 0)));
        AssertNear(new Vec3(0, 0, 0), r["inst1"].ToHost!.Apply(new Vec3(0, 0, 0)));
    }

    [Fact]
    public void Missing_host_makes_everything_unusable()
    {
        var r = LinkResolver.Resolve([Link("A", "H", Move(1, 0, 0))]);
        Assert.False(r["A"].IsUsable);
        Assert.Contains("No host", r["A"].Problem);
    }

    [Fact]
    public void More_than_one_host_makes_everything_unusable()
    {
        var r = LinkResolver.Resolve([Host("H1"), Host("H2")]);
        Assert.False(r["H1"].IsUsable);
        Assert.Contains("More than one host", r["H1"].Problem);
    }

    [Fact]
    public void Very_deep_chain_is_refused_rather_than_looping()
    {
        var docs = new List<DocumentDto> { Host() };
        string parent = "H";
        for (int i = 0; i < 40; i++) { docs.Add(Link("L" + i, parent, Move(1, 0, 0))); parent = "L" + i; }
        var r = LinkResolver.Resolve(docs);
        Assert.True(r["L5"].IsUsable);
        Assert.False(r["L39"].IsUsable);
    }

    [Fact]
    public void Empty_input_gives_empty_result() => Assert.Empty(LinkResolver.Resolve([]));
}
