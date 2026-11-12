using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using Scoreboardly.Application.Functions.Api;
using Scoreboardly.Application.Functions.Infrastructure;

namespace Scoreboardly.Application.Functions;

/// <summary>HTTP endpoints for public leaderboard reads and score submissions.</summary>
public sealed class ScoreboardsService
{
    private readonly ScoreboardRepository scoreboards;

    /// <summary>Creates the service with its persistence dependency.</summary>
    public ScoreboardsService(ScoreboardRepository scoreboards)
    {
        this.scoreboards = scoreboards;
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
    /// Handles PUT /api/scoreboards/{id}. Score writes remain anonymous in this hosting migration;
    /// per-game authorization is implemented in the dependent authentication change.
    /// </summary>
    [Function("PutNewScoreEntry")]
    public async Task<IActionResult> PutNewScoreEntryAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "scoreboards/{id}")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetBoardId(request, out var boardId)) return new NotFoundResult();

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

    private static bool TryGetBoardId(HttpRequest request, out Guid boardId)
    {
        var value = request.RouteValues.TryGetValue("id", out var routeValue)
            ? routeValue?.ToString()
            : null;
        return Guid.TryParse(value, out boardId);
    }
}
