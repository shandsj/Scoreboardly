using Scoreboardly.Application.Functions.Api;
using Scoreboardly.Domain;

namespace Scoreboardly.Application.Functions;

/// <summary>
/// Defines an interface for a factory that creates scoreboard related objects.
/// </summary>
public interface IScoreboardFactory
{
    /// <summary>
    /// Creates a <see cref="Scoreboard" /> from the specified <see cref="CreateScoreboardRequest" />.
    /// </summary>
    /// <param name="source">The source request.</param>
    /// <returns>The created scoreboard.</returns>
    Scoreboard CreateScoreboard(CreateScoreboardRequest source);
}
