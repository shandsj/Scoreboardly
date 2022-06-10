using System.Linq;
using Scoreboardly.Application.Functions.Api;
using Scoreboardly.Domain;

namespace Scoreboardly.Application.Functions;

/// <summary>
/// Provides an implementation of the <see cref="IScoreboardFactory" interface.
/// </summary>
public class ScoreboardFactory : IScoreboardFactory
{
    /// <inheritdoc />
    public Scoreboard CreateScoreboard(CreateScoreboardRequest source)
    {
        return new Scoreboard(source.ScoreboardId)
        {
            Name = source.Name,
            MaximumScores = source.MaximumScores,
        };
    }

    /// <inheritdoc />
    public ScoreboardDto CreateScoreboardDto(Scoreboard source)
    {
        return new ScoreboardDto()
        {
            Id = source.Id,
            Name = source.Name,
            MaximumScores = source.MaximumScores,
            Scores = source.Scores.Select(CreateEntryDto).ToArray(),
        };
    }

    /// <inheritdoc />
    public EntryDto CreateEntryDto(Entry source)
    {
        return new EntryDto()
        {
            PlayerName = source.PlayerName,
            Score = source.Score,
        };
    }
}