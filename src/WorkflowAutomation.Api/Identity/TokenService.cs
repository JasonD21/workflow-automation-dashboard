using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Identity;

public record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(ApplicationUser user);
    Task<string> CreateRefreshTokenAsync(Guid userId, CancellationToken ct = default);
    Task<(Guid UserId, string RefreshToken)?> RotateRefreshTokenAsync(string rawToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string rawToken, CancellationToken ct = default);
}

public class TokenService(AppDbContext db, IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;

    public AccessToken CreateAccessToken(ApplicationUser user)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("demo", user.IsDemo ? "true" : "false"),
        ];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public async Task<string> CreateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var raw = GenerateSecureToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays)
        });
        await db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<(Guid UserId, string RefreshToken)?> RotateRefreshTokenAsync(
        string rawToken, CancellationToken ct = default)
    {
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null || existing.RevokedAt is not null
            || existing.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        var newRaw = GenerateSecureToken();
        var replacement = new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = Hash(newRaw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays)
        };
        db.RefreshTokens.Add(replacement);

        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedByTokenId = replacement.Id;

        await db.SaveChangesAsync(ct);
        return (existing.UserId, newRaw);
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is { RevokedAt: null })
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private static string GenerateSecureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}