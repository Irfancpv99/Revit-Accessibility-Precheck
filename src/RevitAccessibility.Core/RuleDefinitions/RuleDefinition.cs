using RevitAccessibility.Core.Model;

namespace RevitAccessibility.Core.RuleDefinitions;

public enum RuleDomain { Accessibility, Architectural, Structural, Fire, Energy, Hygiene, Coordination }

public enum RuleType { Parameter, Dimension, Geometry, Calculation, ManualReview }

/// <summary>Where the requirement comes from. A project or office requirement is not legislation.</summary>
public enum SourceType { Legislation, TechnicalStandard, ProjectRequirement, OfficeStandard, TestOnly }

/// <summary>Software and review lifecycle of a rule version. Separate from legal verification and from finding status.</summary>
public enum LifecycleStatus { Draft, TechnicallyValidated, LegalReview, Approved, Deprecated }

/// <summary>Legal verification of the source and value. A rule can be technically valid and still Unverified.</summary>
public enum VerificationState { Unverified, NeedsReview, Verified }

/// <summary>Rule only applies to elements whose classification has this value (for example access-door = yes).</summary>
public sealed record ClassificationCondition(string? Name, string? Value);

public sealed record RuleApplicability(
    string? Jurisdiction = null,
    string? BuildingType = null,
    string? InterventionType = null,
    IReadOnlyList<string>? Assumptions = null,
    ClassificationCondition? RequiresClassification = null);

/// <param name="Method">Allowlisted measurement method, e.g. "length-parameter" or "door-clear-width".</param>
/// <param name="Parameter">Revit parameter name, when the method reads one.</param>
public sealed record RuleMeasurement(
    string? Id,
    string? Method,
    string? Parameter = null,
    string? Unit = null,
    MeasurementMeaning? RequiredMeaning = null);

/// <param name="Operator">">=" or "<=" are executable today; other known operators validate but cannot run.</param>
/// <param name="ThresholdUnit">"mm" or "cm" (canonical internal unit is mm).</param>
/// <param name="ToleranceMm">Per-rule tolerance is NOT supported; only the global 0.0005 mm float tolerance applies.</param>
public sealed record RuleEvaluation(
    string? Operator,
    double? Threshold,
    string? ThresholdUnit,
    double? ToleranceMm = null);

public sealed record RuleSourceRef(
    string? Document,
    string? Edition = null,
    string? Clause = null,
    string? Page = null);

public sealed record RuleVerification(
    VerificationState Status = VerificationState.Unverified,
    string? ArchitectReviewer = null,
    string? ProfessorReviewer = null,
    string? ReviewDate = null,
    string? Notes = null);

public sealed record RuleLifecycle(
    LifecycleStatus Status = LifecycleStatus.Draft,
    IReadOnlyList<string>? TestCaseRefs = null,
    string? SupersededBy = null);

/// <summary>
/// Versioned, declarative rule definition (schema version 1). It is data only: no expressions and no code,
/// so a definition can never execute anything the engine does not explicitly implement.
/// Nested objects are nullable so a malformed file is reported by the validator instead of crashing.
/// </summary>
public sealed record RuleDefinition(
    int SchemaVersion,
    string? RuleId,
    int RuleVersion,
    string? Name,
    RuleDomain Domain,
    RuleType RuleType,
    SourceType SourceType,
    IReadOnlyList<string>? ElementCategories,
    RuleApplicability? Applicability,
    RuleMeasurement? Measurement,
    RuleEvaluation? Evaluation,
    RuleSourceRef? Source,
    RuleVerification? Verification,
    RuleLifecycle? Lifecycle,
    Severity Severity = Severity.Error,
    string? Description = null)
{
    public const int CurrentSchemaVersion = 1;
}
