using RevitAccessibility.Core.Geometry;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class AabbTests
{
    private static Aabb Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
        Aabb.Create(new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));

    [Fact]
    public void Valid_box_is_created()
    {
        Assert.True(Aabb.TryCreate(new Vec3(0, 0, 0), new Vec3(1, 2, 3), out var box, out var error));
        Assert.NotNull(box);
        Assert.Null(error);
    }

    [Fact]
    public void Degenerate_flat_box_is_valid() =>
        Assert.True(Aabb.TryCreate(new Vec3(0, 0, 0), new Vec3(5, 5, 0), out _, out _));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Non_finite_coordinate_is_rejected(double bad)
    {
        Assert.False(Aabb.TryCreate(new Vec3(bad, 0, 0), new Vec3(1, 1, 1), out var box, out var error));
        Assert.Null(box);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Min_greater_than_max_is_rejected() =>
        Assert.False(Aabb.TryCreate(new Vec3(2, 0, 0), new Vec3(1, 1, 1), out _, out _));

    [Fact]
    public void Create_throws_on_invalid_box() =>
        Assert.Throws<ArgumentException>(() => Box(2, 0, 0, 1, 1, 1));

    [Fact]
    public void Overlap_extents_are_positive_when_overlapping()
    {
        var o = Box(0, 0, 0, 10, 10, 10).OverlapExtents(Box(6, 7, 8, 20, 20, 20));
        Assert.Equal(new Vec3(4, 3, 2), o);
    }

    [Fact]
    public void Overlap_extents_are_negative_for_a_gap() =>
        Assert.Equal(-5, Box(0, 0, 0, 10, 10, 10).OverlapExtents(Box(15, 0, 0, 20, 10, 10)).X);

    [Fact]
    public void Overlapping_boxes_overlap() =>
        Assert.True(Box(0, 0, 0, 10, 10, 10).Overlaps(Box(5, 5, 5, 15, 15, 15), 0));

    [Fact]
    public void Touching_boxes_do_not_overlap() =>
        Assert.False(Box(0, 0, 0, 10, 10, 10).Overlaps(Box(10, 0, 0, 20, 10, 10), 0));

    [Fact]
    public void Overlap_no_larger_than_tolerance_is_ignored() =>
        Assert.False(Box(0, 0, 0, 10, 10, 10).Overlaps(Box(9.5, 0, 0, 20, 10, 10), 1.0));

    [Fact]
    public void Overlap_larger_than_tolerance_counts() =>
        Assert.True(Box(0, 0, 0, 10, 10, 10).Overlaps(Box(8, 0, 0, 20, 10, 10), 1.0));

    [Fact]
    public void Overlap_must_exist_on_every_axis() =>
        Assert.False(Box(0, 0, 0, 10, 10, 10).Overlaps(Box(5, 5, 50, 15, 15, 60), 0));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Invalid_tolerance_throws(double tol) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Box(0, 0, 0, 1, 1, 1).Overlaps(Box(0, 0, 0, 1, 1, 1), tol));
}

public class RigidTransformTests
{
    private static void AssertNear(Vec3 expected, Vec3 actual, double eps = 1e-9)
    {
        Assert.InRange(actual.X, expected.X - eps, expected.X + eps);
        Assert.InRange(actual.Y, expected.Y - eps, expected.Y + eps);
        Assert.InRange(actual.Z, expected.Z - eps, expected.Z + eps);
    }

    [Fact]
    public void Identity_leaves_points_unchanged() =>
        AssertNear(new Vec3(1, 2, 3), RigidTransform.Identity.Apply(new Vec3(1, 2, 3)));

    [Fact]
    public void Translation_moves_points() =>
        AssertNear(new Vec3(11, 22, 33), RigidTransform.FromTranslation(new Vec3(10, 20, 30)).Apply(new Vec3(1, 2, 3)));

    [Fact]
    public void Rotation_of_90_degrees_about_Z_maps_X_axis_to_Y_axis() =>
        AssertNear(new Vec3(0, 1, 0), RigidTransform.RotationZ(90, new Vec3(0, 0, 0)).Apply(new Vec3(1, 0, 0)));

