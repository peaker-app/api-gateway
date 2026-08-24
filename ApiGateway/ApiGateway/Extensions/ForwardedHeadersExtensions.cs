using System.Net;
using IPNetwork = System.Net.IPNetwork;
using Microsoft.AspNetCore.HttpOverrides;

namespace ApiGateway.Extensions;

internal static class ForwardedHeadersExtensions
{
    private const string ProxiesKey = "ForwardedHeaders:KnownProxies";
    private const string NetworksKey = "ForwardedHeaders:KnownNetworks";

    /// <summary>
    /// Configura <c>X-Forwarded-For</c> para que solo se honre desde los proxies declarados
    /// en configuración.
    /// </summary>
    public static IServiceCollection AddGatewayForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (IPAddress proxy in KnownProxies(configuration))
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (IPNetwork network in KnownNetworks(configuration))
            {
                options.KnownIPNetworks.Add(network);
            }
        });

    /// <summary>
    /// Activa el middleware <b>solo</b> si hay algún proxy de confianza declarado. Con las
    /// dos listas vacías ASP.NET desactiva la comprobación de origen y aceptaría la cabecera
    /// de cualquiera, que es exactamente lo que no se quiere.
    /// </summary>
    public static IApplicationBuilder UseGatewayForwardedHeaders(this WebApplication app)
    {
        bool hasTrustedProxy = KnownProxies(app.Configuration).Count > 0
            || KnownNetworks(app.Configuration).Count > 0;

        if (hasTrustedProxy)
        {
            app.UseForwardedHeaders();
        }
        else
        {
            app.Logger.LogWarning(
                "Sin proxies de confianza declarados: se ignora X-Forwarded-For y el rate limit "
                + "se reparte entre todos los clientes que compartan IP de conexión.");
        }

        return app;
    }

    private static List<IPAddress> KnownProxies(IConfiguration configuration) =>
        [.. Entries(configuration, ProxiesKey)
            .Select(value => IPAddress.TryParse(value, out IPAddress? address) ? address : null)
            .OfType<IPAddress>()];

    private static List<IPNetwork> KnownNetworks(IConfiguration configuration) =>
        [.. Entries(configuration, NetworksKey).Select(ParseNetwork).OfType<IPNetwork>()];

    private static string[] Entries(IConfiguration configuration, string key) =>
        configuration.GetSection(key).Get<string[]>() ?? [];

    private static IPNetwork? ParseNetwork(string value)
    {
        string[] parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 2
            && IPAddress.TryParse(parts[0], out IPAddress? prefix)
            && int.TryParse(parts[1], out int length)
                ? new IPNetwork(prefix, length)
                : null;
    }
}
