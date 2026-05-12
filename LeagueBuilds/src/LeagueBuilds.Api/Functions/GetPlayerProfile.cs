using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LeagueBuilds.Api.Models;
using LeagueBuilds.Api.Services;

namespace LeagueBuilds.Api.Functions;

public class GetPlayerProfile
{
    private RiotApiService _riotApi;
    private PlayerStatsService _playerStats;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GetPlayerProfile()
    {
        _riotApi = null!;
        _playerStats = null!;
    }

    private async Task EnsureInitialised()
    {
        if (_riotApi != null && _playerStats != null) return;

        var apiKey = await ConfigService.GetRiotApiKeyAsync();
        _riotApi = new RiotApiService(apiKey, region: "europe", platform: "euw1");
        _playerStats = new PlayerStatsService(_riotApi);
    }

    public async Task<APIGatewayProxyResponse> HandleAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            await EnsureInitialised();

            string? name = null;
            string? tag = null;
            request.PathParameters?.TryGetValue("name", out name);
            request.PathParameters?.TryGetValue("tag", out tag);

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(tag))
            {
                return CreateResponse(400, ApiResponse<PlayerProfile>.Fail("Name and tag are required"));
            }

            context.Logger.LogInformation($"Fetching profile for {name}#{tag}");

            var profile = await _playerStats.GetPlayerProfileAsync(name, tag);

            if (profile == null)
            {
                return CreateResponse(404, ApiResponse<PlayerProfile>.Fail($"Player '{name}#{tag}' not found"));
            }

            context.Logger.LogInformation($"Found {profile.RecentMatches.Count} matches for {name}#{tag}");

            return CreateResponse(200, ApiResponse<PlayerProfile>.Ok(profile, profile.Patch));
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}\n{ex.StackTrace}");
            return CreateResponse(500, ApiResponse<PlayerProfile>.Fail("Internal server error"));
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