using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ApiGateway.IntegrationTests;

/// <summary>
/// Las consultas geoespaciales son anónimas y caras (<c>ST_DWithin</c>), y el catálogo es
/// dependencia síncrona del camino de escritura de ascent-service y account-service: saturarlo
/// desde una ruta anónima degradaría dos servicios más.
/// </summary>
public sealed class CatalogRateLimitTests : IDisposable
{
    private const int CatalogPermitLimit = 20;

    private readonly IsolatedGateway _factory = new();

    [Fact]
    public async Task Nearby_BeyondItsOwnLimit_IsRejectedBeforeTheGlobalOne()
    {
        using HttpClient client = _factory.CreateClient();

        for (int attempt = 0; attempt < CatalogPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await client.GetAsync(NearbyRoute);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using HttpResponseMessage rejected = await client.GetAsync(NearbyRoute);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task PeakDetail_IsNotAffectedByTheCatalogQueryBucket()
    {
        using HttpClient client = _factory.CreateClient();

        for (int attempt = 0; attempt < CatalogPermitLimit + 1; attempt++)
        {
            using HttpResponseMessage warmUp = await client.GetAsync(SearchRoute);
        }

        using HttpResponseMessage detail = await client.GetAsync(
            new Uri("/api/peaks/0198f000-0000-7000-8000-0000000000a1", UriKind.Relative));

        detail.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    public void Dispose() => _factory.Dispose();

    private static Uri NearbyRoute => new("/api/peaks/nearby?lat=42.6&lon=0.6", UriKind.Relative);

    private static Uri SearchRoute => new("/api/peaks/search?q=aneto", UriKind.Relative);
}
