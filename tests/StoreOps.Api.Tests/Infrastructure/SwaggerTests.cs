using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace StoreOps.Api.Tests.Infrastructure;

public sealed class SwaggerTests : IDisposable
{
    // Factories derived with WithWebHostBuilder are disposed together with this root factory.
    private readonly StoreOpsApiFactory _root = new();

    public static TheoryData<string, string> RequiredEndpoints => new()
    {
        { "get", "/api/activities" },
        { "post", "/api/activities" },
        { "get", "/api/activities/{id}" },
        { "patch", "/api/activities/{id}" },
        { "delete", "/api/activities/{id}" },
        { "get", "/api/programmes" },
        { "post", "/api/programmes" },
        { "post", "/api/programmes/{id}/members" },
        { "get", "/api/alerts" },
    };

    [Theory]
    [MemberData(nameof(RequiredEndpoints))]
    public async Task Swagger_document_lists_required_endpoint_in_development(string method, string path)
    {
        using var client = CreateClient("Development");

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty(path, out var operations), $"Swagger is missing path {path}");
        Assert.True(operations.TryGetProperty(method, out _), $"Swagger is missing {method.ToUpperInvariant()} {path}");
    }

    [Fact]
    public async Task Swagger_ui_is_served_in_development()
    {
        using var client = CreateClient("Development");

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("swagger-ui", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    public async Task Swagger_is_not_exposed_outside_development(string environment)
    {
        using var client = CreateClient(environment);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }

    public void Dispose() => _root.Dispose();

    private HttpClient CreateClient(string environment) =>
        _root.WithWebHostBuilder(builder => builder.UseEnvironment(environment)).CreateClient();
}
