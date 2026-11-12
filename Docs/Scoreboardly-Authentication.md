# Scoreboardly authentication and service migration

This document describes the server-side credential and token flow. JavaScript clients receive only short-lived bearer tokens. Permanent game credentials and the JWT signing key remain on trusted servers.

## Hosting prerequisite

The Functions app is migrated to .NET 10 and the Azure Functions isolated worker model. Before deploying this build, update the Azure Function App configuration:

- Set FUNCTIONS_WORKER_RUNTIME to dotnet-isolated.
- Keep FUNCTIONS_EXTENSION_VERSION set to ~4.
- Confirm the operating system and hosting plan support .NET 10. .NET 10 is not supported on Linux Consumption; use a supported plan such as Flex Consumption for Linux.
- Restart the Function App after changing runtime settings.

The production deployment workflow is manual during this migration. It requires an explicit runtime-configuration confirmation and only deploys from main. Do not dispatch it until the Function App has been updated and verified. A mismatch between the deployed isolated-worker package and the in-process dotnet setting prevents the app from starting. Prefer a staging slot when one is available, then verify public leaderboard reads and authenticated test writes before switching production traffic.

## Required Cosmos DB resources

The existing database is Scoreboardly. Preserve the existing Scoreboards container and its /id partition key.

Create one additional container:

| Setting | Value |
| --- | --- |
| Database | Scoreboardly |
| Container | GameCredentials |
| Partition key | /id |

Credential records contain a random salt and a one-way hash. The raw credential is never written to Cosmos DB.

## Application settings

Configure these as Azure Function App settings, preferably with Key Vault references for secrets:

| Setting | Required | Description |
| --- | --- | --- |
| COSMOS_DB_CONNECTION_STRING | Yes | Existing Cosmos DB connection string. |
| SCOREBOARDLY_TOKEN_SIGNING_KEY | Yes for token issuance/enforcement | Base64-encoded random key containing at least 32 bytes. |
| SCOREBOARDLY_TOKEN_ISSUER | No | JWT issuer. Defaults to scoreboardly. |
| SCOREBOARDLY_TOKEN_AUDIENCE | No | JWT audience. Defaults to scoreboardly-api. |
| SCORE_SUBMISSION_AUTH_REQUIRED | No | Defaults to false. Set to true only after all game backends that submit scores have been migrated to the token broker. |

Generate a signing key outside the repository and store it in a secret manager:

~~~bash
openssl rand -base64 32
~~~

Never commit the generated value, add it to client configuration, or include it in logs. Keep the same signing key across all instances of one environment. Key rotation requires a planned overlap strategy and is not yet automated.

## Credential lifecycle

Credential administration endpoints require an Azure Functions function key. Call them only from an operator-controlled environment. Do not expose these endpoints or their keys to browser clients.

### Provision a credential

POST /api/admin/game-credentials

Headers:

- x-functions-key: operator function key
- Content-Type: application/json

Body:

~~~json
{
  "gameId": "yeti-mountain",
  "allowedBoards": ["00000000-0000-0000-0000-000000000001"],
  "scopes": ["scores:submit"]
}
~~~

The service returns a credentialId and raw credential exactly once. Store the raw credential in the game's server-side secret configuration. The service persists only a salted SHA-256 hash of the 256-bit random secret. If the raw credential is lost, rotate it; it cannot be retrieved.

Each game should have its own credential. The board allowlist and scopes are enforced at token issuance and again when a token is used for a score write.

### Rotate a credential

POST /api/admin/game-credentials/{credentialId}/rotate

This creates a new independent credential with the same game identity, board allowlist, and scopes. The old credential remains valid so the game's backend can switch without downtime. Once the new credential is deployed, revoke the previous one.

### Revoke a credential

DELETE /api/admin/game-credentials/{credentialId}

Revocation is rechecked on every protected score write by reading the credential record. A request that observes the revoked record rejects tokens previously issued to that credential. The propagation window follows the Cosmos DB account's configured consistency level.

## Trusted-backend token exchange

A game's trusted backend calls:

POST /api/auth/token

Headers:

- Authorization: Bearer followed by the private game credential
- Content-Type: application/json

Body:

~~~json
{
  "boardId": "00000000-0000-0000-0000-000000000001"
}
~~~

The service checks the credential hash, active status, board allowlist, scope, and board existence. It returns:

~~~json
{
  "accessToken": "short-lived-signed-token",
  "tokenType": "Bearer",
  "expiresIn": 600
}
~~~

The JWT is signed with HMAC-SHA256 and includes issuer, audience, game identity, board identifier, scores:submit scope, issued-at, not-before, expiration, and token ID claims. The initial lifetime is ten minutes.

The credential must be sent in the Authorization header, never in a query string or request body. Ensure reverse proxies and diagnostic tooling do not capture authorization headers. Do not log the raw credential or access token.

## Score submission authorization

The browser receives only the short-lived token from the game's own same-origin token-broker endpoint. The existing SDK accepts that token through its AuthProvider and attaches it to:

PUT /api/scoreboards/{boardId}

When SCORE_SUBMISSION_AUTH_REQUIRED=true, the service validates:

- JWT signature and HMAC-SHA256 algorithm.
- Issuer, audience, not-before, and expiration.
- Required game, credential, board, scope, and token ID claims.
- Match between route board and token board.
- Match between token game identity and the current credential record.
- Current credential status, allowlisted board, and scores:submit scope.

Missing, malformed, invalid, or expired tokens return 401. A valid token that does not authorize the requested board or scope returns 403. Qualification conflicts remain 409. Public GET leaderboard routes stay anonymous.

### Staged rollout

For compatibility, SCORE_SUBMISSION_AUTH_REQUIRED defaults to false. In this mode the API continues to accept legacy anonymous score writes. This is a migration mode, not a secured production state.

1. Create the GameCredentials container and configure the signing key.
2. Provision one credential for each game/backend.
3. Deploy a same-origin token broker in each game backend. The browser broker calls POST /api/auth/token using the private credential and returns only the short-lived access token.
4. Update each browser game to use the broker through the SDK AuthProvider.
5. Test each game against an isolated board and verify missing, expired, wrong-board, and revoked-token failures.
6. Set SCORE_SUBMISSION_AUTH_REQUIRED=true only after all active score-writing clients have migrated.

Do not claim score submissions are protected while the flag is false. The admin credential endpoints are function-key protected and board creation now requires a Functions key.

## Security limits

A valid token authorizes a score-write request; it does not prove that gameplay was legitimate. Players still control browser JavaScript and can alter local game state. Server-side gameplay verification, score plausibility checks, rate limiting, abuse monitoring, and idempotency/replay handling are separate follow-up work. A leaked game credential is limited to that game's configured boards and can be revoked, but it remains a server-side secret and must be stored accordingly.
