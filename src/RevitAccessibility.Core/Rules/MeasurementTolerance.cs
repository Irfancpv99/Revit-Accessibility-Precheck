namespace RevitAccessibility.Core.Rules;

/// <summary>
/// Revit stores lengths in feet, so a value typed as 800 mm can come back as 799.9999999 mm.
/// Comparisons allow 0.0005 mm of slack so a value exactly at the threshold is not failed by floating-point noise.
/// One real unit below the threshold (1 mm) is still a Fail.
/// </summary>
public static class MeasurementTolerance
{
    public const double Mm = 0.0005;

    public static bool AtLeast(double value, double threshold) => value + Mm >= threshold;

    public static bool AtMost(double value, double threshold) => value - Mm <= threshold;
}
