using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Scoreboardly.Application.Functions.Infrastructure;

namespace Scoreboardly.Application.Functions.Auth;

/// <summary>Operator-only credential lifecycle endpoints and the trusted-backend token exchange.</summary>
public sealed class AuthFunctions
{
    private static readonly Regex GameIdPattern = new(
        "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly GameCredentialRepository credentials;
    private readonly ScoreboardRepository scoreboards;
    private readonly AccessTokenService tokens;
    private readonly ILogger<AuthFunctions> logger;

    /// <summary>Initializes authentication endpoints.</summary>
    public AuthFunctions(
        GameCredentialRepository credentials,
        ScoreboardRepository scoreboards,
        AccessTokenService tokens,
        ILogger<AuthFunctions> logger)
    {
        this.credentials = credentials;
        this.scoreboards = scoreboards;
        this.tokens = tokens;
        this.logger = logger;
    }

    /// <summary>Provisions an independent per-game credential. Requires a Functions key and returns the secret once.</summary>
    [Function("ProvisionGameCredential")]
    public async Task<IActionResult> ProvisionGameCredentialAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/game-credentials")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        ProvisionGameCredentialRequest? body;
        try
        {
            body = JsonConvert.DeserializeObject<ProvisionGameCredentialRequest>(
                await new StreamReader(request.Body).ReadToEndAsync(cancellationToken));
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("Request body must be valid JSON.");
        }

        if (!TryNormalizeRequest(body, out var gameId, out var boards, out var scopes))
            return new BadRequestObjectResult("Provide a valid gameId, one or more GUID board identifiers, and supported scopes.");

        var created = GameCredentialSecurity.Create(gameId, boards, scopes, DateTimeOffset.UtcNow);
        await credentials.CreateAsync(created.Record, cancellationToken);
        logger.LogInformation("Provisioned credential {CredentialId} for game {GameId}.",
            created.Record.Id, created.Record.GameId);

        return new OkObjectResult(ToResponse(created));
    }

    /// <summary>Creates a replacement credential while the prior credential remains active for overlap.</summary>
    [Function("RotateGameCredential")]
    public async Task<IActionResult> RotateGameCredentialAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/game-credentials/{credentialId}/rotate")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var credentialId = request.RouteValues["credentialId"]?.ToString();
        if (string.IsNullOrWhiteSpace(credentialId)) return new BadRequestResult();

        var current = await credentials.GetByIdAsync(credentialId, cancellationToken);
        if (current is null) return new NotFoundResult();
        if (current.RevokedAtUtc is not null)
            return new ConflictObjectResult("A revoked credential cannot be rotated.");

        var created = GameCredentialSecurity.Create(
            current.GameId, current.AllowedBoards, current.Scopes,
            DateTimeOffset.UtcNow, current.Id);
        await credentials.CreateAsync(created.Record, cancellationToken);

