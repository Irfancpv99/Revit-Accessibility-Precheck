namespace RevitAccessibility.Core.Model;

/// <summary>
/// What a measured width actually is. A door family's nominal "Width" is NOT the legal clear opening (luce netta),
/// which is smaller once frame and leaf are taken off. Only a parameter the owner has declared to be the clear
/// opening may be judged against the legal threshold.
/// </summary>
public enum MeasurementMeaning
{
    /// <summary>Not declared. Treated like a proxy: the result needs review.</summary>
    Unknown,
    /// <summary>The owner has declared this parameter holds the clear opening.</summary>
    ClearOpening,
    /// <summary>A nominal or approximate value (for example the type's Width). Indicative only.</summary>
    NominalProxy,
}

/// <summary>Where a measured value came from. Reported with every finding that uses it.</summary>
/// <param name="ParameterName">The parameter that was read.</param>
/// <param name="Origin">"instance", "type" or "computed".</param>
/// <param name="Method">How it was read, for example "Revit length parameter converted to mm once".</param>
/// <param name="Meaning">What the value represents.</param>
public sealed record MeasurementInfo(string ParameterName, string Origin, string Method, MeasurementMeaning Meaning)
{
    public string Describe() => $"{Origin} parameter '{ParameterName}' ({Meaning}); {Method}";
}
