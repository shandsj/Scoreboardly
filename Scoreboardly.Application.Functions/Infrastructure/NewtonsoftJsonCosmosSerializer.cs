using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;

namespace Scoreboardly.Application.Functions.Infrastructure;

/// <summary>
/// Keeps Cosmos DB document serialization compatible with the existing Newtonsoft.Json domain model.
/// </summary>
public sealed class NewtonsoftJsonCosmosSerializer : CosmosSerializer
{
    /// <inheritdoc />
    public override T FromStream<T>(Stream stream)
    {
        if (typeof(Stream).IsAssignableFrom(typeof(T)))
        {
            return (T)(object)stream;
        }

        using (stream)
        using (var textReader = new StreamReader(stream))
        using (var jsonReader = new JsonTextReader(textReader))
        {
            return JsonSerializer.CreateDefault().Deserialize<T>(jsonReader)!;
        }
    }

    /// <inheritdoc />
    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        using (var textWriter = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
        using (var jsonWriter = new JsonTextWriter(textWriter))
        {
            JsonSerializer.CreateDefault().Serialize(jsonWriter, input);
            jsonWriter.Flush();
            textWriter.Flush();
        }

        stream.Position = 0;
        return stream;
    }
}
