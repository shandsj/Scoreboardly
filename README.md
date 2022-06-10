# Scoreboardly
Scoreboardly is a REST API for storing and retrieving high scores in games that runs in Azure using Azure Functions and Cosmos DB.

## Prerequisites
To build and run the project locally, you'll need to install the following prequisites:
* [Azure Function Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=v4%2Cwindows%2Ccsharp%2Cportal%2Cbash)
* [dotnet SDK](https://dotnet.microsoft.com/en-us/download)
* [Cosmos DB Emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator?tabs=ssl-netstd21)

### Cosmos DB Emulator Configuration
* A database called `Scoreboardly` will need to be created.
* A container called `Scoreboards` will need to be created in the `Scoreboardly` database.
> `/id` should be used as the parition key.
* Configure a `local.settings.json` file, and add a `COSMOS_DB_CONNECTION_STRING` property that points to the connection string the Cosmos DB Emulator uses.

## Building
To install the required packages, and build, run the following commands:
```
dotnet restore
dotnet build
```

## Running
To run the Azure Function locally, run the following command:
```
cd Scoreboardly.Applications.Functions
func start
```

## Endpoints
* [Get All Scoreboards](Docs/GetScoreboards.md) : `GET /api/scoreboards`
* [Get Scoreboard by ID](Docs/GetScoreboardById.md) : `GET /api/scoreboards/:id`
* [Create Scoreboard](Docs/CreateScoreboard.md) : `POST /api/scoreboards`
* [Add Scoreboard Entry](Docs/AddScoreboardEntry.md) : `PUT /api/scoreboards/:id`

## Contributing

Feel free to create issues, or submit a PR. Run the `npm run eslint --fix src` command and fix warnings before submitting PRs.

## License

MIT © Jason Shands
