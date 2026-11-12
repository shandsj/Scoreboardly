using System.Net;
using Microsoft.Azure.Cosmos;
using Scoreboardly.Domain;

namespace Scoreboardly.Application.Functions.Infrastructure;

/// <summary>
/// Performs Cosmos DB operations for scoreboard documents.
/// </summary>
public sealed class ScoreboardRepository
{
    private readonly Container container;

    /// <summary>
    /// Initializes the repository using the existing Scoreboards container.
    /// </summary>
    public ScoreboardRepository(CosmosClient client)
    {
        container = client.GetContainer("Scoreboardly", "Scoreboards");
    }

    /// <summary>
    /// Reads all public scoreboards.
    /// </summary>
    public async Task<IReadOnlyList<Scoreboard>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<Scoreboard>();
        using var iterator = container.GetItemQueryIterator<Scoreboard>(
            new QueryDefinition("SELECT * FROM c"));

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(page);
        }

        return results;
    }

    /// <summary>
    /// Reads a scoreboard by its identifier, returning null when it does not exist.
    /// </summary>
    public async Task<Scoreboard?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.ReadItemAsync<Scoreboard>(
                id.ToString(),
                new PartitionKey(id.ToString()),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a scoreboard document.
    /// </summary>
    public async Task CreateAsync(Scoreboard scoreboard, CancellationToken cancellationToken = default)
    {
        await container.CreateItemAsync(
            scoreboard,
            new PartitionKey(scoreboard.Id.ToString()),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Adds a score using ETag-based optimistic concurrency so simultaneous submissions do not overwrite each other.
    /// </summary>
    public async Task<ScoreSubmissionResult> SubmitScoreAsync(
        Guid scoreboardId,
        string playerName,
        int score,
        CancellationToken cancellationToken = default)
    {
        const int maximumAttempts = 3;

        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            ItemResponse<Scoreboard> response;
            try
            {
                response = await container.ReadItemAsync<Scoreboard>(
                    scoreboardId.ToString(),
                    new PartitionKey(scoreboardId.ToString()),
                    cancellationToken: cancellationToken);
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return ScoreSubmissionResult.BoardNotFound;
            }

            var scoreboard = response.Resource;
            if (!scoreboard.EnterScore(playerName, score))
            {
                return ScoreSubmissionResult.NotQualified;
            }

            try
            {
                await container.ReplaceItemAsync(
                    scoreboard,
                    scoreboard.Id.ToString(),
                    new PartitionKey(scoreboard.Id.ToString()),
                    new ItemRequestOptions { IfMatchEtag = response.ETag },
                    cancellationToken);

                return ScoreSubmissionResult.Accepted;
            }
            catch (CosmosException exception) when (
                exception.StatusCode == HttpStatusCode.PreconditionFailed &&
                attempt < maximumAttempts - 1)
            {
                // Another request changed this board after our read; re-read and apply the score again.
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                return ScoreSubmissionResult.ConcurrentUpdate;
            }
        }

        return ScoreSubmissionResult.ConcurrentUpdate;
    }
}

/// <summary>
/// Result of an authoritative score submission.
/// </summary>
public enum ScoreSubmissionResult
{
    /// <summary>The score was persisted.</summary>
    Accepted,
    /// <summary>The requested board does not exist.</summary>
    BoardNotFound,
    /// <summary>The score did not qualify for the board.</summary>
    NotQualified,
    /// <summary>The board was concurrently updated too many times.</summary>
    ConcurrentUpdate
}
