using System.Net;
using System.Text.Json;

namespace DarkVelocity.E2E.Clients;

public sealed class HalResponse : IDisposable
{
    private readonly JsonDocument? _document;

    public HttpStatusCode StatusCode { get; }
    public bool IsSuccess => (int)StatusCode >= 200 && (int)StatusCode < 300;

    private HalResponse(HttpStatusCode statusCode, JsonDocument? document)
    {
        StatusCode = statusCode;
        _document = document;
    }

    public static async Task<HalResponse> FromAsync(HttpResponseMessage response)
    {
        var statusCode = response.StatusCode;
        JsonDocument? doc = null;

        var content = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                doc = JsonDocument.Parse(content);
            }
            catch (JsonException) { }
        }

        return new HalResponse(statusCode, doc);
    }

    public JsonElement Root => _document?.RootElement
        ?? throw new InvalidOperationException("Response has no JSON body");

    public string? GetId() =>
        Root.TryGetProperty("id", out var id) ? id.GetString() : null;

    public string? GetString(string property) =>
        Root.TryGetProperty(property, out var prop) ? prop.ToString() : null;

    public int? GetInt(string property) =>
        Root.TryGetProperty(property, out var prop) ? prop.GetInt32() : null;

    public string? GetLink(string rel)
    {
        if (Root.TryGetProperty("_links", out var links) &&
            links.TryGetProperty(rel, out var link) &&
            link.TryGetProperty("href", out var href))
        {
            return href.GetString();
        }
        return null;
    }

    public JsonElement? GetEmbedded(string rel)
    {
        if (Root.TryGetProperty("_embedded", out var embedded) &&
            embedded.TryGetProperty(rel, out var value))
        {
            return value;
        }
        return null;
    }

    public void Dispose() => _document?.Dispose();
}
