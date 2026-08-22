using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.IntegrationTests;

/// <summary>
/// El cubo de <c>register</c> se parte solo por IP, al revés que el de <c>auth</c>: como el alta
/// devuelve <c>409</c> ante un correo ya registrado, partir además por el correo del cuerpo daría
/// un cubo nuevo a cada candidato y dejaría la enumeración sin techo.
/// </summary>
public sealed class RegisterRateLimitTests : IDisposable
{
    private const int RegisterPermitLimit = 10;

    private readonly IsolatedGateway _gateway = IsolatedGateway.TrustingLoopbackProxies();

    [Fact]
    public async Task Register_BeyondTheLimit_IsRejectedWithTooManyRequests()
    {
        using HttpClient client = _gateway.CreateClient();

        await ExhaustAsync(client, "198.51.100.10");

        using HttpResponseMessage rejected = await PostRegisterAsync(client, "198.51.100.10");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_WithADifferentEmailEachTime_StillShareTheAddressBucket()
    {
        using HttpClient client = _gateway.CreateClient();

        await ExhaustAsync(client, "198.51.100.20");

        using HttpResponseMessage rejected = await PostRegisterAsync(client, "198.51.100.20");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Register_ExhaustedFromOneAddress_StillAllowsAnother()
    {
        using HttpClient client = _gateway.CreateClient();

        await ExhaustAsync(client, "198.51.100.30");

        using HttpResponseMessage sameAddress = await PostRegisterAsync(client, "198.51.100.30");
        using HttpResponseMessage otherAddress = await PostRegisterAsync(client, "198.51.100.31");

        sameAddress.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherAddress.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Register_DoesNotSpendTheLoginBucketOfTheSameAddress()
    {
        using HttpClient client = _gateway.CreateClient();

        await ExhaustAsync(client, "198.51.100.40");

        using HttpResponseMessage login = await PostLoginAsync(client, "198.51.100.40");

        login.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    public void Dispose() => _gateway.Dispose();

    private static async Task ExhaustAsync(HttpClient client, string forwardedFor)
    {
        for (int attempt = 0; attempt < RegisterPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostRegisterAsync(client, forwardedFor);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }

    private static Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, string forwardedFor) =>
        SendAsync(
            client,
            "/api/auth/register",
            new
            {
                email = $"hiker-{Guid.CreateVersion7():N}@peaker.invalid",
                username = "hiker",
                password = "irrelevant",
                acceptedTerms = true
            },
            forwardedFor);

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string forwardedFor) =>
        SendAsync(
            client,
            "/api/auth/login",
            new { identifier = "hiker", password = "irrelevant" },
            forwardedFor);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string path,
        object body,
        string forwardedFor)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);

        return await client.SendAsync(request);
    }
}
