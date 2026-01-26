using System.Net.Http.Json;
using System.Text.Json;

namespace DarkVelocity.Tests.Shared.Extensions;

public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T?> GetFromJsonAsync<T>(this HttpClient client, string uri)
    {
        var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(
        this HttpClient client,
        string uri,
        T value)
    {
        return await client.PostAsJsonAsync(uri, value, JsonOptions);
    }

    public static async Task<TResponse?> PostAndReadAsync<TRequest, TResponse>(
        this HttpClient client,
        string uri,
        TRequest request)
    {
        var response = await client.PostAsJsonAsync(uri, request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
    }
}
