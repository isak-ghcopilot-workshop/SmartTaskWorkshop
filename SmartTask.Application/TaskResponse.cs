namespace SmartTask.Application;

/// <summary>Represents a task returned by the API.</summary>
public sealed record TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTimeOffset CreatedAt);
