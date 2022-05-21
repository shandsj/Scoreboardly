using System;
using Newtonsoft.Json;

namespace Scoreboardly.Application.Functions.Api;

/// <summary>
/// Defines a request sent to create a scoreboard.
/// </summary>
public class CreateScoreboardRequest
{
    /// <summary>
    /// Gets the scoreboard identifier.
    /// </summary>
    [JsonProperty("scoreboardId", Required = Required.Always)]
    public Guid ScoreboardId { get; set; }

    /// <summary>
    /// Gets or sets the scoreboard name.
    /// </summary>
    [JsonProperty("name", Required = Required.Always)]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of scores on the scoreboard.
    /// </summary>
    [JsonProperty("maximumScores", Required = Required.Always)]
    public int MaximumScores { get; set; }
}