using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace ApiGateway.IntegrationTests;

internal sealed class TestJwtBearerConfiguration(TestTokenSigning tokenSigning)
    : IPostConfigureOptions<JwtBearerOptions>
{
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        options.Authority = null;
        options.MetadataAddress = string.Empty;
        options.RequireHttpsMetadata = false;
        options.Configuration = null;
        options.ConfigurationManager = null;
        options.TokenValidationParameters.IssuerSigningKey = tokenSigning.PublicKey;
        options.TokenValidationParameters.ValidIssuer = TestTokenSigning.Issuer;
        options.TokenValidationParameters.ValidAudiences = [TestTokenSigning.Audience];
    }
}
