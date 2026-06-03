namespace WorkflowAutomation.Api.Reporting;

public interface IReportGenerator
{
    Task<Guid?> GenerateAndDeliverAsync(Guid scheduleId, CancellationToken ct);  // returns GeneratedReport id
}

// Temporary stub — replaced with the real generator in step 3.
public class ReportGenerator : IReportGenerator
{
    public Task<Guid?> GenerateAndDeliverAsync(Guid scheduleId, CancellationToken ct) => Task.FromResult<Guid?>(null);
}