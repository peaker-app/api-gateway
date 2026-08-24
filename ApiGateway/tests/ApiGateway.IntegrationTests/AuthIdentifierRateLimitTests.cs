using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.IntegrationTests;

/// <summary>
/// El cubo de <c>auth</c> se parte por IP <b>y</b> por el identificador del cuerpo: de lo
/// contrario una sola persona reintentando su contraseña deja sin login a los demás visitantes
/// que comparten salida NAT.
/// </summary>
public sealed class AuthIdentifierRateLimitTests : IDisposable
{
    private const int AuthPermitLimit = 5;

    private readonly IsolatedGateway _gateway = IsolatedGateway.TrustingLoopbackProxies();

    [Fact]
    public async Task Login_ExhaustedForOneIdentifier_StillAllowsAnother()
    {
        using HttpClient client = _gateway.CreateClient();

        await ExhaustAsync(client, "hiker", "203.0.113.20");

        using HttpResponseMessage sameIdentifier = await PostLoginAsync(client, "hiker", "203.0.113.20");
        using HttpResponseMessage otherIdentifier = await PostLoginAsync(client, "climber", "203.0.113.20");

        sameIdentifier.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherIdentifier.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_ExhaustedFromOneAddress_StillAllowsTheSameIdentifierElsewhere()
    {
        using HttpClient client = _gateway.CreateClient();

        await ExhaustAsync(client, "shared", "203.0.113.30");

        using HttpResponseMessage sameAddress = await PostLoginAsync(client, "shared", "203.0.113.30");
        using HttpResponseMessage otherAddress = await PostLoginAsync(client, "shared", "203.0.113.31");

        sameAddress.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        otherAddress.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_WhenRejected_AnnouncesRetryAfter()
    {
        using HttpClient client = _gateway.CreateClient();

        await ExhaustAsync(client, "patient", "203.0.113.40");

        using HttpResponseMessage rejected = await PostLoginAsync(client, "patient", "203.0.113.40");

        rejected.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithAMalformedBody_FallsBackToTheAddressBucket()
    {
        using HttpClient client = _gateway.CreateClient();

        for (int attempt = 0; attempt < AuthPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostRawAsync(client, "{ not json", "203.0.113.50");
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using HttpResponseMessage rejected = await PostRawAsync(client, "{ not json", "203.0.113.50");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    public void Dispose() => _gateway.Dispose();

    private static async Task ExhaustAsync(HttpClient client, string identifier, string forwardedFor)
    {
        for (int attempt = 0; attempt < AuthPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostLoginAsync(client, identifier, forwardedFor);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string identifier,
        string forwardedFor) =>
        SendAsync(
            client,
            JsonContent.Create(new { identifier, password = "irrelevant" }),
            forwardedFor);

    private static Task<HttpResponseMessage> PostRawAsync(
        HttpClient client,
        string body,
        string forwardedFor) =>
        SendAsync(
            client,
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            forwardedFor);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpContent content,
        string forwardedFor)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login") { Content = content };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);

        return await client.SendAsync(request);
    }
}
