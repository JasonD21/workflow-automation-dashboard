namespace WorkflowAutomation.Api.Automations.Triggers;

public class ProcessTriggerJob(ITriggerProcessor processor)
{
    public Task RunAsync(TriggerEvent evt) => processor.ProcessAsync(evt, CancellationToken.None);
}