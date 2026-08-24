using System.Globalization;
using System.Threading.RateLimiting;
using ApiGateway.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace ApiGateway.Extensions;

internal static class RateLimitingExtensions
{
    public const string AuthPolicyName = "auth";
    public const string RegisterPolicyName = "register";
    public const string CatalogPolicyName = "catalog";

    private const int GlobalPermitLimit = 100;
    private const int AuthPermitLimit = 5;
    private const int RegisterPermitLimit = 10;
    private const int CatalogPermitLimit = 20;
    private const string SubjectClaim = "sub";
    private const string UnknownClient = "unknown";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRetryAfterAsync;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => CreatePartition(ResolveSubjectKey(context), GlobalPermitLimit));

            options.AddPolicy(
                AuthPolicyName,
                context => CreatePartition(ResolveAuthKey(context), AuthPermitLimit));

            options.AddPolicy(
                RegisterPolicyName,
                context => CreatePartition(
                    ResolveClientKey(context), RegisterPermitLimit, RegisterWindow));

            options.AddPolicy(
                CatalogPolicyName,
                context => CreatePartition(ResolveSubjectKey(context), CatalogPermitLimit));
        });

    private static RateLimitPartition<string> CreatePartition(
        string partitionKey,
        int permitLimit,
        TimeSpan? window = null) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window ?? Window
            });

    private static string ResolveSubjectKey(HttpContext context)
    {
        string? subject = context.User.FindFirst(SubjectClaim)?.Value;

        return string.IsNullOrEmpty(subject) ? $"ip:{ResolveClientKey(context)}" : $"sub:{subject}";
    }

    private static string ResolveAuthKey(HttpContext context)
    {
        string clientKey = ResolveClientKey(context);

        return context.Items.TryGetValue(AuthIdentifierMiddleware.ItemKey, out object? fingerprint)
            ? $"{clientKey}|{fingerprint}"
            : clientKey;
    }

    private static string ResolveClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;

    private static ValueTask WriteRetryAfterAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    }
}
