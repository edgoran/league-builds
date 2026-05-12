using LeagueBuilds.Api.Models;

namespace LeagueBuilds.Api.Services;

public class PlayerStatsService
{
    private readonly RiotApiService _riotApi;

    public PlayerStatsService(RiotApiService riotApi)
    {
        _riotApi = riotApi;
    }

    public async Task<PlayerProfile?> GetPlayerProfileAsync(string gameName, string tagLine)
    {
        var patch = await _riotApi.GetCurrentPatchAsync();

        // Get account
        var account = await _riotApi.GetSummonerByNameAsync(gameName, tagLine);
        if (account == null) return null;

        // Get summoner info (profile icon, level)
        var summoner = await _riotApi.GetSummonerByPuuidAsync(account.Puuid);

        // Get recent matches
        var matchIds = await _riotApi.GetMatchIdsAsync(account.Puuid, count: 20, type: null);

        var matches = new List<RiotMatchResponse>();
        foreach (var matchId in matchIds)
        {
            var match = await _riotApi.GetMatchAsync(matchId);
            if (match != null) matches.Add(match);
            await Task.Delay(50);
        }

        // Get champion data for icons
        var championData = await _riotApi.GetChampionDataAsync(patch);
        var itemData = await _riotApi.GetItemDataAsync(patch);

        // Build match summaries
        var recentMatches = matches
            .Select(m => BuildMatchSummary(m, account.Puuid, championData, itemData, patch))
            .Where(m => m != null)
            .Cast<MatchSummary>()
            .ToList();

        // Calculate per-champion stats from recent matches
        var topChampions = CalculateChampionStats(recentMatches, championData, patch);

        return new PlayerProfile
        {
            Puuid = account.Puuid,
            GameName = account.GameName,
            TagLine = account.TagLine,
            ProfileIconId = summoner?.ProfileIconId ?? 0,
            SummonerLevel = summoner?.SummonerLevel ?? 0,
            ProfileIconUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/profileicon/{summoner?.ProfileIconId ?? 1}.png",
            RecentMatches = recentMatches,
            TopChampions = topChampions,
            Patch = patch
        };
    }

    public async Task<ChampionDetailStats?> GetChampionDetailAsync(string gameName, string tagLine, string championName)
    {
        var patch = await _riotApi.GetCurrentPatchAsync();

        // Get account
        var account = await _riotApi.GetSummonerByNameAsync(gameName, tagLine);
        if (account == null) return null;

        // Get champion data
        var championData = await _riotApi.GetChampionDataAsync(patch);
        var itemData = await _riotApi.GetItemDataAsync(patch);

        // Find champion ID
        var champion = championData.Values
            .FirstOrDefault(c => c.Name.Equals(championName, StringComparison.OrdinalIgnoreCase)
                || c.Id.Equals(championName, StringComparison.OrdinalIgnoreCase));

        if (champion == null) return null;

        // Get mastery
        var championId = int.Parse(champion.Key);
        var mastery = await _riotApi.GetChampionMasteryByIdAsync(account.Puuid, championId);

        // Get recent matches (more for detailed view)
        var matchIds = await _riotApi.GetMatchIdsAsync(account.Puuid, count: 50, type: null);

        var matches = new List<RiotMatchResponse>();
        foreach (var matchId in matchIds)
        {
            var match = await _riotApi.GetMatchAsync(matchId);
            if (match != null) matches.Add(match);
            await Task.Delay(50);
        }

        // Filter matches for this champion
        var championMatches = matches
            .Select(m => BuildMatchSummary(m, account.Puuid, championData, itemData, patch))
            .Where(m => m != null && m!.ChampionName.Equals(champion.Name, StringComparison.OrdinalIgnoreCase))
            .Cast<MatchSummary>()
            .ToList();

        if (championMatches.Count == 0) return null;

        // Calculate stats
        var wins = championMatches.Count(m => m.Win);
        var losses = championMatches.Count - wins;
        var avgKills = championMatches.Average(m => m.Kills);
        var avgDeaths = championMatches.Average(m => m.Deaths);
        var avgAssists = championMatches.Average(m => m.Assists);
        var avgKda = avgDeaths > 0 ? (avgKills + avgAssists) / avgDeaths : avgKills + avgAssists;
        var avgCs = championMatches.Average(m => m.CreepScore);

        // Most built items
        var itemCounts = new Dictionary<int, int>();
        foreach (var match in championMatches)
        {
            foreach (var itemId in match.ItemIds.Where(id => id > 0))
            {
                if (itemData.TryGetValue(itemId.ToString(), out var item) && item.Gold.Total >= 2000)
                {
                    itemCounts[itemId] = itemCounts.GetValueOrDefault(itemId) + 1;
                }
            }
        }

        var mostBuilt = itemCounts
            .OrderByDescending(kv => kv.Value)
            .Take(6)
            .Select(kv => new PopularItem
            {
                ItemId = kv.Key,
                ItemName = itemData.TryGetValue(kv.Key.ToString(), out var item) ? item.Name : $"Item {kv.Key}",
                ImageUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/item/{kv.Key}.png",
                TimesBuilt = kv.Value
            })
            .ToList();

        return new ChampionDetailStats
        {
            ChampionName = champion.Name,
            ChampionId = champion.Id,
            ChampionIconUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/champion/{champion.Id}.png",
            MasteryLevel = mastery?.ChampionLevel ?? 0,
            MasteryPoints = mastery?.ChampionPoints ?? 0,
            MasteryPointsFormatted = FormatNumber(mastery?.ChampionPoints ?? 0),
            GamesPlayed = championMatches.Count,
            Wins = wins,
            Losses = losses,
            WinRate = championMatches.Count > 0 ? Math.Round((double)wins / championMatches.Count * 100, 1) : 0,
            AvgKills = Math.Round(avgKills, 1),
            AvgDeaths = Math.Round(avgDeaths, 1),
            AvgAssists = Math.Round(avgAssists, 1),
            AvgKda = Math.Round(avgKda, 1),
            KdaString = $"{Math.Round(avgKills, 1)} / {Math.Round(avgDeaths, 1)} / {Math.Round(avgAssists, 1)}",
            AvgCreepScore = Math.Round(avgCs, 0),
            MostBuiltItems = mostBuilt,
            MatchHistory = championMatches,
            Patch = patch
        };
    }

