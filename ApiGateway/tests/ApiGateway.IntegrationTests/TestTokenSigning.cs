using System.Security.Cryptography;
using Common.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.IntegrationTests;

internal sealed class TestTokenSigning : IDisposable
{
    public const string Issuer = "peaker-auth";
    public const string Audience = "peaker-api";

    private static readonly JsonWebTokenHandler TokenHandler = new() { SetDefaultTimesOnTokenCreation = false };

    private readonly RSA _rsa = RSA.Create(2048);

    public SecurityKey PublicKey => new RsaSecurityKey(_rsa.ExportParameters(includePrivateParameters: false));

    public string CreateAccessToken(params string[] roles)
    {
        DateTime now = DateTime.UtcNow;

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(15),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.CreateVersion7().ToString(),
                [PeakerRoles.ClaimType] = roles
            },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256)
        };

        return TokenHandler.CreateToken(descriptor);
    }

    public void Dispose() => _rsa.Dispose();
}
