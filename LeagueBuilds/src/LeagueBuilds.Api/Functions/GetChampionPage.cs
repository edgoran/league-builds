using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using LeagueBuilds.Api.Models;
using LeagueBuilds.Api.Services;

namespace LeagueBuilds.Api.Functions;

public class GetChampionPage
{
    private RiotApiService _riotApi;
    private PlayerStatsService _playerStats;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GetChampionPage()
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

            string? championName = null;
            request.PathParameters?.TryGetValue("name", out championName);

            if (string.IsNullOrWhiteSpace(championName))
            {
                return CreateResponse(400, ApiResponse<ChampionPageData>.Fail("Champion name is required"));
            }

            // Optional player context
            string? playerName = null;
            string? playerTag = null;
            request.QueryStringParameters?.TryGetValue("player", out playerName);
            request.QueryStringParameters?.TryGetValue("tag", out playerTag);

            var patch = await _riotApi.GetCurrentPatchAsync();
            var championList = await _riotApi.GetChampionDataAsync(patch);

            var matchedChampion = championList.Values
                .FirstOrDefault(c => c.Id.Equals(championName, StringComparison.OrdinalIgnoreCase)
                    || c.Name.Equals(championName, StringComparison.OrdinalIgnoreCase));

            if (matchedChampion == null)
            {
                return CreateResponse(404, ApiResponse<ChampionPageData>.Fail($"Champion '{championName}' not found"));
            }

            var championId = matchedChampion.Id;
            context.Logger.LogInformation($"Fetching champion page for {championId}");

            // Get champion detail
            var detailResponse = await _riotApi.GetChampionDetailAsync(championId, patch);
            if (detailResponse == null || !detailResponse.Data.ContainsKey(championId))
            {
                return CreateResponse(404, ApiResponse<ChampionPageData>.Fail($"Could not fetch detail for {championId}"));
            }

            var detail = detailResponse.Data[championId];

            // Build page data
            var pageData = new ChampionPageData
            {
                ChampionId = championId,
                ChampionName = detail.Name,
                Title = detail.Title,
                Lore = detail.Lore,
                Roles = detail.Tags,
                Patch = patch,
                Abilities = new ChampionAbilities
                {
                    Passive = MapSpell(detail.Passive, patch),
                    Q = MapSpell(detail.Spells, 0, patch),
                    W = MapSpell(detail.Spells, 1, patch),
                    E = MapSpell(detail.Spells, 2, patch),
                    R = MapSpell(detail.Spells, 3, patch)
                },
                Skins = detail.Skins
                    .Where(s => !s.Name.ToLower().Contains("chroma"))
                    .Select(s => new SkinInfo
                    {
                        SkinNum = s.Num,
                        Name = s.Name == "default" ? detail.Name : s.Name,
                        SplashUrl = $"https://ddragon.leagueoflegends.com/cdn/img/champion/splash/{championId}_{s.Num}.jpg",
                        LoadingUrl = $"https://ddragon.leagueoflegends.com/cdn/img/champion/loading/{championId}_{s.Num}.jpg"
                    })
                    .ToList()
            };

            // If player context provided, fetch personal stats
            if (!string.IsNullOrEmpty(playerName) && !string.IsNullOrEmpty(playerTag))
            {
                context.Logger.LogInformation($"Including personal stats for {playerName}#{playerTag}");

                try
                {
                    var personalStats = await _playerStats.GetChampionDetailAsync(playerName, playerTag, matchedChampion.Name);
                    pageData.PersonalStats = personalStats;
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning($"Could not fetch personal stats: {ex.Message}");
                }
            }

            return CreateResponse(200, ApiResponse<ChampionPageData>.Ok(pageData, patch));
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error: {ex.Message}\n{ex.StackTrace}");
            return CreateResponse(500, ApiResponse<ChampionPageData>.Fail("Internal server error"));
        }
    }

    private AbilityInfo MapSpell(ChampionPassive passive, string patch)
    {
        return new AbilityInfo
        {
            Name = passive.Name,
            Description = StripHtml(passive.Description),
            ImageUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/passive/{passive.Image.Full}"
        };
    }

    private AbilityInfo MapSpell(List<ChampionSpell> spells, int index, string patch)
    {
        if (index >= spells.Count) return new AbilityInfo();
        var spell = spells[index];
        return new AbilityInfo
        {
            Name = spell.Name,
            Description = StripHtml(spell.Description),
            ImageUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/spell/{spell.Image.Full}"
        };
    }

    private string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        return Regex.Replace(html, "<.*?>", "");
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