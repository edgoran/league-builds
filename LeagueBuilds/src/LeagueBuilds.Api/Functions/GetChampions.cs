using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LeagueBuilds.Api.Models;
using LeagueBuilds.Api.Services;

namespace LeagueBuilds.Api.Functions;

public class GetChampions
{
    private RiotApiService _riotApi;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GetChampions()
    {
        _riotApi = null!;
    }

    private async Task EnsureInitialised()
    {
        if (_riotApi != null) return;

        var apiKey = await ConfigService.GetRiotApiKeyAsync();
        _riotApi = new RiotApiService(apiKey, region: "europe", platform: "euw1");
    }

    public async Task<APIGatewayProxyResponse> HandleAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            await EnsureInitialised();

            var patch = await _riotApi.GetCurrentPatchAsync();
            var championData = await _riotApi.GetChampionDataAsync(patch);

            var champions = championData.Values
                .Select(c => new ChampionListItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Key = c.Key,
                    ImageUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/champion/{c.Image.Full}"
                })
                .OrderBy(c => c.Name)
                .ToList();

            var response = new ChampionListResponse
            {
                Champions = champions,
                Patch = patch,
                Count = champions.Count
            };

            context.Logger.LogInformation($"Returning {champions.Count} champions for patch {patch}");

            return CreateResponse(200, ApiResponse<ChampionListResponse>.Ok(response, patch));
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}");
            return CreateResponse(500, ApiResponse<ChampionListResponse>.Fail("Internal server error"));
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

// Response models for this endpoint
public class ChampionListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class ChampionListResponse
{
    public List<ChampionListItem> Champions { get; set; } = new();
    public string Patch { get; set; } = string.Empty;
    public int Count { get; set; }
}