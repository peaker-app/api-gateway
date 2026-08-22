using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.IntegrationTests;

/// <summary>
/// El portal habla con el gateway desde su BFF, pero la app móvil de `peaker-mobile` itera en
/// navegador contra `vite dev`, y ahí el origen sí importa (MOBILE.md §1.5).
/// </summary>
public sealed class CorsTests : IDisposable
{
    private const string AllowedOrigin = "http://localhost:5173";
    private const string ForeignOrigin = "https://evil.example";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string ExposeHeadersHeader = "Access-Control-Expose-Headers";
    private const string RequestMethodHeader = "Access-Control-Request-Method";

    private readonly ConfiguredGateway _gateway = new();

    [Fact]
    public async Task Preflight_FromAllowedOrigin_IsAccepted()
    {
        using HttpClient client = _gateway.CreateClient();

        using HttpResponseMessage response = await PreflightAsync(client, "/api/peaks", AllowedOrigin);

        response.Headers.GetValues(AllowOriginHeader).Should().ContainSingle(AllowedOrigin);
    }

    /// <summary>
    /// Fija el orden del pipeline: CORS antes de la autorización. Si se invierte, el preflight
    /// de una ruta protegida se responde con 401 y la petición real nunca sale del navegador.
    /// <para>
    /// La ruta importa: <c>/api/collections</c> exige política <c>authenticated</c> y no
    /// restringe métodos, así que el <c>OPTIONS</c> casa con ella. Las rutas protegidas de
    /// <c>/api/ascents</c> sí filtran por método, de modo que el preflight caería en la ruta
    /// anónima de orden 2 y el test no probaría nada.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Preflight_OnProtectedRoute_IsNotRejectedAsUnauthorized()
    {
        using HttpClient client = _gateway.CreateClient();

        using HttpResponseMessage response = await PreflightAsync(client, "/api/collections", AllowedOrigin);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.Headers.GetValues(AllowOriginHeader).Should().ContainSingle(AllowedOrigin);
    }

    [Fact]
    public async Task Preflight_FromForeignOrigin_DoesNotReceiveTheAllowHeader()
    {
        using HttpClient client = _gateway.CreateClient();

        using HttpResponseMessage response = await PreflightAsync(client, "/api/peaks", ForeignOrigin);

        response.Headers.Contains(AllowOriginHeader).Should().BeFalse();
    }

    [Fact]
    public async Task Request_FromAllowedOrigin_ExposesTheCorrelationHeader()
    {
        using HttpClient client = _gateway.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/peaks");
        request.Headers.TryAddWithoutValidation("Origin", AllowedOrigin);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.Headers.GetValues(AllowOriginHeader).Should().ContainSingle(AllowedOrigin);
        response.Headers.GetValues(ExposeHeadersHeader).Should().ContainSingle("X-Correlation-Id");
    }

    public void Dispose() => _gateway.Dispose();

    private static async Task<HttpResponseMessage> PreflightAsync(
        HttpClient client,
        string path,
        string origin)
    {
        using HttpRequestMessage request = new(HttpMethod.Options, path);
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation(RequestMethodHeader, "GET");

        return await client.SendAsync(request);
    }

    private sealed class ConfiguredGateway()
        : IsolatedGateway(new KeyValuePair<string, string?>("Cors:AllowedOrigins:0", AllowedOrigin));
}
