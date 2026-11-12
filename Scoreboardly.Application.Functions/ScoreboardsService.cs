using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Scoreboardly.Application.Functions.Api;
using Scoreboardly.Application.Functions.Auth;
using Scoreboardly.Application.Functions.Infrastructure;

namespace Scoreboardly.Application.Functions;

/// <summary>HTTP endpoints for public leaderboard reads and score submissions.</summary>
public sealed class ScoreboardsService
{
    private readonly ScoreboardRepository scoreboards;
    private readonly GameCredentialRepository credentials;
    private readonly AccessTokenService tokens;
    private readonly IConfiguration configuration;
    private readonly ILogger<ScoreboardsService> logger;

    /// <summary>Creates the service with persistence and authorization dependencies.</summary>
    public ScoreboardsService(
        ScoreboardRepository scoreboards,
        GameCredentialRepository credentials,
        AccessTokenService tokens,
        IConfiguration configuration,
        ILogger<ScoreboardsService> logger)
    {
        this.scoreboards = scoreboards;
        this.credentials = credentials;
        this.tokens = tokens;
        this.configuration = configuration;
        this.logger = logger;
    }

    /// <summary>Handles GET /api/scoreboards. Leaderboard reads remain public.</summary>
    [Function("GetAllScoreboards")]
    public async Task<IActionResult> GetAllScoreboards(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scoreboards")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var boards = await scoreboards.GetAllAsync(cancellationToken);
        if (boards.Count == 0) return new NotFoundResult();

        var factory = new ScoreboardFactory();
        var response = boards.Select(factory.CreateScoreboardDto).ToArray();
        return new OkObjectResult(JsonConvert.SerializeObject(response));
    }

    /// <summary>Handles POST /api/scoreboards. A Functions key is required for operator-only board creation.</summary>
    [Function("CreateScoreboard")]
    public async Task<IActionResult> CreateScoreboardAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "scoreboards")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        CreateScoreboardRequest? body;
        try
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
            body = JsonConvert.DeserializeObject<CreateScoreboardRequest>(requestBody);
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }

        if (body is null || body.ScoreboardId == Guid.Empty ||
            body.MaximumScores <= 0 || string.IsNullOrWhiteSpace(body.Name))
            return new BadRequestResult();

        await scoreboards.CreateAsync(new ScoreboardFactory().CreateScoreboard(body), cancellationToken);
        return new OkResult();
    }

    /// <summary>Handles GET /api/scoreboards/{id}. Reads do not require authentication.</summary>
    [Function("GetScoreboardById")]
    public async Task<IActionResult> GetScoreboardById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scoreboards/{id}")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetBoardId(request, out var boardId)) return new NotFoundResult();

        var scoreboard = await scoreboards.GetByIdAsync(boardId, cancellationToken);
        if (scoreboard is null) return new NotFoundResult();

        var response = new ScoreboardFactory().CreateScoreboardDto(scoreboard);
        return new OkObjectResult(JsonConvert.SerializeObject(response));
    }

    /// <summary>
    /// Handles PUT /api/scoreboards/{id}. Enforcement is staged behind SCORE_SUBMISSION_AUTH_REQUIRED
    /// so game backends can migrate before anonymous writes are disabled.
    /// </summary>
    [Function("PutNewScoreEntry")]
    public async Task<IActionResult> PutNewScoreEntryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "scoreboards/{id}")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetBoardId(request, out var boardId)) return new NotFoundResult();

        if (bool.TryParse(configuration["SCORE_SUBMISSION_AUTH_REQUIRED"], out var authRequired) && authRequired)
        {
            var authorizationResult = await AuthorizeScoreSubmissionAsync(request, boardId, cancellationToken);
            if (authorizationResult is not null) return authorizationResult;
        }

        EntryDto? entry;
        try
        {
            var requestBody = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
            entry = JsonConvert.DeserializeObject<EntryDto>(requestBody);
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }

        if (entry is null || string.IsNullOrWhiteSpace(entry.PlayerName) || entry.Score < 0)
            return new BadRequestResult();

        var result = await scoreboards.SubmitScoreAsync(boardId, entry.PlayerName, entry.Score, cancellationToken);
        return result switch
        {
            ScoreSubmissionResult.Accepted => new OkResult(),
            ScoreSubmissionResult.BoardNotFound => new NotFoundResult(),
            ScoreSubmissionResult.NotQualified => new ConflictResult(),
            ScoreSubmissionResult.ConcurrentUpdate => new ConflictResult(),
            _ => new StatusCodeResult(StatusCodes.Status500InternalServerError)
        };
    }

    private async Task<IActionResult?> AuthorizeScoreSubmissionAsync(
        HttpRequest request, Guid boardId, CancellationToken cancellationToken)
    {
        var authorization = request.Headers["Authorization"].ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return new UnauthorizedResult();

        var rawToken = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(rawToken)) return new UnauthorizedResult();

        AccessTokenClaims claims;
        try
        {
            claims = tokens.Validate(rawToken);
        }
        catch (InvalidOperationException)
        {
            logger.LogError("Scoreboardly token validation is not configured correctly.");
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception) when (
            exception is SecurityTokenException or ArgumentException or FormatException)
        {
            return new UnauthorizedResult();
        }

        if (!Guid.TryParse(claims.BoardId, out var tokenBoardId)) return new UnauthorizedResult();

        if (tokenBoardId != boardId ||
            !claims.Scopes.Contains(GameCredentialSecurity.ScoreSubmitScope, StringComparer.Ordinal))
            return new StatusCodeResult(StatusCodes.Status403Forbidden);

        var credential = await credentials.GetByIdAsync(claims.CredentialId, cancellationToken);
        if (credential is null || credential.RevokedAtUtc is not null)
            return new UnauthorizedResult();

        if (!string.Equals(credential.GameId, claims.GameId, StringComparison.Ordinal))
            return new UnauthorizedResult();

        if (!GameCredentialSecurity.CanSubmitToBoard(credential, boardId))
            return new StatusCodeResult(StatusCodes.Status403Forbidden);

        return null;
    }

    private static bool TryGetBoardId(HttpRequest request, out Guid boardId)
    {
        var value = request.RouteValues.TryGetValue("id", out var routeValue)
            ? routeValue?.ToString()
            : null;
        return Guid.TryParse(value, out boardId);
    }
}