        logger.LogInformation("Rotated credential {CredentialId} for game {GameId}; prior credential remains active.",
            created.Record.Id, created.Record.GameId);
        return new OkObjectResult(ToResponse(created));
    }

    /// <summary>Revokes a credential and invalidates its outstanding tokens on subsequent score writes.</summary>
    [Function("RevokeGameCredential")]
    public async Task<IActionResult> RevokeGameCredentialAsync(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "admin/game-credentials/{credentialId}")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var credentialId = request.RouteValues["credentialId"]?.ToString();
        if (string.IsNullOrWhiteSpace(credentialId)) return new BadRequestResult();

        var credential = await credentials.GetByIdAsync(credentialId, cancellationToken);
        if (credential is null) return new NotFoundResult();

        if (credential.RevokedAtUtc is null)
        {
            credential.RevokedAtUtc = DateTimeOffset.UtcNow;
            await credentials.ReplaceAsync(credential, cancellationToken);
            logger.LogInformation("Revoked credential {CredentialId} for game {GameId}.",
                credential.Id, credential.GameId);
        }

        return new NoContentResult();
    }

    /// <summary>Exchanges a private game credential for a ten-minute token scoped to one permitted board.</summary>
    [Function("IssueScoreAccessToken")]
    public async Task<IActionResult> IssueScoreAccessTokenAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/token")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = request.Headers["Authorization"].ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return new UnauthorizedResult();

        var rawCredential = authorization["Bearer ".Length..].Trim();
        if (!GameCredentialSecurity.TryGetCredentialId(rawCredential, out var credentialId))
            return new UnauthorizedResult();

        var credential = await credentials.GetByIdAsync(credentialId, cancellationToken);
        if (credential is null || !GameCredentialSecurity.Verify(credential, rawCredential))
            return new UnauthorizedResult();

        TokenRequest? body;
        try
        {
            body = JsonConvert.DeserializeObject<TokenRequest>(
                await new StreamReader(request.Body).ReadToEndAsync(cancellationToken));
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("Request body must be valid JSON.");
        }

        if (body is null || !Guid.TryParse(body.BoardId, out var boardId))
            return new BadRequestObjectResult("A valid boardId is required.");

        if (!GameCredentialSecurity.CanSubmitToBoard(credential, boardId))
            return new StatusCodeResult(StatusCodes.Status403Forbidden);

        if (await scoreboards.GetByIdAsync(boardId, cancellationToken) is null)
            return new NotFoundResult();

        string accessToken;
        try
        {
            accessToken = tokens.Issue(credential, boardId, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException)
        {
            logger.LogError("Scoreboardly token signing is not configured.");
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }

        return new OkObjectResult(new
        {
            accessToken,
            tokenType = "Bearer",
            expiresIn = (int)AccessTokenService.TokenLifetime.TotalSeconds
        });
    }

    private static bool TryNormalizeRequest(
        ProvisionGameCredentialRequest? request,
        out string gameId,
        out string[] boards,
        out string[] scopes)
    {
        gameId = request?.GameId?.Trim() ?? string.Empty;
        boards = [];
        scopes = [];

        if (!GameIdPattern.IsMatch(gameId) || request?.AllowedBoards is null || request.AllowedBoards.Length == 0)
            return false;

        var normalizedBoards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var board in request.AllowedBoards)
        {
            if (!Guid.TryParse(board, out var boardId)) return false;
            normalizedBoards.Add(boardId.ToString());
        }

        var requestedScopes = request.Scopes is { Length: > 0 }
            ? request.Scopes
            : new[] { GameCredentialSecurity.ScoreSubmitScope };

        if (requestedScopes.Any(scope =>
            !string.Equals(scope, GameCredentialSecurity.ScoreSubmitScope, StringComparison.Ordinal)))
            return false;

        boards = normalizedBoards.ToArray();
        scopes = requestedScopes.Distinct(StringComparer.Ordinal).ToArray();
        return true;
    }

    private static CredentialProvisioningResponse ToResponse(CreatedGameCredential created) => new()
    {
        CredentialId = created.Record.Id,
        Credential = created.RawCredential,
        GameId = created.Record.GameId,
        AllowedBoards = created.Record.AllowedBoards,
        Scopes = created.Record.Scopes,
        CreatedAtUtc = created.Record.CreatedAtUtc
    };
}

/// <summary>Operator input for creating a game credential.</summary>
public sealed class ProvisionGameCredentialRequest
{
    /// <summary>Stable game identity.</summary>
    [JsonProperty("gameId")]
    public string? GameId { get; set; }

    /// <summary>Board GUIDs the game may submit scores to.</summary>
    [JsonProperty("allowedBoards")]
    public string[]? AllowedBoards { get; set; }

    /// <summary>Allowed operations; defaults to scores:submit.</summary>
    [JsonProperty("scopes")]
    public string[]? Scopes { get; set; }
}

/// <summary>Board requested by a trusted backend during token exchange.</summary>
public sealed class TokenRequest
{
    /// <summary>Board GUID for which the short-lived token is requested.</summary>
    [JsonProperty("boardId")]
    public string? BoardId { get; set; }
}

/// <summary>One-time response returned when a game credential is created or rotated.</summary>
public sealed class CredentialProvisioningResponse
{
    /// <summary>Public credential identifier.</summary>
    [JsonProperty("credentialId")]
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>Raw secret. This is returned only at provisioning time.</summary>
    [JsonProperty("credential")]
    public string Credential { get; set; } = string.Empty;

    /// <summary>Game identity.</summary>
    [JsonProperty("gameId")]
    public string GameId { get; set; } = string.Empty;

    /// <summary>Board allowlist.</summary>
    [JsonProperty("allowedBoards")]
    public string[] AllowedBoards { get; set; } = [];

    /// <summary>Granted operation scopes.</summary>
    [JsonProperty("scopes")]
    public string[] Scopes { get; set; } = [];

    /// <summary>Provisioning timestamp.</summary>
    [JsonProperty("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; }
}
