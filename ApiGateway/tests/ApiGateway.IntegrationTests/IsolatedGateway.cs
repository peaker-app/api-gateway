using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.IntegrationTests;

/// <summary>
/// Gateway con todos sus destinos apuntando a un puerto muerto y con la espera acotada a 100 ms.
/// Sin esto la suite depende de si la pila local está levantada: con ella arriba los servicios
/// reales contestan sus propios <c>401</c> y <c>429</c> —el retardo de login de auth-service, por
/// ejemplo— y con ella abajo cada petición tarda segundos en fallar, de modo que las ventanas fijas
/// del limitador se renuevan a mitad de un test. Aquí solo se ejercita el borde, así que la
/// respuesta del destino no importa: importa que llegue rápido y siempre igual.
/// </summary>
internal class IsolatedGateway(params KeyValuePair<string, string?>[] overrides)
    : WebApplicationFactory<Program>
{
    private const string DeadDestination = "http://127.0.0.1:1/";
    private const string FailFast = "00:00:00.100";
    private const string LoopbackNetwork = "ForwardedHeaders:KnownNetworks:0";

    private static readonly string[] ClusterIds = ["auth", "account", "peak", "ascent"];

    public static IsolatedGateway TrustingLoopbackProxies() =>
        new(new KeyValuePair<string, string?>(LoopbackNetwork, "127.0.0.0/8"));

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(Settings()));

    private Dictionary<string, string?> Settings()
    {
        Dictionary<string, string?> settings = [];

        foreach (string cluster in ClusterIds)
        {
            settings[$"ReverseProxy:Clusters:{cluster}:Destinations:{cluster}-service:Address"] =
                DeadDestination;
            settings[$"ReverseProxy:Clusters:{cluster}:HttpRequest:ActivityTimeout"] = FailFast;
        }

        foreach ((string key, string? value) in overrides)
        {
            settings[key] = value;
        }

        return settings;
    }
}
