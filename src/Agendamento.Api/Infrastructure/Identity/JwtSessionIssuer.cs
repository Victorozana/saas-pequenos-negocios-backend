using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agendamento.Application.Identity;
using Agendamento.Application.Identity.CreateSession;

namespace Agendamento.Infrastructure.Identity;

/// <summary>Issues a short-lived, HS256 signed bearer token with only the session context claims.</summary>
public sealed class JwtSessionIssuer(JwtSessionIssuerOptions options) : ISessionIssuer
{
    public IssuedSession Issue(SessionPrincipal principal)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(options.Lifetime);
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = principal.UserId,
            tenant_id = principal.TenantId,
            role = principal.Role,
            iat = now.ToUnixTimeSeconds(),
            exp = expiresAt.ToUnixTimeSeconds(),
        }));
        var unsignedToken = $"{header}.{payload}";
        var signature = Sign(unsignedToken, options.SigningKey);

        return new IssuedSession($"{unsignedToken}.{signature}", expiresAt);
    }

    private static string Sign(string content, string signingKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}

public sealed record JwtSessionIssuerOptions(string SigningKey, TimeSpan Lifetime)
{
    public static JwtSessionIssuerOptions Create(string signingKey) => new(signingKey, TimeSpan.FromMinutes(15));
}
