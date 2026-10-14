namespace RevitAccessibility.Core.Model;

public enum Severity { Info, Warning, Error }

/// <summary>
/// Outcome of one check on one element.
/// NeedsReview is appended (not inserted) so existing numeric values do not change. It means "a person must look":
/// a potential clash, or a measurement whose meaning cannot be trusted as the legal quantity.
/// It is never counted as a Pass.
/// </summary>
public enum FindingStatus { Pass, Fail, CannotEvaluate, NeedsReview }

public enum CheckType { Regulatory, Geometric }

/// <summary>
/// Traceability data for a finding. Every field is optional so a rule only states what it really knows.
/// </summary>
/// <param name="RuleVersion">Version of the rule logic, so a result can be tied to the code that produced it.</param>
/// <param name="CheckType">Regulatory (cites a legal clause) or Geometric (coordination, no legal claim).</param>
/// <param name="SourceDocument">Title of the Revit document that contains the element.</param>
/// <param name="LinkedDocument">Identity of the link the element came from; null for the host model.</param>
/// <param name="MeasuredValue">The value that was compared, before any display rounding.</param>
/// <param name="Unit">Unit of <paramref name="MeasuredValue"/>, normally "mm".</param>
/// <param name="MeasurementSource">Which parameter or method produced the value, and what it means.</param>
/// <param name="ExpectedCondition">The requirement in words, for example "at least 800 mm".</param>
/// <param name="VerificationStatus">Register status of the legal value (for example "Needs review"); "n/a" for geometric checks.</param>
/// <param name="MissingInputs">What was missing or invalid when the result is Cannot evaluate or Needs review.</param>
/// <param name="RelatedElement">For pair checks (clashes): identity (DocumentId:UniqueId) of the second element.</param>
public sealed record FindingDetail(
    string RuleVersion,
    CheckType CheckType,
    string? SourceDocument = null,
    string? LinkedDocument = null,
    double? MeasuredValue = null,
    string? Unit = null,
    string? MeasurementSource = null,
    string? ExpectedCondition = null,
    string? VerificationStatus = null,
    IReadOnlyList<string>? MissingInputs = null,
    string? RelatedElement = null);

public sealed record Finding(
    string RuleId,
    string ClauseRef,
    Severity Severity,
    FindingStatus Status,
    string ElementUid,
    string Message,
    FindingDetail? Detail = null);
