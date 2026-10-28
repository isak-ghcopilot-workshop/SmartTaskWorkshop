using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartTask.Application;

namespace SmartTask.Api.Tests;

public sealed class TaskApiTests
{
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [Fact]
    public async Task Crud_preserves_identity_and_creation_time_and_shares_data_across_requests()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        Assert.Empty((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks"))!);

        var before = DateTimeOffset.UtcNow;
        using var created = await client.PostAsJsonAsync("/api/tasks", new { title = " First task ", description = "Details" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<TaskResponse>())!;
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal("First task", task.Title);
        Assert.Equal("Details", task.Description);
        Assert.False(task.IsCompleted);
        Assert.InRange(task.CreatedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal($"/api/tasks/{task.Id}", created.Headers.Location?.OriginalString);

        using var secondClient = CreateClient(factory);
        Assert.Equal(task, await secondClient.GetFromJsonAsync<TaskResponse>(created.Headers.Location));
        var tasks = await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks");
        Assert.Equal(task, Assert.Single(tasks!));

        using var updated = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", new { title = "Updated", isCompleted = true });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var replacement = (await updated.Content.ReadFromJsonAsync<TaskResponse>())!;
        Assert.Equal(task.Id, replacement.Id);
        Assert.Equal(task.CreatedAt, replacement.CreatedAt);
        Assert.Equal("Updated", replacement.Title);
        Assert.Null(replacement.Description);
        Assert.True(replacement.IsCompleted);
        Assert.Equal(replacement, await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}"));

        using var deleted = await client.DeleteAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("", await deleted.Content.ReadAsStringAsync());
        using var missing = await client.GetAsync($"/api/tasks/{task.Id}");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound);
        Assert.Empty((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks"))!);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Missing_tasks_return_problem_details(string method)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/tasks/{Guid.NewGuid()}");
        if (method == "PUT")
            request.Content = JsonContent.Create(new { title = "Missing", isCompleted = false });

        using var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("POST", "{}")]
    [InlineData("POST", "{\"title\":null}")]
    [InlineData("POST", "{\"title\":\"\"}")]
    [InlineData("POST", "{\"title\":\"   \"}")]
    [InlineData("POST", "{\"title\":123}")]
    [InlineData("POST", "{")]
    [InlineData("PUT", "{\"title\":\"Missing completion\"}")]
    [InlineData("PUT", "{\"title\":\" \",\"isCompleted\":false}")]
    public async Task Invalid_payloads_are_rejected_without_writing_data(string method, string body)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        var url = method == "POST" ? "/api/tasks" : $"/api/tasks/{Guid.NewGuid()}";
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks"))!);
    }

    [Theory]
    [InlineData("POST", 200, 2000, HttpStatusCode.Created)]
    [InlineData("POST", 201, 2000, HttpStatusCode.BadRequest)]
    [InlineData("POST", 200, 2001, HttpStatusCode.BadRequest)]
    [InlineData("PUT", 200, 2000, HttpStatusCode.OK)]
    [InlineData("PUT", 201, 2000, HttpStatusCode.BadRequest)]
    [InlineData("PUT", 200, 2001, HttpStatusCode.BadRequest)]
    public async Task Length_limits_are_enforced_at_the_exact_boundary(
        string method, int titleLength, int descriptionLength, HttpStatusCode expected)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var initial = await client.PostAsJsonAsync("/api/tasks", new { title = "Original" });
        var original = (await initial.Content.ReadFromJsonAsync<TaskResponse>())!;
        var url = method == "POST" ? "/api/tasks" : $"/api/tasks/{original.Id}";
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new
            {
                title = new string('t', titleLength),
                description = new string('d', descriptionLength),
                isCompleted = true
            })
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            await AssertProblemAsync(response, expected);
            Assert.Equal(original, await client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{original.Id}"));
            Assert.Single((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks"))!);
        }
    }

    [Fact]
    public async Task New_application_starts_with_an_empty_store()
    {
        using (var factory = CreateFactory())
        using (var client = CreateClient(factory))
        {
            using var response = await client.PostAsJsonAsync("/api/tasks", new { title = "Temporary" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        using var restartedFactory = CreateFactory();
        using var restartedClient = CreateClient(restartedFactory);
        Assert.Empty((await restartedClient.GetFromJsonAsync<TaskResponse[]>("/api/tasks"))!);
    }

    [Fact]
    public async Task OpenApi_documents_all_task_operations()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var collection = paths.EnumerateObject().Single(path => path.Name.TrimEnd('/') == "/api/tasks").Value;
        var resource = paths.GetProperty("/api/tasks/{id}");
        Assert.Equal("GetTasks", collection.GetProperty("get").GetProperty("operationId").GetString());
        Assert.True(collection.GetProperty("post").GetProperty("responses").TryGetProperty("201", out _));
        Assert.True(collection.GetProperty("post").GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(resource.GetProperty("get").GetProperty("responses").TryGetProperty("404", out _));
        Assert.True(resource.GetProperty("put").GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(resource.GetProperty("delete").GetProperty("responses").TryGetProperty("204", out _));
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
    }
}
