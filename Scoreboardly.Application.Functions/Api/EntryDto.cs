using System;
using Newtonsoft.Json;

namespace Scoreboardly.Application.Functions.Api;

/// <summary>
/// Defines the entry API DTO
/// </summary>
public class EntryDto
{
    /// <summary>
    /// Gets or sets the player name.
    /// </summary>
    [JsonProperty("playerName")]
    public string PlayerName { get; set; }

    /// <summary>
    /// Gets or sets the score.
    /// </summary>
    [JsonProperty("score")]
    public int Score { get; set; }
}