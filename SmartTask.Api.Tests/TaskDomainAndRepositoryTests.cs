using SmartTask.Domain;
using SmartTask.Infrastructure;

namespace SmartTask.Api.Tests;

public sealed class TaskDomainAndRepositoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Domain_rejects_blank_titles(string? title)
    {
        Assert.ThrowsAny<ArgumentException>(() => TaskItem.Create(title!, null));
    }

    [Fact]
    public void Domain_enforces_limits_and_updates_without_mutating_the_original()
    {
        Assert.Throws<ArgumentException>(() => TaskItem.Create(new string('t', 201), null));
        Assert.Throws<ArgumentException>(() => TaskItem.Create("Task", new string('d', 2001)));
        var task = TaskItem.Create(new string('t', 200), new string('d', 2000));
        var updated = task.Update(" Changed ", null, true);
        Assert.Equal(task.Id, updated.Id);
        Assert.Equal(task.CreatedAt, updated.CreatedAt);
        Assert.Equal("Changed", updated.Title);
        Assert.True(updated.IsCompleted);
        Assert.False(task.IsCompleted);
        Assert.Equal(200, task.Title.Length);
    }

    [Fact]
    public async Task Updates_cannot_resurrect_a_deleted_task()
    {
        var repository = new InMemoryTaskRepository();
        var task = TaskItem.Create("Task", null);
        await repository.AddAsync(task, CancellationToken.None);
        Assert.True(await repository.DeleteAsync(task.Id, CancellationToken.None));
        Assert.False(await repository.UpdateAsync(task.Update("Changed", null, true), CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(task.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_creates_do_not_lose_tasks()
    {
        var repository = new InMemoryTaskRepository();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(index =>
            Task.Run(() => repository.AddAsync(TaskItem.Create($"Task {index}", null), CancellationToken.None))));
        var tasks = await repository.GetAllAsync(CancellationToken.None);
        Assert.Equal(100, tasks.Count);
        Assert.Equal(100, tasks.Select(task => task.Id).Distinct().Count());
        Assert.Equal(tasks.OrderBy(task => task.CreatedAt).ThenBy(task => task.Id), tasks);
    }

    [Fact]
    public async Task Cancelled_operations_do_not_write_data()
    {
        var repository = new InMemoryTaskRepository();
        var task = TaskItem.Create("Task", null);
        await repository.AddAsync(task, CancellationToken.None);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAllAsync(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetByIdAsync(task.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AddAsync(TaskItem.Create("Other", null), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.UpdateAsync(task.Update("Changed", null, true), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DeleteAsync(task.Id, source.Token));
        Assert.Same(task, Assert.Single(await repository.GetAllAsync(CancellationToken.None)));
    }
}
