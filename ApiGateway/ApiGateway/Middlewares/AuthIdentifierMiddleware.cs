using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApiGateway.Middlewares;

internal sealed class AuthIdentifierMiddleware(RequestDelegate next)
{
    public const string ItemKey = "peaker.auth-identifier";

    private const int MaxInspectedBytes = 4 * 1024;
    private static readonly PathString AuthPath = new("/api/auth");
    private static readonly string[] IdentifierFields = ["identifier", "email"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsInspectable(context.Request))
        {
            await CaptureAsync(context);
        }

        await next(context);
    }

    private static bool IsInspectable(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) &&
        request.Path.StartsWithSegments(AuthPath, StringComparison.OrdinalIgnoreCase) &&
        request.ContentLength is > 0 and <= MaxInspectedBytes &&
        request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task CaptureAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        string? identifier = await ReadIdentifierAsync(context.Request);

        context.Request.Body.Position = 0;

        if (identifier is not null)
        {
            context.Items[ItemKey] = Fingerprint(identifier);
        }
    }

    private static async Task<string?> ReadIdentifierAsync(HttpRequest request)
    {
        byte[] buffer = new byte[MaxInspectedBytes];
        int read = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false);

        return Extract(buffer.AsMemory(0, read).Span);
    }

    private static string? Extract(ReadOnlySpan<byte> body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body.ToArray());

            return FirstStringField(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? FirstStringField(JsonElement root)
    {
        if (root.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        foreach (string field in IdentifierFields)
        {
            if (root.TryGetProperty(field, out JsonElement value) && value.ValueKind is JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static string Fingerprint(string identifier) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier.ToUpperInvariant())));
}