    private MatchSummary? BuildMatchSummary(
        RiotMatchResponse match,
        string puuid,
        Dictionary<string, ChampionInfo> championData,
        Dictionary<string, ItemInfo> itemData,
        string patch)
    {
        var participant = match.Info.Participants
            .FirstOrDefault(p => match.Metadata.Participants.IndexOf(puuid) == match.Info.Participants.IndexOf(p));

        // Find by matching position in participants list
        var puuidIndex = match.Metadata.Participants.IndexOf(puuid);
        if (puuidIndex < 0 || puuidIndex >= match.Info.Participants.Count) return null;

        participant = match.Info.Participants[puuidIndex];

        var items = new List<int>
        {
            participant.Item0,
            participant.Item1,
            participant.Item2,
            participant.Item3,
            participant.Item4,
            participant.Item5,
            participant.Item6
        };

        var deaths = Math.Max(participant.Deaths, 1);
        var kda = (double)(participant.Kills + participant.Assists) / deaths;

        var gameDuration = match.Info.GameDuration;
        var minutes = gameDuration / 60;
        var seconds = gameDuration % 60;

        var gameCreation = match.Info.GameCreation;
        var timeAgo = CalculateTimeAgo(gameCreation);

        var championInfo = championData.Values
            .FirstOrDefault(c => c.Name.Equals(participant.ChampionName, StringComparison.OrdinalIgnoreCase));

        return new MatchSummary
        {
            MatchId = match.Metadata.MatchId,
            ChampionName = participant.ChampionName,
            ChampionId = championInfo?.Id ?? participant.ChampionName,
            ChampionIconUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/champion/{championInfo?.Id ?? participant.ChampionName}.png",
            Win = participant.Win,
            Kills = participant.Kills,
            Deaths = participant.Deaths,
            Assists = participant.Assists,
            Kda = Math.Round(kda, 1),
            KdaString = $"{participant.Kills}/{participant.Deaths}/{participant.Assists}",
            CreepScore = participant.TotalMinionsKilled + participant.NeutralMinionsKilled,
            Role = participant.TeamPosition,
            GameDuration = gameDuration,
            GameDurationString = $"{minutes}m {seconds}s",
            GameMode = match.Info.GameMode,
            GameCreation = gameCreation,
            TimeAgo = timeAgo,
            ItemIds = items,
            ItemIconUrls = items.Where(id => id > 0)
                .Select(id => $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/item/{id}.png")
                .ToList(),
            Spell1Id = participant.Summoner1Id,
            Spell2Id = participant.Summoner2Id
        };
    }

    private List<ChampionStats> CalculateChampionStats(
        List<MatchSummary> matches,
        Dictionary<string, ChampionInfo> championData,
        string patch)
    {
        return matches
            .GroupBy(m => m.ChampionName)
            .Select(g =>
            {
                var wins = g.Count(m => m.Win);
                var total = g.Count();
                var avgKills = g.Average(m => m.Kills);
                var avgDeaths = g.Average(m => m.Deaths);
                var avgAssists = g.Average(m => m.Assists);
                var avgDeathsSafe = Math.Max(avgDeaths, 1);

                var champion = championData.Values
                    .FirstOrDefault(c => c.Name.Equals(g.Key, StringComparison.OrdinalIgnoreCase));

                return new ChampionStats
                {
                    ChampionName = g.Key,
                    ChampionId = champion?.Id ?? g.Key,
                    ChampionIconUrl = $"https://ddragon.leagueoflegends.com/cdn/{patch}/img/champion/{champion?.Id ?? g.Key}.png",
                    GamesPlayed = total,
                    Wins = wins,
                    Losses = total - wins,
                    WinRate = total > 0 ? Math.Round((double)wins / total * 100, 1) : 0,
                    AvgKills = Math.Round(avgKills, 1),
                    AvgDeaths = Math.Round(avgDeaths, 1),
                    AvgAssists = Math.Round(avgAssists, 1),
                    AvgKda = Math.Round((avgKills + avgAssists) / avgDeathsSafe, 1),
                    KdaString = $"{Math.Round(avgKills, 1)} / {Math.Round(avgDeaths, 1)} / {Math.Round(avgAssists, 1)}"
                };
            })
            .OrderByDescending(c => c.GamesPlayed)
            .Take(10)
            .ToList();
    }

    private string CalculateTimeAgo(long gameCreationMs)
    {
        var gameTime = DateTimeOffset.FromUnixTimeMilliseconds(gameCreationMs);
        var diff = DateTimeOffset.UtcNow - gameTime;

        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return $"{(int)(diff.TotalDays / 7)}w ago";
    }

    private string FormatNumber(int number)
    {
        if (number >= 1000000) return $"{number / 1000000.0:F1}M";
        if (number >= 1000) return $"{number / 1000.0:F1}K";
        return number.ToString();
    }
}