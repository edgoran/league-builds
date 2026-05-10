using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LeagueBuilds.Api.Models;
using LeagueBuilds.Api.Services;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LeagueBuilds.Api.Functions;

public class GetChampionBuilds
{
    private readonly CacheService _cache;
    private MatchAggregator _aggregator;
    private RiotApiService _riotApi;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GetChampionBuilds()
    {
        var tableName = Environment.GetEnvironmentVariable("CACHE_TABLE_NAME")
            ?? "LeagueBuildsCache";

        var dynamoDb = new AmazonDynamoDBClient();
        _cache = new CacheService(dynamoDb, tableName);

        // These will be initialised on first request (need async for SSM)
        _riotApi = null!;
        _aggregator = null!;
    }

    private async Task EnsureInitialised()
    {
        if (_riotApi != null && _aggregator != null) return;

        var apiKey = await ConfigService.GetRiotApiKeyAsync();
        _riotApi = new RiotApiService(apiKey, region: "europe", platform: "euw1");
        _aggregator = new MatchAggregator(_riotApi);
    }

    public async Task<APIGatewayProxyResponse> HandleAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            await EnsureInitialised();

            // Extract champion name from path
            string? championName = null;
            request.PathParameters?.TryGetValue("name", out championName);

            if (string.IsNullOrWhiteSpace(championName))
            {
                return CreateResponse(400, ApiResponse<ChampionBuild>.Fail("Champion name is required"));
            }

            // Normalise champion name (capitalise first letter)
            championName = char.ToUpper(championName[0]) + championName[1..].ToLower();

            // Optional role filter from query string
            string? role = null;
            request.QueryStringParameters?.TryGetValue("role", out role);

            context.Logger.LogInformation($"Fetching builds for {championName}, role: {role ?? "auto"}");

            // Check cache first
            var cachedBuild = await _cache.GetCachedBuildAsync(championName, role ?? "all");

            if (cachedBuild != null)
            {
                context.Logger.LogInformation($"Cache hit for {championName}");
                return CreateResponse(200, ApiResponse<ChampionBuild>.Ok(cachedBuild, cachedBuild.Patch));
            }

            context.Logger.LogInformation($"Cache miss for {championName} — fetching from Riot API");

            // Cache miss — collect and aggregate data
            var matches = await _aggregator.CollectMatchesForChampionAsync(championName, targetMatchCount: 50);

            if (matches.Count == 0)
            {
                return CreateResponse(404, ApiResponse<ChampionBuild>.Fail($"No match data found for {championName}"));
            }

            var build = await _aggregator.AggregateChampionDataAsync(championName, matches, role);

            if (build == null)
            {
                return CreateResponse(404, ApiResponse<ChampionBuild>.Fail($"Could not aggregate data for {championName}"));
            }

            // Cache the result
            await _cache.SetCachedBuildAsync(championName, role ?? "all", build);

            context.Logger.LogInformation($"Successfully aggregated {matches.Count} matches for {championName}");

            return CreateResponse(200, ApiResponse<ChampionBuild>.Ok(build, build.Patch));
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return CreateResponse(500, ApiResponse<ChampionBuild>.Fail("Internal server error"));
        }
    }

    private APIGatewayProxyResponse CreateResponse<T>(int statusCode, T body)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Access-Control-Allow-Origin"] = "*",
                ["Access-Control-Allow-Methods"] = "GET,OPTIONS",
                ["Access-Control-Allow-Headers"] = "Content-Type"
            },
            Body = JsonSerializer.Serialize(body, JsonOptions)
        };
    }
}