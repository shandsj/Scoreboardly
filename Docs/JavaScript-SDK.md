# JavaScript SDK and browser harness guide

This guide describes the current SDK vertical slice and how to extend it safely. It distinguishes implemented behavior from planned work.

## Package boundaries

- packages/scoreboardly-sdk contains the framework-independent client. It must not depend on Phaser, browser UI components, or the harness.
- apps/browser-harness imports the SDK and exercises it against a deterministic local mock API.
- tests contains automated SDK tests. Tests should exercise the public contract rather than duplicate private implementation details.
- The .NET Azure Functions service remains the authoritative API and deployment target. SDK workspace CI is separate from .NET service CI and deployment.

Keep the package usable from ordinary JavaScript/TypeScript games, not just Phaser games.

## Request flow

### Leaderboard read

1. The game calls client.getLeaderboard().
2. The SDK sends GET {baseUrl}/scoreboards/{boardId}.
3. No access token is requested for this public read.
4. The SDK parses the response, including the legacy JSON-string envelope emitted by the .NET API.
5. Required board fields are validated and malformed individual score entries are filtered.

### Score submission

1. The game calls client.submitScore({ playerName, score }).
2. The SDK calls the configured AuthProvider.getAccessToken().
3. The game's same-origin backend authenticates its caller and requests a short-lived token using its server-side private game credential.
4. The trusted backend sends POST {baseUrl}/auth/token with Authorization: Bearer followed by the private game credential and a JSON body containing boardId.
5. Scoreboardly validates the salted credential hash, active status, board allowlist, and scores:submit scope. It returns a ten-minute signed bearer token.
6. The game's backend returns only that short-lived token to the browser.
7. The SDK sends PUT {baseUrl}/scoreboards/{boardId} with Authorization: Bearer followed by the short-lived token.
8. The API response determines whether the promise resolves or rejects. A request being sent is never represented as an accepted score.

The SDK does not store or issue credentials. The permanent credential and token signing key must never ship in browser code. Board IDs are public identifiers, not security boundaries. Service-side enforcement is controlled by SCORE_SUBMISSION_AUTH_REQUIRED during migration; score writes are not protected until that setting is true.

## Production token-broker request

The following request is sent by the trusted game backend, not by browser code:

~~~http
POST /api/auth/token HTTP/1.1
Host: scoreboardly.example
Authorization: Bearer <private-game-credential>
Content-Type: application/json

{
  "boardId": "00000000-0000-0000-0000-000000000001"
}
~~~

The backend should authenticate its caller, enforce the board/session rules for that game, keep the private credential in server-side configuration, and return only the accessToken field to the browser. The browser's SDK AuthProvider calls the backend's same-origin endpoint, not Scoreboardly's credential exchange directly.

## Browser harness

Start from the repository root:

~~~bash
npm install
npm run dev:harness
~~~

The Vite server binds to 127.0.0.1:5173. Mock API endpoints are registered as development-server middleware in apps/browser-harness/vite.config.ts. They are not Azure Functions and are not included in the production service.

| Control | Behavior |
| --- | --- |
| API availability | Mock API/token requests return HTTP 503 when unavailable. |
| Simulated latency | Delays mock API and token responses from 0 to 2500 ms. |
| Valid token | Token scoped to harness-board with score-submit permission. |
| Missing token | Provider returns null; SDK must not send the score request. |
| Expired token | Token is rejected with HTTP 401. |
| Wrong board | Token for another board; mock API responds HTTP 403. |
| Insufficient scope | Read-only token; mock API responds HTTP 403 to submission. |
| Accept score | Adds the submission to the in-memory mock leaderboard. |
| Qualification conflict | Returns HTTP 409 without changing the board. |
| Server error | Returns HTTP 500 without changing the board. |
| Reset | Restores initial scores and default scenario values. |

Mock token values are fixed test fixtures. They are not signed, are not accepted by the real service, and must never be copied into a real game. Mock control endpoints are unauthenticated because the server binds to loopback for local development only.

## Tests and validation

Run from the repository root:

~~~bash
npm install
npm test
npm run typecheck
npm run build
dotnet test Scoreboardly.sln --configuration Release
~~~

- Vitest runs SDK tests in Node.
- MSW intercepts HTTP requests so SDK tests do not depend on the real API or network.
- The .NET test project validates game credential hashing/permissions, JWT issuance/validation, expiry, audience, and Cosmos document serialization compatibility.
- The harness is type-checked separately and built after the SDK package.
- Never use a production board or real credentials in CI.

## Documentation expectations

- Add TSDoc/JSDoc to every exported interface, type, class, public member, and function.
- Document each public property and method, including parameters, return values, errors, side effects, and lifecycle constraints where relevant.
- Add concise comments to private helpers when they enforce an invariant or handle compatibility behavior.
- Update this guide and the package README when public behavior or auth flow changes.
- Keep examples copyable and label application-owned endpoints as such.

## Planned work, not implemented in this slice

- Same-origin token brokers in each consuming game.
- Service-side rate limiting, score plausibility checks, gameplay verification, and anti-cheat.
- Server-side idempotency and replay-safe offline submissions.
- Leaderboard caching and stale-cache metadata.
- Durable offline submission queue, retry/backoff, expiry, and explicit accepted/rejected states.
- Integration tests against an isolated live test service environment.
- Package publishing and consumer installation documentation.
