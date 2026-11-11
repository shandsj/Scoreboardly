# Scoreboardly JavaScript SDK

The SDK is a framework-independent TypeScript/JavaScript client for the Scoreboardly REST API. It is currently an internal workspace package and is not published. Its public API is still evolving.

## Current capabilities

- Read the public leaderboard for one configured board.
- Submit a score using a short-lived bearer token supplied by the host application's authentication provider.
- Normalize common HTTP failures into ScoreboardlyError.
- Parse ordinary JSON and the legacy JSON-string response envelope currently emitted by the .NET API.

## Not implemented yet

This initial slice does not provide caching, offline queues, retries, token refresh, idempotency keys, or client-side score validation. A successful submitScore promise means the API returned a successful HTTP status. It does not prove that a client-generated score is honest; authoritative validation belongs on the server.

## Quick start

~~~ts
import { createScoreboardly } from "@scoreboardly/sdk";

const client = createScoreboardly({
  baseUrl: "https://scores.example.com/api",
  scoreboardId: "public-board-id",
  auth: {
    async getAccessToken() {
      const response = await fetch("/api/scoreboard-token", {
        method: "POST",
        credentials: "same-origin"
      });
      if (!response.ok) throw new Error(`Token service failed: ${response.status}`);
      const result: { accessToken: string | null } = await response.json();
      return result.accessToken;
    }
  }
});

const board = await client.getLeaderboard();
await client.submitScore({ playerName: "Yeti", score: 12500 });
~~~

The example token route is owned by the consuming game/backend. It is not implemented by this SDK or the current Scoreboardly API. It must authenticate the game/session, authorize the board and operation, and return a short-lived token. Never put a permanent service credential, signing key, or privileged API key in browser code.

## Public API

### createScoreboardly(options)

| Option | Required | Meaning |
| --- | --- | --- |
| baseUrl | Yes | API root including /api. The SDK appends /scoreboards/{id}. |
| scoreboardId | Yes | Public board identifier. It is not a secret. |
| auth | Yes | Provider used to obtain a token for each score submission. |
| fetch | No | Fetch implementation override for tests or alternate runtimes. |

The factory throws TypeError when the base URL, board identifier, or fetch implementation is missing or invalid.

### client.getLeaderboard()

- Sends an unauthenticated GET /scoreboards/{id}.
- Resolves with a Scoreboard object.
- Does not currently cache results.
- Rejects with ScoreboardlyError for network, HTTP, or malformed-response failures.

### client.submitScore(submission)

- Calls auth.getAccessToken() before sending a request.
- Sends PUT /scoreboards/{id} with an Authorization: Bearer header.
- Resolves only when the API responds with a successful status.
- Does not currently retry, queue, or persist a failed submission.
- A missing token rejects locally and prevents the score request.

### Authentication boundary

The SDK does not mint or refresh credentials. A browser game should call its own same-origin backend. That backend authenticates the game/session, checks board and operation permissions, uses its server-side credential to obtain or mint a short-lived scoped token, and returns that token to the SDK. Scoreboardly must validate token signature, expiration, board, scope, and request.

A board ID is an identifier, not a secret. Hiding it does not secure a board. Browser code is controlled by the player, so a shared secret embedded in the game can be extracted and replayed. Server-side score validation, rate limits, and abuse controls remain necessary.

### Error handling

Catch ScoreboardlyError and branch on its code. The optional status property contains the HTTP status when one was received.

| Code | Meaning |
| --- | --- |
| network | Fetch rejected or connection failed. |
| unauthorized | Token missing, invalid, or expired (HTTP 401). |
| forbidden | Token lacks board or operation permission (HTTP 403). |
| conflict | Score did not qualify (HTTP 409). Do not blindly retry. |
| http | Another non-success HTTP response. |
| invalid_response | Response body does not satisfy the expected contract. |

Errors thrown directly by the host AuthProvider currently propagate unchanged.

## Compatibility notes

- baseUrl must include the API path, normally /api.
- Current wire operations are GET /scoreboards/:id and PUT /scoreboards/:id.
- The parser accepts the .NET API's legacy JSON-string envelope and a normal JSON object.
- Malformed individual score entries are filtered, but exhaustive runtime validation of every field is not yet implemented.
- Build the SDK workspace before consuming its generated dist entry point.

See ../../Docs/JavaScript-SDK.md for architecture, harness scenarios, tests, and planned follow-up work.