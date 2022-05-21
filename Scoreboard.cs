
using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Scoreboardly;

public class Scoreboard
{
    [JsonProperty("scores")]
    private List<Entry> scores = new List<Entry>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Scoreboard" /> class.
    /// </summary>
    /// <param name="id">The scoreboard identifier.</param>
    public Scoreboard(Guid id)
    {
        this.Id = id;
    }

    /// <summary>
    /// Gets the scoreboard identifier.
    /// </summary>
    [JsonProperty("id")]
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the scoreboard name.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of scores on the scoreboard.
    /// </summary>
    [JsonProperty("maximumScores")]
    public int MaximumScores { get; set; }

    /// <summary>
    /// Gets the collection of high scores.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<Entry> Scores => this.scores.ToArray();

    /// <summary>
    /// Gets the highest entry on the scoreboard.
    /// </summary>
    [JsonIgnore]
    public Entry HighestEntry => this.scores.Any() ? this.scores.First() : Entry.Empty;

    /// <summary>
    /// Gets the lowest entry on the scoreboard.
    /// </summary>
    [JsonIgnore]
    public Entry LowestEntry => this.scores.Any() ? this.scores.Last() : Entry.Empty;

    /// <summary>
    /// Gets a value indicating whether the scoreboard is full.
    /// </summary>
    [JsonIgnore]
    public bool IsFull => this.scores.Count() >= MaximumScores;

    /// <summary>
    /// Enters the score on the scoreboard.
    /// </summary>
    /// <param name="playerName">The player's name.</param>
    /// <param name="score">The score.</param>
    /// <returns>True if the score was added to the scoreboard; false otherwise.</returns>
    public bool EnterScore(string playerName, int score)
    {
        if (score < this.LowestEntry.Score && this.IsFull)
        {
            return false;
        }

        var entry = new Entry(this.Id, playerName, score);
        this.scores.Add(entry);
        this.scores = new List<Entry>(this.scores.OrderByDescending(e => e.Score));

        if (this.scores.Count > this.MaximumScores)
        {
            this.scores.RemoveRange(this.MaximumScores, this.scores.Count - this.MaximumScores);
        }
        
        return true;
    }
}