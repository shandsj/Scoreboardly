using Scoreboardly.Application.Functions.Infrastructure;
using Scoreboardly.Domain;
using Xunit;

namespace Scoreboardly.Tests;

public sealed class CosmosSerializerCompatibilityTests
{
    [Fact]
    public void Serializer_RoundTripsExistingScoreboardDocumentShape()
    {
        var serializer = new NewtonsoftJsonCosmosSerializer();
        var board = new Scoreboard(Guid.Parse("c3d1c685-7a69-4d2f-86a0-6b2a3f0e7351"))
        {
            Name = "Compatibility Board",
            MaximumScores = 5
        };
        board.EnterScore("Ada", 900);

        using var document = serializer.ToStream(board);
        using var reader = new StreamReader(document);
        var json = reader.ReadToEnd();

        Assert.Contains(@"""scores""", json);
        Assert.Contains(@"""playerName"":""Ada""", json.Replace(" ", string.Empty));

        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var restored = serializer.FromStream<Scoreboard>(input);

        Assert.Equal(board.Id, restored.Id);
        Assert.Equal("Compatibility Board", restored.Name);
        Assert.Single(restored.Scores);
        Assert.Equal("Ada", restored.Scores.Single().PlayerName);
        Assert.Equal(900, restored.Scores.Single().Score);
    }
}
