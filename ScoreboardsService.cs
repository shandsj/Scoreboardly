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

namespace Scoreboardly
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
                return new OkObjectResult(JsonConvert.SerializeObject(scoreboards));
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
            Scoreboard scoreboard = JsonConvert.DeserializeObject<Scoreboard>(requestBody);
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
                return new OkObjectResult(JsonConvert.SerializeObject(scoreboard));
            }
        }
    }
}
