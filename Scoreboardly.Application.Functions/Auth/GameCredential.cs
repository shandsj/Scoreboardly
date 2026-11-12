using Newtonsoft.Json;

namespace Scoreboardly.Application.Functions.Auth;

/// <summary>
/// Server-side record for one independently revocable game backend credential.
/// The raw credential is never stored in this document.
/// </summary>
public sealed class GameCredential
{
    /// <summary>Cosmos document identifier and partition key.</summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Stable game identity chosen by the operator.</summary>
    [JsonProperty("gameId")]
    public string GameId { get; set; } = string.Empty;

    /// <summary>Normalized board identifiers this credential may access.</summary>
    [JsonProperty("allowedBoards")]
    public string[] AllowedBoards { get; set; } = [];

    /// <summary>Operation scopes granted to this credential.</summary>
    [JsonProperty("scopes")]
    public string[] Scopes { get; set; } = [];

    /// <summary>Base64-encoded random salt used when hashing the secret.</summary>
    [JsonProperty("secretSalt")]
    public string SecretSalt { get; set; } = string.Empty;

    /// <summary>Base64-encoded SHA-256 hash of the salted secret.</summary>
    [JsonProperty("secretHash")]
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this credential was provisioned.</summary>
    [JsonProperty("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp when this credential replaced another credential, if rotated.</summary>
    [JsonProperty("rotatedAtUtc")]
    public DateTimeOffset? RotatedAtUtc { get; set; }

    /// <summary>Identifier of the credential this record replaced, if rotated.</summary>
    [JsonProperty("rotationOfCredentialId")]
    public string? RotationOfCredentialId { get; set; }

    /// <summary>UTC timestamp when this credential was revoked; null while active.</summary>
    [JsonProperty("revokedAtUtc")]
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

/// <summary>
/// A newly provisioned credential. The raw value is returned once and must not be persisted by this service.
/// </summary>
public sealed record CreatedGameCredential(GameCredential Record, string RawCredential);

/// <summary>
/// Claims extracted from a verified short-lived access token.
/// </summary>
public sealed record AccessTokenClaims(
    string CredentialId,
    string GameId,
    string BoardId,
    string[] Scopes);
