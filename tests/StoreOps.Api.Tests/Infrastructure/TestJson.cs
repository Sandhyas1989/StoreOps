using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Shared.Errors;

namespace StoreOps.Api.Tests.Infrastructure;

public static class TestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Options);
        Assert.NotNull(value);
        return value;
    }

    public static async Task<ErrorResponse> ReadErrorAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var error = await response.ReadAsync<ErrorResponse>();
        Assert.Equal((int)expectedStatus, error.StatusCode);
        return error;
    }

    public static async Task<ActivityResponse> CreateActivityAsync(
        this HttpClient client,
        string title = "Restock aisle 4",
        string priority = "Medium")
    {
        var response = await client.PostAsJsonAsync("/api/activities", new { title, storeId = "STORE-001", priority });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<ActivityResponse>();
    }
}
