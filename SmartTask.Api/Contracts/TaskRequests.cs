using System.ComponentModel.DataAnnotations;
using SmartTask.Domain;

namespace SmartTask.Api.Contracts;

/// <summary>Creates an incomplete task with a required title and optional description.</summary>
public sealed record CreateTaskRequest
{
    [Required, StringLength(TaskItem.MaxTitleLength)]
    public required string Title { get; init; }

    [StringLength(TaskItem.MaxDescriptionLength)]
    public string? Description { get; init; }
}

/// <summary>Replaces a task's editable fields while preserving its ID and creation time.</summary>
public sealed record UpdateTaskRequest
{
    [Required, StringLength(TaskItem.MaxTitleLength)]
    public required string Title { get; init; }

    [StringLength(TaskItem.MaxDescriptionLength)]
    public string? Description { get; init; }

    public required bool IsCompleted { get; init; }
}
