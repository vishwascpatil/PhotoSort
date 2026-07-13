using PhotoSort.TestHarness.Diagnostics;

namespace PhotoSort.TestHarness.Tests;

public enum TestStatus
{
    Pending,
    Running,
    Passed,
    Failed,
    Warning,
    Skipped
}

public sealed class TestResult
{
    public string PhaseName { get; init; } = "";
    public string TestName { get; init; } = "";
    public TestStatus Status { get; set; } = TestStatus.Pending;
    public string Message { get; set; } = "";
    public double DurationMs { get; set; }
    public long MemoryDeltaBytes { get; set; }
    public int ItemsProcessed { get; set; }
    public double ItemsPerSecond { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Exception? Exception { get; set; }

    public void Pass(string message = "")
    {
        Status = TestStatus.Passed;
        Message = message;
    }

    public void Fail(string message, Exception? ex = null)
    {
        Status = TestStatus.Failed;
        Message = message;
        Exception = ex;
    }

    public void Warn(string message)
    {
        Status = TestStatus.Warning;
        Warnings.Add(message);
        if (string.IsNullOrEmpty(Message))
            Message = message;
    }
}

public sealed class TestSuite
{
    public string PhaseName { get; init; } = "";
    public List<TestResult> Results { get; set; } = new();
    public MemoryReport? MemoryReport { get; set; }

    public int Passed => Results.Count(r => r.Status == TestStatus.Passed);
    public int Failed => Results.Count(r => r.Status == TestStatus.Failed);
    public int Warnings => Results.Count(r => r.Status == TestStatus.Warning);
}
