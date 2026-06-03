namespace WorkflowAutomation.Api.Reporting;

public class GenerateReportJob(IReportGenerator generator)
{
    public Task RunScheduledAsync(Guid scheduleId) => generator.GenerateAndDeliverAsync(scheduleId, CancellationToken.None);
}