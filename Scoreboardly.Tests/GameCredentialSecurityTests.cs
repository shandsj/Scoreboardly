using Newtonsoft.Json;
using Scoreboardly.Application.Functions.Auth;
using Xunit;

namespace Scoreboardly.Tests;

public sealed class GameCredentialSecurityTests
{
    private static readonly Guid AllowedBoard = Guid.Parse("c3d1c685-7a69-4d2f-86a0-6b2a3f0e7351");
    private static readonly Guid OtherBoard = Guid.Parse("f5c5ec7a-5b3e-476c-a9b5-14c2e7ac5a31");

    [Fact]
    public void Create_ReturnsRandomSecret_AndStoresOnlySaltedHash()
    {
        var created = CreateCredential();

        Assert.StartsWith("sb_live_", created.RawCredential, StringComparison.Ordinal);
        Assert.True(GameCredentialSecurity.TryGetCredentialId(created.RawCredential, out var id));
        Assert.Equal(created.Record.Id, id);
        Assert.DoesNotContain(created.RawCredential, JsonConvert.SerializeObject(created.Record));
        Assert.True(GameCredentialSecurity.Verify(created.Record, created.RawCredential));
    }

    [Fact]
    public void Verify_RejectsMalformedUnknownAndIncorrectSecrets()
    {
        var created = CreateCredential();
        var wrongSecret = created.RawCredential[..^1] +
            (created.RawCredential[^1] == 'a' ? "b" : "a");

        Assert.False(GameCredentialSecurity.TryGetCredentialId("not-a-credential", out _));
        Assert.False(GameCredentialSecurity.TryGetCredentialId("sb_live_bad.id", out _));
        Assert.False(GameCredentialSecurity.Verify(created.Record,
            "sb_live_00000000000000000000000000000000." + new string('a', 64)));
        Assert.False(GameCredentialSecurity.Verify(created.Record, wrongSecret));
    }

    [Fact]
    public void Verify_RejectsRevokedCredential()
    {
        var created = CreateCredential();
        created.Record.RevokedAtUtc = DateTimeOffset.UtcNow;

        Assert.False(GameCredentialSecurity.Verify(created.Record, created.RawCredential));
        Assert.False(GameCredentialSecurity.CanSubmitToBoard(created.Record, AllowedBoard));
    }

    [Fact]
    public void Credential_IsRestrictedToConfiguredBoardAndScope()
    {
        var created = CreateCredential();

        Assert.True(GameCredentialSecurity.CanSubmitToBoard(created.Record, AllowedBoard));
        Assert.False(GameCredentialSecurity.CanSubmitToBoard(created.Record, OtherBoard));

        created.Record.Scopes = ["leaderboards:read"];
        Assert.False(GameCredentialSecurity.CanSubmitToBoard(created.Record, AllowedBoard));
    }

    [Fact]
    public void Rotation_AllowsOverlapUntilOldCredentialIsRevoked()
    {
        var original = CreateCredential();
        var replacement = GameCredentialSecurity.Create(original.Record.GameId,
            original.Record.AllowedBoards, original.Record.Scopes,
            DateTimeOffset.UtcNow, original.Record.Id);

        Assert.True(GameCredentialSecurity.Verify(original.Record, original.RawCredential));
        Assert.True(GameCredentialSecurity.Verify(replacement.Record, replacement.RawCredential));
        Assert.Equal(original.Record.Id, replacement.Record.RotationOfCredentialId);

        original.Record.RevokedAtUtc = DateTimeOffset.UtcNow;
        Assert.False(GameCredentialSecurity.Verify(original.Record, original.RawCredential));
        Assert.True(GameCredentialSecurity.Verify(replacement.Record, replacement.RawCredential));
    }

    private static CreatedGameCredential CreateCredential() =>
        GameCredentialSecurity.Create("yeti-mountain", [AllowedBoard.ToString()],
            [GameCredentialSecurity.ScoreSubmitScope], DateTimeOffset.UtcNow);
}
