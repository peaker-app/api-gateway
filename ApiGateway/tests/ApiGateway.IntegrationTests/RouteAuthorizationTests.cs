using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class RouteAuthorizationTests : IDisposable
{
    private const string SampleId = "11111111-1111-1111-1111-111111111111";

    private readonly WebApplicationFactory<Program> _factory = new();

    public static TheoryData<string, string> ProtectedRoutes =>
        new()
        {
            { "GET", "/api/ascents" },
            { "POST", "/api/ascents" },
            { "DELETE", $"/api/ascents/{SampleId}" },
            { "POST", $"/api/ascents/{SampleId}/photos" },
            { "GET", "/api/collections" },
            { "GET", "/api/profiles/me" },
            { "PUT", "/api/profiles/me/slug" },
            { "POST", "/api/auth/logout" },
            { "DELETE", "/api/auth/me" }
        };

    public static TheoryData<string> AnonymousRoutes =>
        new()
        {
            "/api/peaks",
            $"/api/ascents/{SampleId}",
            $"/api/ascents/by-user/{SampleId}",
            $"/api/profiles/{SampleId}",
            "/api/peak/swagger/v1/swagger.json",
            "/api/ascent/swagger/v1/swagger.json"
        };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_WithoutToken_IsRejectedAtTheGateway(string method, string path)
    {
        using HttpClient client = _factory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AnonymousRoutes))]
    public async Task AnonymousRoute_WithoutToken_IsNotRejectedAtTheGateway(string path)
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _factory.Dispose();
}
