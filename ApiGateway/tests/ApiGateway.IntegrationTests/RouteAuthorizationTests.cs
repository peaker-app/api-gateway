using System.Net;
using System.Net.Http.Headers;
using Common.Application.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class RouteAuthorizationTests : IDisposable
{
    private const string SampleId = "11111111-1111-1111-1111-111111111111";

    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly TestTokenSigning _tokenSigning = new();
    private readonly WebApplicationFactory<Program> _signedFactory;

    public RouteAuthorizationTests() => _signedFactory = CreateSignedFactory();

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
            { "DELETE", "/api/auth/me" },
            { "POST", $"/api/admin/users/{SampleId}/unlock" },
            { "POST", $"/api/admin/users/{SampleId}/roles" },
            { "DELETE", $"/api/admin/users/{SampleId}/roles/Admin" }
        };

    public static TheoryData<string, string> AdminRoutes =>
        new()
        {
            { "POST", $"/api/admin/users/{SampleId}/unlock" },
            { "POST", $"/api/admin/users/{SampleId}/roles" },
            { "DELETE", $"/api/admin/users/{SampleId}/roles/Admin" }
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

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task AdminRoute_WithARegularToken_IsForbiddenAtTheGateway(string method, string path)
    {
        using HttpResponseMessage response = await SendSignedAsync(method, path, _tokenSigning.CreateAccessToken());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task AdminRoute_WithAnAdminToken_IsNotRejectedAtTheGateway(string method, string path)
    {
        using HttpResponseMessage response = await SendSignedAsync(
            method, path, _tokenSigning.CreateAccessToken(PeakerRoles.Admin));

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    public void Dispose()
    {
        _signedFactory.Dispose();
        _factory.Dispose();
        _tokenSigning.Dispose();
    }

    private async Task<HttpResponseMessage> SendSignedAsync(string method, string path, string accessToken)
    {
        using HttpClient client = _signedFactory.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await client.SendAsync(request);
    }

    private WebApplicationFactory<Program> CreateSignedFactory() =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>>(
                new TestJwtBearerConfiguration(_tokenSigning))));
}
