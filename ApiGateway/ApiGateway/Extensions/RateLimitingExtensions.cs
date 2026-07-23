using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace ApiGateway.Extensions;

internal static class RateLimitingExtensions
{
    public const string AuthPolicyName = "auth";

    private const int GlobalPermitLimit = 100;
    private const int AuthPermitLimit = 5;

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => CreatePartition(context, GlobalPermitLimit));

            options.AddPolicy(AuthPolicyName, context => CreatePartition(context, AuthPermitLimit));
        });

    private static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ResolveClientKey(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });

    private static string ResolveClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
