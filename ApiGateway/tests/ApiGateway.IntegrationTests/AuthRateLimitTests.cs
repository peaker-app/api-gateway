using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ApiGateway.IntegrationTests;

public sealed class AuthRateLimitTests : IDisposable
{
    private const int AuthPermitLimit = 5;

    private readonly WebApplicationFactory<Program> _factory = new();

    [Fact]
    public async Task Login_BeyondFivePerMinute_IsRejectedWithTooManyRequests()
    {
        using HttpClient client = _factory.CreateClient();

        for (int attempt = 0; attempt < AuthPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostLoginAsync(client);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using HttpResponseMessage rejected = await PostLoginAsync(client);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ResendEmailConfirmation_BeyondFivePerMinute_IsRejectedWithTooManyRequests()
    {
        using HttpClient client = _factory.CreateClient();

        for (int attempt = 0; attempt < AuthPermitLimit; attempt++)
        {
            using HttpResponseMessage allowed = await PostResendAsync(client);
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        using HttpResponseMessage rejected = await PostResendAsync(client);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    public void Dispose() => _factory.Dispose();

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/login", new { identifier = "hiker", password = "irrelevant" });

    private static Task<HttpResponseMessage> PostResendAsync(HttpClient client) =>
        client.PostAsync(new Uri("/api/auth/email/resend", UriKind.Relative), content: null);
}
