using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Scoreboardly.Application.Functions.Api;

/// <summary>
/// Defines the scoreboard API DTO.
/// </summary>
public class ScoreboardDto
{
    /// <summary>
    /// Gets or sets the scoreboard identifier.
    /// </summary>
    [JsonProperty("id")]
    public Guid Id { get; set; }

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
    /// Gets or sets the collection of high scores.
    /// </summary>
    [JsonProperty("scores")]
    public IEnumerable<EntryDto> Scores { get; set; }
}