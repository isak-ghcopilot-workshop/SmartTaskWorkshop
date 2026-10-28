namespace SmartTask.Domain;

public sealed class TaskItem
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;

    public Guid Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public bool IsCompleted { get; }
    public DateTimeOffset CreatedAt { get; }

    private TaskItem(Guid id, string title, string? description, bool isCompleted, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > MaxTitleLength)
            throw new ArgumentException($"Title cannot exceed {MaxTitleLength} characters.", nameof(title));
        if (description?.Length > MaxDescriptionLength)
            throw new ArgumentException($"Description cannot exceed {MaxDescriptionLength} characters.", nameof(description));

        Id = id;
        Title = title.Trim();
        Description = description;
        IsCompleted = isCompleted;
        CreatedAt = createdAt;
    }

    public static TaskItem Create(string title, string? description) =>
        new(Guid.NewGuid(), title, description, false, DateTimeOffset.UtcNow);

    public TaskItem Update(string title, string? description, bool isCompleted) =>
        new(Id, title, description, isCompleted, CreatedAt);
}
