namespace RevitAccessibility.Core.Model;

public enum Severity { Info, Warning, Error }

public enum FindingStatus { Pass, Fail, CannotEvaluate }

public sealed record Finding(
    string RuleId,
    string ClauseRef,
    Severity Severity,
    FindingStatus Status,
    string ElementUid,
    string Message);
