namespace UnlimitedPDF.Benchmarks.Core;

/// <summary>Classification of every scenario/library/parameter case. Only <see cref="Passed"/> cases are measured or ranked.</summary>
public enum ResultStatus
{
    Passed,
    FailedValidation,
    Unsupported,
    ExecutionFailed,
    TimedOut,
    NotExecuted,
}
