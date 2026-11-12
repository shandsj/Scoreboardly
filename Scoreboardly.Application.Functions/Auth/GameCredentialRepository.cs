using System.Net;
using Microsoft.Azure.Cosmos;

namespace Scoreboardly.Application.Functions.Auth;

/// <summary>
/// Persists per-game credentials in the GameCredentials Cosmos DB container.
/// </summary>
public sealed class GameCredentialRepository
{
    private readonly Container container;

    /// <summary>
    /// Initializes the repository using the GameCredentials container.
    /// </summary>
    public GameCredentialRepository(CosmosClient client)
    {
        container = client.GetContainer("Scoreboardly", "GameCredentials");
    }

    /// <summary>
    /// Loads a credential by its public identifier.
    /// </summary>
    public async Task<GameCredential?> GetByIdAsync(
        string credentialId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.ReadItemAsync<GameCredential>(
                credentialId,
                new PartitionKey(credentialId),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Inserts a new credential record.
    /// </summary>
    public async Task CreateAsync(GameCredential credential, CancellationToken cancellationToken = default)
    {
        await container.CreateItemAsync(
            credential,
            new PartitionKey(credential.Id),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Replaces an existing credential record.
    /// </summary>
    public async Task ReplaceAsync(GameCredential credential, CancellationToken cancellationToken = default)
    {
        await container.ReplaceItemAsync(
            credential,
            credential.Id,
            new PartitionKey(credential.Id),
            cancellationToken: cancellationToken);
    }
}
