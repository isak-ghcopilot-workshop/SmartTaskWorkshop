using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartTask.Application;

namespace SmartTask.Api.Tests;

public sealed class CustomerApiTests
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
        Assert.Empty((await client.GetFromJsonAsync<CustomerResponse[]>("/api/customers"))!);

        var before = DateTimeOffset.UtcNow;
        using var created = await client.PostAsJsonAsync("/api/customers",
            new { name = " Example Customer ", email = "customer@example.com", phone = " +1 555 0100 " });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Example Customer", customer.Name);
        Assert.Equal("customer@example.com", customer.Email);
        Assert.Equal("+1 555 0100", customer.Phone);
        Assert.InRange(customer.CreatedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal($"/api/customers/{customer.Id}", created.Headers.Location?.OriginalString);

        using var secondClient = CreateClient(factory);
        Assert.Equal(customer, await secondClient.GetFromJsonAsync<CustomerResponse>(created.Headers.Location));
        Assert.Equal(customer, Assert.Single((await client.GetFromJsonAsync<CustomerResponse[]>("/api/customers"))!));

        using var updated = await client.PutAsJsonAsync($"/api/customers/{customer.Id}",
            new { name = "Updated Customer", email = "updated@example.com" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var replacement = (await updated.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal(customer.Id, replacement.Id);
        Assert.Equal(customer.CreatedAt, replacement.CreatedAt);
        Assert.Equal("Updated Customer", replacement.Name);
        Assert.Equal("updated@example.com", replacement.Email);
        Assert.Null(replacement.Phone);
        Assert.Equal(replacement, await client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{customer.Id}"));

        using var deleted = await client.DeleteAsync($"/api/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("", await deleted.Content.ReadAsStringAsync());
        using var missing = await client.GetAsync($"/api/customers/{customer.Id}");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound);
        using var deletedAgain = await client.DeleteAsync($"/api/customers/{customer.Id}");
        await AssertProblemAsync(deletedAgain, HttpStatusCode.NotFound);
        Assert.Empty((await client.GetFromJsonAsync<CustomerResponse[]>("/api/customers"))!);
        Assert.Empty((await client.GetFromJsonAsync<TaskResponse[]>("/api/tasks"))!);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Missing_customers_return_problem_details(string method)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/customers/{Guid.NewGuid()}");
        if (method == "PUT")
            request.Content = JsonContent.Create(new { name = "Missing", email = "missing@example.com" });

        using var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    public static IEnumerable<object[]> InvalidPayloads()
    {
        string[] bodies =
        [
            "{}",
            "{\"email\":\"customer@example.com\"}",
            "{\"name\":\"Customer\"}",
            "{\"name\":null,\"email\":\"customer@example.com\"}",
            "{\"name\":\"\",\"email\":\"customer@example.com\"}",
            "{\"name\":\"   \",\"email\":\"customer@example.com\"}",
            "{\"name\":\"Customer\",\"email\":null}",
            "{\"name\":\"Customer\",\"email\":\"\"}",
            "{\"name\":\"Customer\",\"email\":\"   \"}",
            "{\"name\":\"Customer\",\"email\":\"not-an-email\"}",
            "{\"name\":123,\"email\":\"customer@example.com\"}",
            "{\"name\":\"Customer\",\"email\":\"customer@example.com\",\"phone\":123}",
            "{"
        ];
        foreach (var method in new[] { "POST", "PUT" })
        foreach (var body in bodies)
            yield return [method, body];
    }

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task Invalid_payloads_are_rejected_without_writing_data(string method, string body)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var initial = await client.PostAsJsonAsync("/api/customers",
            new { name = "Original", email = "original@example.com", phone = "123" });
        var original = (await initial.Content.ReadFromJsonAsync<CustomerResponse>())!;
        var url = method == "POST" ? "/api/customers" : $"/api/customers/{original.Id}";
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(original, Assert.Single((await client.GetFromJsonAsync<CustomerResponse[]>("/api/customers"))!));
    }

    [Theory]
    [InlineData("POST", 200, 254, 50, HttpStatusCode.Created)]
    [InlineData("POST", 201, 254, 50, HttpStatusCode.BadRequest)]
    [InlineData("POST", 200, 255, 50, HttpStatusCode.BadRequest)]
    [InlineData("POST", 200, 254, 51, HttpStatusCode.BadRequest)]
    [InlineData("PUT", 200, 254, 50, HttpStatusCode.OK)]
    [InlineData("PUT", 201, 254, 50, HttpStatusCode.BadRequest)]
    [InlineData("PUT", 200, 255, 50, HttpStatusCode.BadRequest)]
    [InlineData("PUT", 200, 254, 51, HttpStatusCode.BadRequest)]
    public async Task Length_limits_are_enforced_at_the_exact_boundary(
        string method, int nameLength, int emailLength, int phoneLength, HttpStatusCode expected)
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var initial = await client.PostAsJsonAsync("/api/customers",
            new { name = "Original", email = "original@example.com" });
        var original = (await initial.Content.ReadFromJsonAsync<CustomerResponse>())!;
        var url = method == "POST" ? "/api/customers" : $"/api/customers/{original.Id}";
        var name = new string('n', nameLength);
        var email = $"{new string('e', emailLength - "@example.com".Length)}@example.com";
        var phone = new string('1', phoneLength);
        using var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new { name, email, phone })
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            await AssertProblemAsync(response, expected);
            Assert.Equal(original, Assert.Single((await client.GetFromJsonAsync<CustomerResponse[]>("/api/customers"))!));
        }
        else
        {
            var customer = (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
            Assert.Equal(name, customer.Name);
            Assert.Equal(email, customer.Email);
            Assert.Equal(phone, customer.Phone);
        }
    }

    [Fact]
    public async Task Blank_phone_is_cleared_and_email_is_not_a_unique_key()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        for (var index = 0; index < 2; index++)
        {
            using var response = await client.PostAsJsonAsync("/api/customers",
                new { name = $"Customer {index}", email = "shared@example.com", phone = " " });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Null((await response.Content.ReadFromJsonAsync<CustomerResponse>())!.Phone);
        }
        var customers = (await client.GetFromJsonAsync<CustomerResponse[]>("/api/customers"))!;
        Assert.Equal(2, customers.Length);
        Assert.NotEqual(customers[0].Id, customers[1].Id);
        Assert.Equal(customers.OrderBy(customer => customer.CreatedAt).ThenBy(customer => customer.Id), customers);
    }

    [Fact]
    public async Task New_application_starts_with_an_empty_customer_store()
    {
        using (var factory = CreateFactory())
        using (var client = CreateClient(factory))
        {
            using var response = await client.PostAsJsonAsync("/api/customers",
                new { name = "Temporary", email = "temporary@example.com" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        using var restartedFactory = CreateFactory();
        using var restartedClient = CreateClient(restartedFactory);
        Assert.Empty((await restartedClient.GetFromJsonAsync<CustomerResponse[]>("/api/customers"))!);
    }

    [Fact]
    public async Task OpenApi_documents_all_customer_operations()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var collection = paths.EnumerateObject().Single(path => path.Name.TrimEnd('/') == "/api/customers").Value;
        var resource = paths.GetProperty("/api/customers/{id}");
        AssertOperation(collection, "get", "GetCustomers", "200");
        AssertOperation(collection, "post", "CreateCustomer", "201", "400");
        AssertOperation(resource, "get", "GetCustomerById", "200", "404");
        AssertOperation(resource, "put", "UpdateCustomer", "200", "400", "404");
        AssertOperation(resource, "delete", "DeleteCustomer", "204", "404");
    }

    private static void AssertOperation(JsonElement path, string method, string operationId, params string[] statuses)
    {
        var operation = path.GetProperty(method);
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("description").GetString()));
        foreach (var status in statuses)
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, document.RootElement.GetProperty("status").GetInt32());
    }
}
