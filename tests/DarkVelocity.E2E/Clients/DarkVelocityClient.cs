using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DarkVelocity.E2E.Auth;

namespace DarkVelocity.E2E.Clients;

public sealed class DarkVelocityClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public DarkVelocityClient(HttpClient http, string baseUrl)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public void Authenticate(Guid userId, Guid orgId, Guid? siteId = null, Guid? sessionId = null)
    {
        var token = TestTokenGenerator.GenerateToken(userId, orgId, siteId, sessionId);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<HalResponse> GetAsync(string path)
    {
        var response = await _http.GetAsync($"{_baseUrl}{path}");
        return await HalResponse.FromAsync(response);
    }

    public async Task<HalResponse> PostAsync(string path, object? body = null)
    {
        var content = body is not null
            ? new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            : null;

        var response = await _http.PostAsync($"{_baseUrl}{path}", content);
        return await HalResponse.FromAsync(response);
    }

    public async Task<HalResponse> PatchAsync(string path, object body)
    {
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.PatchAsync($"{_baseUrl}{path}", content);
        return await HalResponse.FromAsync(response);
    }

    public async Task<HalResponse> DeleteAsync(string path)
    {
        var response = await _http.DeleteAsync($"{_baseUrl}{path}");
        return await HalResponse.FromAsync(response);
    }
}
