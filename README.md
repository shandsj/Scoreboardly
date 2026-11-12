# Scoreboardly

Scoreboardly is a REST API for storing and retrieving game high scores. It runs in Azure using Azure Functions and Cosmos DB.

## Prerequisites

To build and run the project locally, install:

- .NET 10 SDK
- Azure Functions Core Tools v4
- Node.js 22.12 or newer and npm 10 or newer (for the SDK workspace)
- Cosmos DB Emulator or an isolated Cosmos DB development account

## Cosmos DB configuration

Create a database called Scoreboardly with these containers:

| Container | Partition key | Purpose |
| --- | --- | --- |
| Scoreboards | /id | Existing public leaderboard documents |
| GameCredentials | /id | Salted credential hashes and authorization metadata |

Configure local.settings.json with COSMOS_DB_CONNECTION_STRING and FUNCTIONS_WORKER_RUNTIME=dotnet-isolated. For local token issuance, also configure a development-only SCOREBOARDLY_TOKEN_SIGNING_KEY generated with openssl rand -base64 32. Do not use production secrets in local development.

## Building and testing

~~~bash
dotnet restore Scoreboardly.sln
dotnet build Scoreboardly.sln --configuration Release
dotnet test Scoreboardly.sln --configuration Release
~~~

For the JavaScript SDK workspace:

~~~bash
npm install
npm test
npm run typecheck
npm run build
~~~

## Running the Functions app

From the repository root:

~~~bash
cd Scoreboardly.Application.Functions
func start
~~~

See Docs/Scoreboardly-Authentication.md before deploying the isolated-worker app. The Azure Function App must be configured for dotnet-isolated and a supported .NET 10 hosting plan before deployment.

## Endpoints

- Get all scoreboards: GET /api/scoreboards
- Get scoreboard by ID: GET /api/scoreboards/:id
- Create scoreboard: POST /api/scoreboards (Functions key required)
- Add scoreboard entry: PUT /api/scoreboards/:id
- Authentication and credential lifecycle: see Docs/Scoreboardly-Authentication.md

## JavaScript SDK workspace

The repository includes a framework-independent TypeScript SDK and a local browser harness. The harness uses the SDK package against a deterministic mock API; it does not contact production Scoreboardly.

Run the browser harness with:

~~~bash
npm run dev:harness
~~~

The SDK accepts a short-lived bearer token through its AuthProvider. A consuming game must implement a same-origin trusted backend endpoint that obtains the token using its own server-side Scoreboardly credential. The game credential and token signing key must never be shipped to browser code.

## Deployment and security status

The .NET service has been migrated to .NET 10 Azure Functions isolated worker. The production deployment workflow is manual until the Azure Function App has been configured for dotnet-isolated. Before dispatching it, preserve FUNCTIONS_EXTENSION_VERSION=~4, create the GameCredentials Cosmos container, and configure the signing key.

For compatibility, SCORE_SUBMISSION_AUTH_REQUIRED defaults to false. Legacy anonymous score writes remain accepted until every active game has a trusted token broker and the setting is explicitly enabled. Do not treat the production score-write endpoint as protected until enforcement is enabled and tested.

Valid access tokens authorize a request, not the legitimacy of the gameplay. Server-side score verification, rate limiting, and replay protection remain separate security work.

## Contributing

Keep pull requests focused, update documentation with behavior changes, preserve useful comments and rationale, and add tests for changed contracts. See AGENTS.md for repository guidance.

## License

MIT © Jason Shands
