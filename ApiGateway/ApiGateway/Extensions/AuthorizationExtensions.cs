using Microsoft.AspNetCore.Authorization;

namespace ApiGateway.Extensions;

internal static class AuthorizationExtensions
{
    public const string AuthenticatedPolicyName = "authenticated";

    public static IServiceCollection AddGatewayAuthorization(this IServiceCollection services) =>
        services.AddAuthorization(options =>
            options.AddPolicy(AuthenticatedPolicyName, policy => policy.RequireAuthenticatedUser()));
}
