using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Scoreboardly.Application.Functions.Auth;

/// <summary>Issues and validates short-lived HMAC-signed Scoreboardly access tokens.</summary>
public sealed class AccessTokenService
{
    /// <summary>Lifetime of an issued browser access token.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    private const string DefaultIssuer = "scoreboardly";
    private const string DefaultAudience = "scoreboardly-api";
    private readonly IConfiguration configuration;
    private readonly JwtSecurityTokenHandler handler = new() { MapInboundClaims = false };

    /// <summary>Initializes token handling from server-only application configuration.</summary>
    public AccessTokenService(IConfiguration configuration) => this.configuration = configuration;

    /// <summary>Creates a token for one game, one board, and the credential's authorized scopes.</summary>
    public string Issue(GameCredential credential, Guid boardId, DateTimeOffset now)
    {
        if (credential.RevokedAtUtc is not null ||
            !GameCredentialSecurity.CanSubmitToBoard(credential, boardId))
            throw new InvalidOperationException("The credential is not authorized for this board.");

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = GetIssuer(),
            Audience = GetAudience(),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, credential.GameId),
                new Claim("game_id", credential.GameId),
                new Claim("board_id", boardId.ToString()),
                new Claim("credential_id", credential.Id),
                new Claim("scope", GameCredentialSecurity.ScoreSubmitScope),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(TokenLifetime).UtcDateTime,
            SigningCredentials = new SigningCredentials(GetSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>Validates signature, algorithm, issuer, audience, lifetime, and required authorization claims.</summary>
    /// <exception cref="SecurityTokenException">The token is invalid or lacks required claims.</exception>
    public AccessTokenClaims Validate(string rawToken)
    {
        var principal = handler.ValidateToken(rawToken, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSigningKey(),
            ValidateIssuer = true,
            ValidIssuer = GetIssuer(),
            ValidateAudience = true,
            ValidAudience = GetAudience(),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        }, out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwt ||
            !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
            throw new SecurityTokenException("Unsupported access-token algorithm.");

        var credentialId = principal.FindFirst("credential_id")?.Value;
        var gameId = principal.FindFirst("game_id")?.Value;
        var boardId = principal.FindFirst("board_id")?.Value;
        var tokenId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var scopes = principal.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        if (string.IsNullOrWhiteSpace(credentialId) ||
            string.IsNullOrWhiteSpace(gameId) ||
            string.IsNullOrWhiteSpace(tokenId) ||
            !Guid.TryParse(boardId, out _) ||
            scopes.Length == 0)
            throw new SecurityTokenException("Access token is missing required claims.");

        return new AccessTokenClaims(credentialId, gameId, boardId!, scopes);
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var encodedKey = configuration["SCOREBOARDLY_TOKEN_SIGNING_KEY"];
        if (string.IsNullOrWhiteSpace(encodedKey))
            throw new InvalidOperationException("SCOREBOARDLY_TOKEN_SIGNING_KEY is not configured.");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("SCOREBOARDLY_TOKEN_SIGNING_KEY must be base64-encoded.", exception);
        }

        if (keyBytes.Length < 32)
            throw new InvalidOperationException("SCOREBOARDLY_TOKEN_SIGNING_KEY must contain at least 32 random bytes.");

        return new SymmetricSecurityKey(keyBytes);
    }

    private string GetIssuer() => configuration["SCOREBOARDLY_TOKEN_ISSUER"] ?? DefaultIssuer;
    private string GetAudience() => configuration["SCOREBOARDLY_TOKEN_AUDIENCE"] ?? DefaultAudience;
}
