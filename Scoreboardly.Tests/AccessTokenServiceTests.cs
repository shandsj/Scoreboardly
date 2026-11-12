using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Scoreboardly.Application.Functions.Auth;
using Xunit;

namespace Scoreboardly.Tests;

public sealed class AccessTokenServiceTests
{
    private static readonly Guid BoardId = Guid.Parse("c3d1c685-7a69-4d2f-86a0-6b2a3f0e7351");

    [Fact]
    public void Issue_ProducesTenMinuteTokenWithBoardAndGameScope()
    {
        var now = DateTimeOffset.UtcNow;
        var credential = CreateCredential();
        var service = CreateService();

        var token = service.Issue(credential, BoardId, now);
        var claims = service.Validate(token);

        Assert.Equal(credential.Id, claims.CredentialId);
        Assert.Equal(credential.GameId, claims.GameId);
        Assert.Equal(BoardId.ToString(), claims.BoardId);
        Assert.Contains(GameCredentialSecurity.ScoreSubmitScope, claims.Scopes);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.InRange(jwt.ValidTo - now.UtcDateTime, TimeSpan.FromMinutes(9.9), TimeSpan.FromMinutes(10.1));
    }

    [Fact]
    public void Validate_RejectsExpiredToken()
    {
        var service = CreateService();
        var token = service.Issue(CreateCredential(), BoardId, DateTimeOffset.UtcNow.AddMinutes(-20));

        Assert.ThrowsAny<SecurityTokenException>(() => service.Validate(token));
    }

    [Fact]
    public void Validate_RejectsTamperedToken()
    {
        var service = CreateService();
        var token = service.Issue(CreateCredential(), BoardId, DateTimeOffset.UtcNow);
        var segments = token.Split('.');
        segments[2] = (segments[2][0] == 'a' ? "b" : "a") + segments[2][1..];
        var tampered = string.Join('.', segments);

        Assert.ThrowsAny<SecurityTokenException>(() => service.Validate(tampered));
    }

    [Fact]
    public void Validate_RejectsTokenSignedForDifferentAudience()
    {
        var credential = CreateCredential();
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var issuedBy = CreateService("game-api", key);
        var validatedBy = CreateService("other-api", key);
        var token = issuedBy.Issue(credential, BoardId, DateTimeOffset.UtcNow);

        Assert.ThrowsAny<SecurityTokenException>(() => validatedBy.Validate(token));
    }

    [Fact]
    public void Issue_RejectsBoardOutsideCredentialAllowlist()
    {
        var service = CreateService();
        Assert.Throws<InvalidOperationException>(() =>
            service.Issue(CreateCredential(), Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    private static GameCredential CreateCredential() =>
        GameCredentialSecurity.Create("test-game", [BoardId.ToString()],
            [GameCredentialSecurity.ScoreSubmitScope], DateTimeOffset.UtcNow).Record;

    private static AccessTokenService CreateService(
        string audience = "scoreboardly-api",
        string? key = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SCOREBOARDLY_TOKEN_SIGNING_KEY"] = key ??
                    Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ["SCOREBOARDLY_TOKEN_ISSUER"] = "scoreboardly-test",
                ["SCOREBOARDLY_TOKEN_AUDIENCE"] = audience
            })
            .Build();

        return new AccessTokenService(configuration);
    }
}