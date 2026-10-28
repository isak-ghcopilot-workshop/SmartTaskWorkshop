using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartTask.Application;

namespace SmartTask.Api.Tests;

public sealed class TaskAuditTests
{
    [Fact]
    public async Task Successful_mutations_emit_one_structured_audit_event_without_task_content()
    {
        using var provider = new AuditLoggerProvider();
        using var factory = CreateFactory(provider);
        using var client = CreateClient(factory);
        var before = DateTimeOffset.UtcNow;

        using var created = await client.PostAsJsonAsync("/api/tasks",
            new { title = "Private title", description = "Private description" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<TaskResponse>())!;

        using var updated = await client.PutAsJsonAsync($"/api/tasks/{task.Id}",
            new { title = "Changed private title", description = "Changed private description", isCompleted = true });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        using var deleted = await client.DeleteAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var entries = provider.Entries.ToArray();
        Assert.Equal(new[] { "Created", "Updated", "Deleted" },
            entries.Select(entry => Assert.IsType<string>(entry.Fields["Operation"])));
        foreach (var entry in entries)
        {
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(1000, entry.EventId.Id);
            Assert.Equal("TaskMutation", entry.EventId.Name);
            Assert.Equal(task.Id, Assert.IsType<Guid>(entry.Fields["TaskId"]));
            var timestamp = Assert.IsType<DateTimeOffset>(entry.Fields["OccurredAtUtc"]);
            Assert.Equal(TimeSpan.Zero, timestamp.Offset);
            Assert.InRange(timestamp, before, DateTimeOffset.UtcNow);
            Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(entry.Fields["TraceId"])));
            Assert.Equal(
                new[] { "OccurredAtUtc", "Operation", "TaskId", "TraceId", "{OriginalFormat}" },
                entry.Fields.Keys.OrderBy(key => key, StringComparer.Ordinal));
            Assert.DoesNotContain("private", entry.Message, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(3, entries.Select(entry => entry.Fields["TraceId"]).Distinct().Count());
    }

    [Fact]
    public async Task Reads_invalid_payloads_and_missing_tasks_do_not_emit_success_audit_events()
    {
        using var provider = new AuditLoggerProvider();
        using var factory = CreateFactory(provider);
        using var client = CreateClient(factory);
        var missingId = Guid.NewGuid();

        using var list = await client.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var get = await client.GetAsync($"/api/tasks/{missingId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        using var invalidCreate = await client.PostAsJsonAsync("/api/tasks", new { title = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        using var invalidUpdate = await client.PutAsJsonAsync($"/api/tasks/{missingId}",
            new { title = " ", isCompleted = false });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);
        using var update = await client.PutAsJsonAsync($"/api/tasks/{missingId}",
            new { title = "Missing", isCompleted = true });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        using var delete = await client.DeleteAsync($"/api/tasks/{missingId}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        Assert.Empty(provider.Entries);
    }

    [Theory]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("DELETE", false)]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("DELETE", true)]
    public async Task Failed_or_cancelled_service_operations_do_not_emit_success_audit_events(
        string method, bool cancelled)
    {
        using var provider = new AuditLoggerProvider();
        using var factory = CreateFactory(provider).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddScoped<ITaskService>(_ => new FailingTaskService(cancelled))));
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/tasks" : $"/api/tasks/{Guid.NewGuid()}");
        if (method != "DELETE")
            request.Content = JsonContent.Create(new { title = "Task", isCompleted = true });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(provider.Entries);
    }

    private static WebApplicationFactory<Program> CreateFactory(AuditLoggerProvider provider) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging =>
            {
                logging.AddFilter<AuditLoggerProvider>("SmartTask.Audit", LogLevel.Information);
                logging.AddProvider(provider);
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private sealed record AuditEntry(
        LogLevel Level, EventId EventId, IReadOnlyDictionary<string, object?> Fields, string Message);

    private sealed class FailingTaskService(bool cancelled) : ITaskService
    {
        private Exception Failure => cancelled
            ? new OperationCanceledException(new CancellationToken(canceled: true))
            : new InvalidOperationException("Simulated service failure.");

        public Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskResponse> CreateAsync(
            string title, string? description, CancellationToken cancellationToken) =>
            Task.FromException<TaskResponse>(Failure);

        public Task<TaskResponse?> UpdateAsync(
            Guid id, string title, string? description, bool isCompleted, CancellationToken cancellationToken) =>
            Task.FromException<TaskResponse?>(Failure);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromException<bool>(Failure);
    }

    private sealed class AuditLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<AuditEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new AuditLogger(this, categoryName);

        public void Dispose() { }

        private sealed class AuditLogger(AuditLoggerProvider provider, string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => categoryName == "SmartTask.Audit";

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                var fields = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(state);
                provider.Entries.Enqueue(new AuditEntry(
                    logLevel, eventId, fields.ToDictionary(pair => pair.Key, pair => pair.Value),
                    formatter(state, exception)));
            }
        }
    }
}
