# .NET 10 isolated-worker hosting migration

The Functions project now uses the .NET 10 isolated worker model. The production deployment workflow is intentionally manual while the Azure Function App runtime is being updated.

Before deploying:

1. Set `FUNCTIONS_WORKER_RUNTIME` to `dotnet-isolated`.
2. Keep `FUNCTIONS_EXTENSION_VERSION` set to `~4`.
3. Confirm the operating system and hosting plan support .NET 10. .NET 10 is not supported on Linux Consumption; use a supported plan such as Flex Consumption for Linux.
4. Restart the Function App after changing runtime settings.
5. Prefer a staging slot when available, then verify public leaderboard reads and test score submissions before switching production traffic.

The deployment workflow can only be dispatched against `main` and requires an explicit runtime-configuration confirmation. Do not dispatch it until the Function App has been updated and verified. A mismatch between the deployed isolated-worker package and the in-process `dotnet` worker setting prevents the app from starting.

The existing `Scoreboards` Cosmos DB container and its `/id` partition key are retained. The custom Newtonsoft serializer preserves the domain model's existing document shape.

This migration does not yet secure score submissions. The follow-up authentication change adds per-game credentials and scoped tokens; until that change is deployed and enforcement is enabled, score writes remain anonymous.
