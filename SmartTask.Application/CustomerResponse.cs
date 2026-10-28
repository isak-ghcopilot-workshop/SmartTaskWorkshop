namespace SmartTask.Application;

/// <summary>Represents a customer's contact details returned by the API.</summary>
public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    DateTimeOffset CreatedAt);
