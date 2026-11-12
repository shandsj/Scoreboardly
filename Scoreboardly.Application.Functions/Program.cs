using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scoreboardly.Application.Functions.Auth;
using Scoreboardly.Application.Functions.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration["COSMOS_DB_CONNECTION_STRING"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("COSMOS_DB_CONNECTION_STRING is required.");
        }

        services.AddSingleton(_ => new CosmosClient(
            connectionString,
            new CosmosClientOptions { Serializer = new NewtonsoftJsonCosmosSerializer() }));

        services.AddSingleton<ScoreboardRepository>();
        services.AddSingleton<GameCredentialRepository>();
        services.AddSingleton<AccessTokenService>();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
