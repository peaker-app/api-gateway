using ApiGateway.Extensions;
using Common.API.Health;
using Common.API.Middlewares;
using Common.API.Security;
using Serilog;
using Serilog.Formatting.Compact;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", "api-gateway")
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddCommonJwtAuthentication(builder.Configuration);
builder.Services.AddGatewayForwardedHeaders(builder.Configuration);
builder.Services.AddGatewayRateLimiting();
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

app.UseGatewayForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api/auth/swagger/v1/swagger.json", "auth-service");
        options.SwaggerEndpoint("/api/account/swagger/v1/swagger.json", "account-service");
        options.SwaggerEndpoint("/api/peak/swagger/v1/swagger.json", "peak-service");
        options.SwaggerEndpoint("/api/ascent/swagger/v1/swagger.json", "ascent-service");
        options.RoutePrefix = "swagger";
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapCommonHealthChecks();
app.MapReverseProxy();

await app.RunAsync();

public partial class Program;
