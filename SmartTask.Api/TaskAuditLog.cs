namespace SmartTask.Api;

internal static partial class TaskAuditLog
{
    internal const string CategoryName = "SmartTask.Audit";

    [LoggerMessage(
        EventId = 1000,
        EventName = "TaskMutation",
        Level = LogLevel.Information,
        Message = "Task audit: {Operation} task {TaskId} at {OccurredAtUtc}; trace {TraceId}")]
    internal static partial void Mutation(
        ILogger logger, string operation, Guid taskId, DateTimeOffset occurredAtUtc, string traceId);
}
