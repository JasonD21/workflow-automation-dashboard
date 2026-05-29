namespace WorkflowAutomation.Api.Common;

public static class SequentialGuid
{
    public static Guid New() => Guid.CreateVersion7();
}