    [Fact]
    public void Rotation_is_applied_before_translation() =>
        AssertNear(new Vec3(100, 1, 0), RigidTransform.RotationZ(90, new Vec3(100, 0, 0)).Apply(new Vec3(1, 0, 0)));

    [Fact]
    public void Compose_applies_the_inner_transform_first()
    {
        var rotate = RigidTransform.RotationZ(90, new Vec3(0, 0, 0));
        var move = RigidTransform.FromTranslation(new Vec3(10, 0, 0));

        // inner = move, outer = rotate: (1,0,0) -> (11,0,0) -> (0,11,0)
        AssertNear(new Vec3(0, 11, 0), rotate.Compose(move).Apply(new Vec3(1, 0, 0)));
        // inner = rotate, outer = move: (1,0,0) -> (0,1,0) -> (10,1,0)
        AssertNear(new Vec3(10, 1, 0), move.Compose(rotate).Apply(new Vec3(1, 0, 0)));
    }

    [Fact]
    public void Inverse_undoes_the_transform()
    {
        var t = RigidTransform.RotationZ(37, new Vec3(120, -40, 8));
        var p = new Vec3(5, 6, 7);
        AssertNear(p, t.Inverse().Apply(t.Apply(p)), 1e-9);
    }

    [Fact]
    public void Transform_composed_with_its_inverse_is_identity()
    {
        var t = RigidTransform.RotationZ(123, new Vec3(1, 2, 3));
        AssertNear(new Vec3(4, 5, 6), t.Compose(t.Inverse()).Apply(new Vec3(4, 5, 6)), 1e-9);
    }

    [Fact]
    public void Scaling_is_rejected()
    {
        Assert.False(RigidTransform.TryCreate([2, 0, 0, 0, 2, 0, 0, 0, 2], new Vec3(0, 0, 0), out var t, out var error));
        Assert.Null(t);
        Assert.Contains("orthonormal", error);
    }

    [Fact]
    public void Mirroring_is_rejected()
    {
        Assert.False(RigidTransform.TryCreate([-1, 0, 0, 0, 1, 0, 0, 0, 1], new Vec3(0, 0, 0), out var t, out var error));
        Assert.Null(t);
        Assert.Contains("mirror", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Shear_is_rejected() =>
        Assert.False(RigidTransform.TryCreate([1, 0.5, 0, 0, 1, 0, 0, 0, 1], new Vec3(0, 0, 0), out _, out _));

    [Fact]
    public void Wrong_number_of_values_is_rejected() =>
        Assert.False(RigidTransform.TryCreate([1, 0, 0, 0, 1, 0], new Vec3(0, 0, 0), out _, out _));

    [Fact]
    public void Non_finite_translation_is_rejected() =>
        Assert.False(RigidTransform.TryCreate([1, 0, 0, 0, 1, 0, 0, 0, 1], new Vec3(double.NaN, 0, 0), out _, out _));

    [Fact]
    public void Transforming_a_box_by_90_degrees_swaps_its_extents()
    {
        var box = Aabb.Create(new Vec3(0, 0, 0), new Vec3(10, 2, 3));
        var r = RigidTransform.RotationZ(90, new Vec3(0, 0, 0)).TransformBox(box);
        Assert.Equal(2, r.Max.X - r.Min.X, 9);
        Assert.Equal(10, r.Max.Y - r.Min.Y, 9);
        Assert.Equal(3, r.Max.Z - r.Min.Z, 9);
    }

    [Fact]
    public void Transforming_a_box_by_45_degrees_grows_it_never_shrinks_it()
    {
        var box = Aabb.Create(new Vec3(0, 0, 0), new Vec3(10, 10, 1));
        var r = RigidTransform.RotationZ(45, new Vec3(0, 0, 0)).TransformBox(box);
        Assert.True(r.Max.X - r.Min.X >= 10);
        Assert.True(r.Max.Y - r.Min.Y >= 10);
    }
}
