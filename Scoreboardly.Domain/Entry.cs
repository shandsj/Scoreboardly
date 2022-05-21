using Newtonsoft.Json;

namespace Scoreboardly.Domain;

/// <summary>
/// Defines a value object that represents an entry on the scoreboard.
/// </summary>
public class Entry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Entry" /> class.
    /// </summary>
    /// <param name="scoreboardId">The parent scoreboard identifier.</param>
    public Entry(Guid scoreboardId, string playerName, int score)
    {
        this.ScoreboardId = scoreboardId;
        this.PlayerName = playerName;
        this.Score = score;
    }

    /// <summary>
    /// Gets the empty entry value.
    /// </summary>
    public static Entry Empty => new Entry(Guid.Empty, string.Empty, 0);

    /// <summary>
    /// Gets the parent scoreboard identifier.
    /// </summary>
    [JsonProperty("scoreboardId")]
    public Guid ScoreboardId { get; }

    /// <summary>
    /// Gets the player name.
    /// </summary>
    [JsonProperty("playerName")]
    public string PlayerName { get; }

    /// <summary>
    /// Gets the score.
    /// </summary>
    [JsonProperty("score")]
    public int Score { get; }
}