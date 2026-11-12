# Scoreboardly staging environment

This staging environment is intentionally separate from production. Do not point it at the production Cosmos DB account, database, container, storage account, or Function App.

## Azure resources

Create a dedicated Azure Function App named something like `scoreboardly-staging` and configure it for:

- Azure Functions runtime: `~4`
- Worker runtime: `dotnet-isolated`
- .NET isolated worker version: `10.0`
- A .NET 10-compatible hosting plan. .NET 10 is not supported on Linux Consumption. Flex Consumption is suitable for .NET 10 but does not support deployment slots.
- A dedicated staging storage account.
- A dedicated staging Cosmos DB account or, at minimum, a separate staging database/container with credentials that cannot write to production.
- Application Insights for staging diagnostics.

The Function App must have these application settings:

| Setting | Value |
| --- | --- |
| `FUNCTIONS_EXTENSION_VERSION` | `~4` |
| `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
| `COSMOS_DB_CONNECTION_STRING` | Staging-only Cosmos DB connection string |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Staging Application Insights connection string, if using a dedicated resource |

Create the Cosmos DB database and container expected by the application: database `Scoreboardly`, container `Scoreboards`, partition key `/id`. Seed it with sanitized copies of representative legacy scoreboard documents if you want to test read compatibility against existing data. Never copy secrets or personal data from production.

## GitHub configuration

In **Settings → Environments**, create an environment named `staging`. Add these environment-scoped values:

### Variables

- `SCOREBOARDLY_STAGING_APP_NAME`: the staging Function App name, without the hostname.

### Secrets

- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`: publish profile downloaded from the staging Function App only.
- `SCOREBOARDLY_STAGING_FUNCTION_KEY`: a function key (or host key) for the staging app, used only by the staging smoke test to create a disposable test board.

Protect the environment with required reviewers if desired. Do not reuse the production publish profile or production Function key.

## Deploy

1. Push/merge the staging workflow and run PR #20 CI first.
2. In GitHub Actions, select **Deploy Scoreboardly staging**.
3. Select branch `feature/dotnet10-isolated-migration` and run the workflow.
4. The workflow restores, tests, publishes, deploys, then performs API smoke tests. It creates a disposable board named `Staging smoke test` and submits one test score.
5. Review the workflow result and the staging Function App's Application Insights logs.

The workflow is manual-only and refuses to deploy when run from any branch other than `feature/dotnet10-isolated-migration`.

## Manual API checks

The automated smoke test covers board creation, retrieval, and score submission. Also verify:

- `GET /api/scoreboards` returns the expected public leaderboard response (or 404 when the staging container is empty).
- `POST /api/scoreboards` without a Functions key is rejected; with a valid staging key it succeeds.
- Invalid JSON and invalid values are rejected.
- A nonexistent board returns 404; a non-qualifying score returns 409.
- Concurrent submissions do not silently overwrite one another.
- Data remains after an app restart.
- At least one existing JavaScript game can read and submit scores against the staging URL.

**Important scope note:** PR #20 deliberately leaves score submission anonymous for compatibility. Per-game score authentication is in dependent PR #21. Do not treat this staging deployment as proof that score submissions are protected from arbitrary clients.
