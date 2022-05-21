using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Scoreboardly.Domain;
using Scoreboardly.Application.Functions.Api;

namespace Scoreboardly.Application.Functions
{
    public static class ScoreboardsService
    {

        [FunctionName("GetAllScoreboards")]
        public static IActionResult GetAllScoreboards(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",
                Route = "scoreboards")]HttpRequest request,
            [CosmosDB(
                databaseName: "Scoreboardly",
                collectionName: "Scoreboards",
                SqlQuery = "select * from c",
                ConnectionStringSetting = "COSMOS_DB_CONNECTION_STRING")] IEnumerable<Scoreboard> scoreboards,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            if (!scoreboards.Any())
            {
                log.LogInformation($"No scoreboards found");
                return new NotFoundResult();
            }
            else
            {
                log.LogInformation($"Multiple scoreboards found: {scoreboards.Count()}");
                var scoreboardDtos = scoreboards.Select(new ScoreboardFactory().CreateScoreboardDto).ToArray();
                return new OkObjectResult(JsonConvert.SerializeObject(scoreboardDtos));
            }
        }

        

        [FunctionName("CreateScoreboard")]
        public static async Task<IActionResult> CreateScoreboardAsync(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "post",
                Route = "scoreboards")] HttpRequest request,
            [CosmosDB(
                databaseName: "Scoreboardly",
                collectionName: "Scoreboards",
                ConnectionStringSetting = "COSMOS_DB_CONNECTION_STRING")] IAsyncCollector<Scoreboard> scoreboards,
            ILogger log)
        {
            log.LogInformation("Received a create scoreboard request", request);

            string requestBody = await new StreamReader(request.Body).ReadToEndAsync();

            CreateScoreboardRequest createScoreboardRequest;
            try
            {
                createScoreboardRequest = JsonConvert.DeserializeObject<CreateScoreboardRequest>(requestBody);
            }
            catch (Exception ex)
            {
                log.LogWarning("Received a bad request", ex);
                return new BadRequestResult();
            }

            var scoreboard = new ScoreboardFactory().CreateScoreboard(createScoreboardRequest);
            await scoreboards.AddAsync(scoreboard);

            return new OkResult();
        }

        [FunctionName("GetScoreboardById")]
        public static IActionResult GetScoreboardById(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "get",
                Route = "scoreboards/{id}")] HttpRequest request,
            [CosmosDB(
                databaseName: "Scoreboardly",
                collectionName: "Scoreboards",
                ConnectionStringSetting = "COSMOS_DB_CONNECTION_STRING",
                PartitionKey = "{id}",
                Id = "{id}")] Scoreboard scoreboard,
            ILogger log)
        {
            log.LogInformation("Received a get scoreboard by id request", request);

            if (scoreboard == null)
            {
                log.LogInformation($"Scoreboard not found");
                return new NotFoundResult();
            }
            else
            {
                log.LogInformation($"Scoreboard found");
                var scoreboardDto = new ScoreboardFactory().CreateScoreboardDto(scoreboard);
                return new OkObjectResult(JsonConvert.SerializeObject(scoreboardDto));
            }
        }

        

        [FunctionName("PutNewScoreEntry")]
        public static async Task<IActionResult> PutNewScoreEntryAsync(
            [HttpTrigger(
                AuthorizationLevel.Anonymous,
                "put",
                Route = "scoreboards/{id}")] HttpRequest request,
            [CosmosDB(
                databaseName: "Scoreboardly",
                collectionName: "Scoreboards",
                ConnectionStringSetting = "COSMOS_DB_CONNECTION_STRING",
                PartitionKey = "{id}",
                Id = "{id}")] Scoreboard scoreboard,
            [CosmosDB(
                databaseName: "Scoreboardly",
                collectionName: "Scoreboards",
                ConnectionStringSetting = "COSMOS_DB_CONNECTION_STRING")] IAsyncCollector<Scoreboard> scoreboards,
            ILogger log)
        {
            log.LogInformation("Received a get scoreboard by id request", request);

            string requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            
            EntryDto entryDto;
            try
            {
                entryDto = JsonConvert.DeserializeObject<EntryDto>(requestBody);
            }
            catch (Exception ex)
            {
                scoreboard = null;
                log.LogWarning("Received a bad request", ex);
                return new BadRequestResult();
            }

            var result = scoreboard.EnterScore(entryDto.PlayerName, entryDto.Score);
            if (!result)
            {
                return new ConflictResult();
            }

            await scoreboards.AddAsync(scoreboard);            
            return new OkResult();
        }
    }
}
