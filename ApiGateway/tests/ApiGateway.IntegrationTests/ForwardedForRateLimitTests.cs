using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.IntegrationTests;

/// <summary>
/// El BFF del portal es el único origen que habla con el gateway (RF-WEB-09), así que sin
/// honrar X-Forwarded-For todos los visitantes compartirían el mismo cubo de 5 por minuto.
/// <para>
/// El rechazo desde un proxy <b>no</b> declarado no se puede ejercitar aquí: TestServer no
/// tiene IP de conexión, y sin ella el middleware no puede validar el origen. Esa mitad se
/// cubre en despliegue con <c>ForwardedHeaders__KnownNetworks__0</c>.
/// </para>
/// </summary>
public sealed class ForwardedForRateLimitTests : IDisposable
{
    private const int AuthPermitLimit = 5;

    private readonly IsolatedGateway _trusting = IsolatedGateway.TrustingLoopbackProxies();
    private readonly IsolatedGateway _default = new();

    [Fact]
    public async Task Login_WithTrustedProxy_PartitionsEachForwardedAddressSeparately()
    {
        using HttpClient client = _trusting.CreateClient();

        for (int attempt = 0; attempt < AuthPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostLoginAsync(client, "203.0.113.10");
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using HttpResponseMessage exhausted = await PostLoginAsync(client, "203.0.113.10");
        using HttpResponseMessage otherVisitor = await PostLoginAsync(client, "203.0.113.99");

        exhausted.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherVisitor.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_WithoutConfiguredProxies_IgnoresTheForwardedHeader()
    {
        using HttpClient client = _default.CreateClient();

        for (int attempt = 0; attempt < AuthPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostLoginAsync(client, "203.0.113.10");
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using HttpResponseMessage spoofed = await PostLoginAsync(client, "203.0.113.99");

        spoofed.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    public void Dispose()
    {
        _trusting.Dispose();
        _default.Dispose();
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string forwardedFor)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { identifier = "hiker", password = "irrelevant" }),
        };

        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);

        return await client.SendAsync(request);
    }
}
