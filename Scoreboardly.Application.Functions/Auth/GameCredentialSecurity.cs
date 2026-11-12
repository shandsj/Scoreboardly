using System.Security.Cryptography;

namespace Scoreboardly.Application.Functions.Auth;

/// <summary>Creates and verifies high-entropy per-game backend credentials.</summary>
public static class GameCredentialSecurity
{
    /// <summary>Permission required for browser-originated score submissions.</summary>
    public const string ScoreSubmitScope = "scores:submit";

    private const string CredentialPrefix = "sb_live_";
    private const int SecretLengthBytes = 32;
    private const int SaltLengthBytes = 16;

    /// <summary>Creates an independently revocable credential record and its one-time raw value.</summary>
    public static CreatedGameCredential Create(
        string gameId, IEnumerable<string> allowedBoards, IEnumerable<string> scopes,
        DateTimeOffset now, string? rotationOfCredentialId = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var secretBytes = RandomNumberGenerator.GetBytes(SecretLengthBytes);
        var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
        var secret = Convert.ToHexString(secretBytes).ToLowerInvariant();
        var saltedSecret = new byte[salt.Length + secretBytes.Length];

        Buffer.BlockCopy(salt, 0, saltedSecret, 0, salt.Length);
        Buffer.BlockCopy(secretBytes, 0, saltedSecret, salt.Length, secretBytes.Length);

        var record = new GameCredential
        {
            Id = id,
            GameId = gameId,
            AllowedBoards = allowedBoards.ToArray(),
            Scopes = scopes.ToArray(),
            SecretSalt = Convert.ToBase64String(salt),
            SecretHash = Convert.ToBase64String(SHA256.HashData(saltedSecret)),
            CreatedAtUtc = now,
            RotatedAtUtc = rotationOfCredentialId is null ? null : now,
            RotationOfCredentialId = rotationOfCredentialId
        };

        return new CreatedGameCredential(record, $"{CredentialPrefix}{id}.{secret}");
    }

    /// <summary>Extracts the identifier from a correctly shaped credential.</summary>
    public static bool TryGetCredentialId(string? rawCredential, out string credentialId)
    {
        credentialId = string.Empty;
        if (string.IsNullOrWhiteSpace(rawCredential) ||
            !rawCredential.StartsWith(CredentialPrefix, StringComparison.Ordinal))
            return false;

        var separator = rawCredential.IndexOf('.', CredentialPrefix.Length);
        if (separator < 0) return false;

        var candidateId = rawCredential[CredentialPrefix.Length..separator];
        var secret = rawCredential[(separator + 1)..];
        if (!Guid.TryParseExact(candidateId, "N", out _) ||
            secret.Length != SecretLengthBytes * 2 ||
            !secret.All(Uri.IsHexDigit))
            return false;

        credentialId = candidateId;
        return true;
    }

    /// <summary>Verifies a salted SHA-256 hash using constant-time comparison.</summary>
    public static bool Verify(GameCredential record, string? rawCredential)
    {
        if (record.RevokedAtUtc is not null ||
            !TryGetCredentialId(rawCredential, out var credentialId) ||
            !string.Equals(record.Id, credentialId, StringComparison.Ordinal))
            return false;

        try
        {
            var separator = rawCredential!.IndexOf('.');
            var secretBytes = Convert.FromHexString(rawCredential[(separator + 1)..]);
            var salt = Convert.FromBase64String(record.SecretSalt);
            var expectedHash = Convert.FromBase64String(record.SecretHash);
            var input = new byte[salt.Length + secretBytes.Length];

            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(secretBytes, 0, input, salt.Length, secretBytes.Length);

            return CryptographicOperations.FixedTimeEquals(expectedHash, SHA256.HashData(input));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Checks current server-side board and scope permissions.</summary>
    public static bool CanSubmitToBoard(GameCredential record, Guid boardId) =>
        record.RevokedAtUtc is null &&
        record.AllowedBoards.Contains(boardId.ToString(), StringComparer.OrdinalIgnoreCase) &&
        record.Scopes.Contains(ScoreSubmitScope, StringComparer.Ordinal);
}
