using Microsoft.AspNetCore.Http.HttpResults;
using SmartTask.Api.Contracts;
using SmartTask.Application;

namespace SmartTask.Api.Endpoints;

public static class TaskEndpoints
{
    public static WebApplication MapTasks(this WebApplication app)
    {
        var group = app.MapGroup("/api/tasks").WithTags("Tasks");

        group.MapGet("/", async (ITaskService service, CancellationToken cancellationToken) =>
            TypedResults.Ok(await service.GetAllAsync(cancellationToken)))
            .WithName("GetTasks")
            .WithSummary("List all tasks")
            .WithDescription("Returns tasks ordered by creation time.");

        group.MapGet("/{id:guid}", async Task<Results<Ok<TaskResponse>, NotFound>> (
            Guid id, ITaskService service, CancellationToken cancellationToken) =>
        {
            var task = await service.GetByIdAsync(id, cancellationToken);
            return task is null ? TypedResults.NotFound() : TypedResults.Ok(task);
        })
            .WithName("GetTaskById")
            .WithSummary("Get a task")
            .WithDescription("Returns a task by ID, or 404 when it does not exist.");

        group.MapPost("/", async (
            CreateTaskRequest request, ITaskService service, ILoggerFactory loggerFactory,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            var task = await service.CreateAsync(request.Title, request.Description, cancellationToken);
            TaskAuditLog.Mutation(loggerFactory.CreateLogger(TaskAuditLog.CategoryName),
                "Created", task.Id, DateTimeOffset.UtcNow, context.TraceIdentifier);
            return TypedResults.Created($"/api/tasks/{task.Id}", task);
        })
            .WithName("CreateTask")
            .WithSummary("Create a task")
            .WithDescription("Creates an incomplete task and returns its resource URL.")
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", async Task<Results<Ok<TaskResponse>, NotFound>> (
            Guid id, UpdateTaskRequest request, ITaskService service, ILoggerFactory loggerFactory,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            var task = await service.UpdateAsync(
                id, request.Title, request.Description, request.IsCompleted, cancellationToken);
            if (task is null)
                return TypedResults.NotFound();

            TaskAuditLog.Mutation(loggerFactory.CreateLogger(TaskAuditLog.CategoryName),
                "Updated", task.Id, DateTimeOffset.UtcNow, context.TraceIdentifier);
            return TypedResults.Ok(task);
        })
            .WithName("UpdateTask")
            .WithSummary("Update a task")
            .WithDescription("Replaces title, description, and completion state without changing the creation time.")
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id, ITaskService service, ILoggerFactory loggerFactory,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!await service.DeleteAsync(id, cancellationToken))
                return TypedResults.NotFound();

            TaskAuditLog.Mutation(loggerFactory.CreateLogger(TaskAuditLog.CategoryName),
                "Deleted", id, DateTimeOffset.UtcNow, context.TraceIdentifier);
            return TypedResults.NoContent();
        })
            .WithName("DeleteTask")
            .WithSummary("Delete a task")
            .WithDescription("Removes a task, or returns 404 when it does not exist.");

        return app;
    }
}
