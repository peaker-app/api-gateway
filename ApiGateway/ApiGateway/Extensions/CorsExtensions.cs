namespace ApiGateway.Extensions;

internal static class CorsExtensions
{
    public const string PolicyName = "peaker-clients";

    private const string OriginsKey = "Cors:AllowedOrigins";
    private const string CorrelationHeader = "X-Correlation-Id";

    /// <summary>
    /// Declara la allowlist de orígenes que pueden llamar al gateway desde un navegador.
    /// <para>
    /// Sin <c>AllowCredentials</c> a propósito: los clientes autentican con
    /// <c>Authorization: Bearer</c>, nunca con cookies, y credenciales más orígenes reflejados
    /// es la combinación que convierte una allowlist en un agujero. Lista vacía equivale a
    /// no permitir ningún origen, que es el valor seguro por defecto en producción.
    /// </para>
    /// </summary>
    public static IServiceCollection AddGatewayCors(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddCors(options => options.AddPolicy(
            PolicyName,
            policy => policy
                .WithOrigins(AllowedOrigins(configuration))
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(CorrelationHeader)));

    /// <summary>
    /// Debe registrarse <b>antes</b> del rate limiter y de la autorización: un preflight
    /// <c>OPTIONS</c> viaja sin <c>Authorization</c>, así que detrás de <c>UseAuthorization</c>
    /// las rutas con política <c>authenticated</c> lo responderían con <c>401</c> y el navegador
    /// nunca llegaría a lanzar la petición real.
    /// </summary>
    public static IApplicationBuilder UseGatewayCors(this IApplicationBuilder app) =>
        app.UseCors(PolicyName);

    private static string[] AllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection(OriginsKey).Get<string[]>() ?? [];
}
