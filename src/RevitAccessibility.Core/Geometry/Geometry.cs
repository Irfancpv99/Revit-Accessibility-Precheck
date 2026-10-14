namespace RevitAccessibility.Core.Geometry;

/// <summary>A point or vector in millimetres. Core never stores Revit internal units (feet).</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
}

/// <summary>
/// Axis-aligned box in millimetres, in the coordinate system of the HOST model.
/// A box is a broad-phase filter only: two overlapping boxes do not prove that the solids intersect.
/// </summary>
public sealed record Aabb
{
    private Aabb(Vec3 min, Vec3 max) { Min = min; Max = max; }

    public Vec3 Min { get; }
    public Vec3 Max { get; }

    public static bool TryCreate(Vec3 min, Vec3 max, out Aabb? box, out string? error)
    {
        box = null;
        if (!min.IsFinite || !max.IsFinite) { error = "Bounding box has a non-finite coordinate."; return false; }
        if (min.X > max.X || min.Y > max.Y || min.Z > max.Z) { error = "Bounding box minimum exceeds its maximum."; return false; }
        box = new Aabb(min, max);
        error = null;
        return true;
    }

    public static Aabb Create(Vec3 min, Vec3 max) =>
        TryCreate(min, max, out var box, out var error) ? box! : throw new ArgumentException(error);

    /// <summary>
    /// Overlap length on each axis in mm. Positive means the boxes overlap on that axis, zero means they touch,
    /// negative is the gap between them.
    /// </summary>
    public Vec3 OverlapExtents(Aabb other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Vec3(
            Math.Min(Max.X, other.Max.X) - Math.Max(Min.X, other.Min.X),
            Math.Min(Max.Y, other.Max.Y) - Math.Max(Min.Y, other.Min.Y),
            Math.Min(Max.Z, other.Max.Z) - Math.Max(Min.Z, other.Min.Z));
    }

    /// <summary>
    /// True when the boxes overlap by MORE than <paramref name="toleranceMm"/> on every axis.
    /// Boxes that merely touch, or overlap by no more than the tolerance, are not candidates.
    /// </summary>
    public bool Overlaps(Aabb other, double toleranceMm)
    {
        if (!double.IsFinite(toleranceMm) || toleranceMm < 0)
            throw new ArgumentOutOfRangeException(nameof(toleranceMm), "Tolerance must be finite and not negative.");
        var o = OverlapExtents(other);
        return o.X > toleranceMm && o.Y > toleranceMm && o.Z > toleranceMm;
    }
}

/// <summary>
/// A rigid transform (rotation + translation, translation in millimetres). It maps coordinates of one
/// document into the coordinates of another. Scaling, shearing and mirroring are rejected: a rigid
/// transform keeps distances, which is what makes comparing geometry across documents meaningful.
/// </summary>
public sealed class RigidTransform
{
    private const double OrthoTolerance = 1e-6;
    private readonly double[] _r; // row-major 3x3

    private RigidTransform(double[] r, Vec3 t) { _r = r; Translation = t; }

    public Vec3 Translation { get; }

    public static RigidTransform Identity { get; } = new([1, 0, 0, 0, 1, 0, 0, 0, 1], new Vec3(0, 0, 0));

    public static bool TryCreate(IReadOnlyList<double> rotationRowMajor, Vec3 translationMm,
        out RigidTransform? transform, out string? error)
    {
        transform = null;
        if (rotationRowMajor is null || rotationRowMajor.Count != 9) { error = "Rotation must have 9 values (3x3, row-major)."; return false; }
        if (rotationRowMajor.Any(v => !double.IsFinite(v)) || !translationMm.IsFinite) { error = "Transform has a non-finite value."; return false; }

        var r = rotationRowMajor.ToArray();
        // Rows must be unit length and mutually orthogonal, and the determinant must be +1.
        for (int i = 0; i < 3; i++)
            for (int j = i; j < 3; j++)
            {
                double dot = r[i * 3] * r[j * 3] + r[i * 3 + 1] * r[j * 3 + 1] + r[i * 3 + 2] * r[j * 3 + 2];
                double expected = i == j ? 1 : 0;
                if (Math.Abs(dot - expected) > OrthoTolerance) { error = "Rotation is not orthonormal (scaling or shearing is not supported)."; return false; }
            }
        double det = r[0] * (r[4] * r[8] - r[5] * r[7]) - r[1] * (r[3] * r[8] - r[5] * r[6]) + r[2] * (r[3] * r[7] - r[4] * r[6]);
        if (Math.Abs(det - 1) > OrthoTolerance) { error = "Rotation is mirrored (determinant is not +1); mirrored links are not supported."; return false; }

        transform = new RigidTransform(r, translationMm);
        error = null;
        return true;
    }

    public static RigidTransform FromTranslation(Vec3 translationMm) =>
        TryCreate([1, 0, 0, 0, 1, 0, 0, 0, 1], translationMm, out var tr, out var e) ? tr! : throw new ArgumentException(e);

    /// <summary>Rotation about the Z axis (counter-clockwise, degrees) followed by a translation in mm.</summary>
    public static RigidTransform RotationZ(double degrees, Vec3 translationMm)
    {
        double a = degrees * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
        return TryCreate([c, -s, 0, s, c, 0, 0, 0, 1], translationMm, out var tr, out var e) ? tr! : throw new ArgumentException(e);
    }

    public Vec3 Apply(Vec3 p) => new(
        _r[0] * p.X + _r[1] * p.Y + _r[2] * p.Z + Translation.X,
        _r[3] * p.X + _r[4] * p.Y + _r[5] * p.Z + Translation.Y,
        _r[6] * p.X + _r[7] * p.Y + _r[8] * p.Z + Translation.Z);

    /// <summary>Returns the transform that applies <paramref name="inner"/> first and then this one.</summary>
    public RigidTransform Compose(RigidTransform inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        var r = new double[9];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[i * 3 + j] = _r[i * 3] * inner._r[j] + _r[i * 3 + 1] * inner._r[3 + j] + _r[i * 3 + 2] * inner._r[6 + j];
        var t = Apply(inner.Translation);
        return new RigidTransform(r, t);
    }

    public RigidTransform Inverse()
    {
        // Inverse of a rotation is its transpose; translation becomes -R^T t.
        var rt = new[] { _r[0], _r[3], _r[6], _r[1], _r[4], _r[7], _r[2], _r[5], _r[8] };
        var t = Translation;
        var nt = new Vec3(
            -(rt[0] * t.X + rt[1] * t.Y + rt[2] * t.Z),
            -(rt[3] * t.X + rt[4] * t.Y + rt[5] * t.Z),
            -(rt[6] * t.X + rt[7] * t.Y + rt[8] * t.Z));
        return new RigidTransform(rt, nt);
    }

    /// <summary>
    /// Axis-aligned box that contains the transformed box. Under rotation the result is larger than the
    /// original box, so it is conservative: it can add false candidates but never loses a real one.
    /// </summary>
    public Aabb TransformBox(Aabb box)
    {
        ArgumentNullException.ThrowIfNull(box);
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var x in new[] { box.Min.X, box.Max.X })
            foreach (var y in new[] { box.Min.Y, box.Max.Y })
                foreach (var z in new[] { box.Min.Z, box.Max.Z })
                {
                    var p = Apply(new Vec3(x, y, z));
                    minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
                    maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); maxZ = Math.Max(maxZ, p.Z);
                }
        return Aabb.Create(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));
    }
}
