# Scoreboardly

Scoreboardly is a REST API for storing and retrieving game high scores. It runs in Azure using Azure Functions and Cosmos DB.

## Prerequisites

To build and run the project locally, install:

- .NET 10 SDK
- Azure Functions Core Tools v4
- Node.js 22.12 or newer and npm 10 or newer (for the SDK workspace)
- Cosmos DB Emulator or an isolated Cosmos DB development account

## Cosmos DB configuration

Create a database called Scoreboardly with the existing Scoreboards container and its `/id` partition key. The isolated-worker migration preserves the existing document serialization shape.

Configure local.settings.json with COSMOS_DB_CONNECTION_STRING and FUNCTIONS_WORKER_RUNTIME=dotnet-isolated.

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

The Functions app uses the .NET 10 isolated worker model. Before deploying, configure the Azure Function App for `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`, keep `FUNCTIONS_EXTENSION_VERSION=~4`, and verify the hosting plan supports .NET 10. See [Hosting migration](Docs/Hosting-Migration.md).

## Endpoints

- Get all scoreboards: GET /api/scoreboards
- Get scoreboard by ID: GET /api/scoreboards/:id
- Create scoreboard: POST /api/scoreboards (Functions key required)
- Add scoreboard entry: PUT /api/scoreboards/:id

## JavaScript SDK workspace

The repository includes a framework-independent TypeScript SDK and a local browser harness. The harness uses the SDK package against a deterministic mock API; it does not contact production Scoreboardly.

Run the browser harness with:

~~~bash
npm run dev:harness
~~~

## Current score-write security status

This hosting migration preserves compatibility: score writes remain anonymous in this PR. Per-game credentials and scoped score-write authorization are handled in the dependent authentication pull request. Do not treat this migration by itself as securing score submissions.

## Contributing

Keep pull requests focused, update documentation with behavior changes, preserve useful comments and rationale, and add tests for changed contracts. See AGENTS.md for repository guidance.
