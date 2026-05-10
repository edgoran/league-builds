using LeagueBuilds.Api.Models;

namespace LeagueBuilds.Api.Services;

public class MatchAggregator
{
    private readonly RiotApiService _riotApi;

    public MatchAggregator(RiotApiService riotApi)
    {
        _riotApi = riotApi;
    }

    /// <summary>
    /// Aggregates match data for a specific champion across multiple matches.
    /// Returns build statistics including items, runes, skills, and summoner spells.
    /// </summary>
    public async Task<ChampionBuild?> AggregateChampionDataAsync(
        string championName,
        List<RiotMatchResponse> matches,
        string? roleFilter = null)
    {
        var patch = await _riotApi.GetCurrentPatchAsync();
        var itemData = await _riotApi.GetItemDataAsync(patch);

        // Filter participants for this champion
        var relevantParticipants = new List<MatchParticipant>();

        foreach (var match in matches)
        {
            var participants = match.Info.Participants
                .Where(p => p.ChampionName.Equals(championName, StringComparison.OrdinalIgnoreCase));

            if (roleFilter != null)
            {
                participants = participants
                    .Where(p => p.TeamPosition.Equals(roleFilter, StringComparison.OrdinalIgnoreCase));
            }

            relevantParticipants.AddRange(participants);
        }

        if (relevantParticipants.Count == 0)
            return null;

        return new ChampionBuild
        {
            ChampionName = championName,
            ChampionId = championName.ToLower(),
            Role = roleFilter ?? DetectMostCommonRole(relevantParticipants),
            ItemBuilds = AggregateItemBuilds(relevantParticipants, itemData),
            RunePages = AggregateRunes(relevantParticipants),
            SkillOrders = AggregateSkillOrders(relevantParticipants),
            SummonerSpells = AggregateSummonerSpells(relevantParticipants),
            Patch = patch,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Collects match data from high-elo players for a specific champion.
    /// </summary>
    public async Task<List<RiotMatchResponse>> CollectMatchesForChampionAsync(
        string championName,
        int targetMatchCount = 100)
    {
        var matches = new List<RiotMatchResponse>();
        var challengerPlayers = await _riotApi.GetChallengerPlayersAsync();

        // Shuffle to get variety
        var shuffled = challengerPlayers.OrderBy(_ => Random.Shared.Next()).ToList();

        foreach (var player in shuffled)
        {
            if (matches.Count >= targetMatchCount) break;

            var puuid = await _riotApi.GetPuuidBySummonerIdAsync(player.SummonerId);
            if (puuid == null) continue;

            var matchIds = await _riotApi.GetMatchIdsAsync(puuid, count: 10);

            foreach (var matchId in matchIds)
            {
                if (matches.Count >= targetMatchCount) break;

                var match = await _riotApi.GetMatchAsync(matchId);
                if (match == null) continue;

                // Only include if this champion was played
                var hasChampion = match.Info.Participants
                    .Any(p => p.ChampionName.Equals(championName, StringComparison.OrdinalIgnoreCase));

                if (hasChampion)
                {
                    matches.Add(match);
                }

                // Rate limiting — Riot API allows 20 requests per second
                await Task.Delay(50);
            }

            // Rate limiting between players
            await Task.Delay(100);
        }

        return matches;
    }

    #region Private Aggregation Methods

    private string DetectMostCommonRole(List<MatchParticipant> participants)
    {
        return participants
            .Where(p => !string.IsNullOrEmpty(p.TeamPosition))
            .GroupBy(p => p.TeamPosition)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "MIDDLE";
    }

    private List<ItemBuild> AggregateItemBuilds(
        List<MatchParticipant> participants,
        Dictionary<string, ItemInfo> itemData)
    {
        var buildCounts = new Dictionary<string, BuildCounter>();

        foreach (var participant in participants)
        {
            // Get completed items (exclude item slot 6 which is trinket, and 0 which means empty)
            var items = new List<int>
            {
                participant.Item0,
                participant.Item1,
                participant.Item2,
                participant.Item3,
                participant.Item4,
                participant.Item5
            }
            .Where(id => id > 0)
            .OrderBy(id => id) // Sort for consistent key
            .ToList();

            if (items.Count < 3) continue; // Skip incomplete builds

            var key = string.Join(",", items);

            if (!buildCounts.ContainsKey(key))
            {
                buildCounts[key] = new BuildCounter { Items = items };
            }

            buildCounts[key].TotalGames++;
            if (participant.Win) buildCounts[key].Wins++;
        }

        return buildCounts.Values
            .Where(b => b.TotalGames >= 3) // Minimum sample size
            .OrderByDescending(b => b.TotalGames)
            .Take(5) // Top 5 builds
            .Select(b => new ItemBuild
            {
                ItemIds = b.Items,
                ItemNames = b.Items
                    .Select(id => itemData.TryGetValue(id.ToString(), out var item) ? item.Name : $"Item {id}")
                    .ToList(),
                WinRate = b.TotalGames > 0 ? Math.Round((double)b.Wins / b.TotalGames * 100, 1) : 0,
                PickRate = Math.Round((double)b.TotalGames / participants.Count * 100, 1),
                MatchCount = b.TotalGames
            })
            .ToList();
    }

    private List<RunePage> AggregateRunes(List<MatchParticipant> participants)
    {
        var runeCounts = new Dictionary<string, RuneCounter>();

        foreach (var participant in participants)
        {
            if (participant.Perks?.Styles == null || participant.Perks.Styles.Count < 2)
                continue;

            var primaryStyle = participant.Perks.Styles
                .FirstOrDefault(s => s.Description == "primaryStyle");
            var secondaryStyle = participant.Perks.Styles
                .FirstOrDefault(s => s.Description == "subStyle");

            if (primaryStyle == null || secondaryStyle == null) continue;

            var primarySelections = primaryStyle.Selections.Select(s => s.Perk).ToList();
            var secondarySelections = secondaryStyle.Selections.Select(s => s.Perk).ToList();

            var key = $"{primaryStyle.Style}:{string.Join(",", primarySelections)}|{secondaryStyle.Style}:{string.Join(",", secondarySelections)}";

            if (!runeCounts.ContainsKey(key))
            {
                runeCounts[key] = new RuneCounter
                {
                    PrimaryStyleId = primaryStyle.Style,
                    PrimarySelections = primarySelections,
                    SecondaryStyleId = secondaryStyle.Style,
                    SecondarySelections = secondarySelections
                };
            }

            runeCounts[key].TotalGames++;
            if (participant.Win) runeCounts[key].Wins++;
        }

        return runeCounts.Values
            .Where(r => r.TotalGames >= 3)
            .OrderByDescending(r => r.TotalGames)
            .Take(3) // Top 3 rune pages
            .Select(r => new RunePage
            {
                PrimaryStyleId = r.PrimaryStyleId,
                PrimaryRuneIds = r.PrimarySelections,
                SecondaryStyleId = r.SecondaryStyleId,
                SecondaryRuneIds = r.SecondarySelections,
                WinRate = r.TotalGames > 0 ? Math.Round((double)r.Wins / r.TotalGames * 100, 1) : 0,
                PickRate = Math.Round((double)r.TotalGames / participants.Count * 100, 1),
                MatchCount = r.TotalGames
            })
            .ToList();
    }

    private List<SkillOrder> AggregateSkillOrders(List<MatchParticipant> participants)
    {
        // Since Riot API doesn't provide skill level-up order directly in match-v5,
        // we'll determine priority based on max rank patterns.
        // For now, return common known orders — this can be enhanced with timeline data.
        var skillPriorities = new Dictionary<string, SkillCounter>();

        // We'll group by the most common skill max order
        // This is a simplified version — full implementation would use match timeline API
        foreach (var participant in participants)
        {
            // Default priority detection based on champion patterns
            // In a full implementation, you'd call the match timeline endpoint
            var priority = "Q → W → E"; // Placeholder

            if (!skillPriorities.ContainsKey(priority))
            {
                skillPriorities[priority] = new SkillCounter { Priority = priority };
            }

            skillPriorities[priority].TotalGames++;
            if (participant.Win) skillPriorities[priority].Wins++;
        }

        return skillPriorities.Values
            .OrderByDescending(s => s.TotalGames)
            .Take(3)
            .Select(s => new SkillOrder
            {
                Priority = s.Priority,
                WinRate = s.TotalGames > 0 ? Math.Round((double)s.Wins / s.TotalGames * 100, 1) : 0,
                PickRate = Math.Round((double)s.TotalGames / participants.Count * 100, 1),
                MatchCount = s.TotalGames
            })
            .ToList();
    }

    private List<SummonerSpellSet> AggregateSummonerSpells(List<MatchParticipant> participants)
    {
        var spellCounts = new Dictionary<string, SpellCounter>();

        foreach (var participant in participants)
        {
            // Sort spell IDs so Flash+Ignite and Ignite+Flash are the same
            var spells = new[] { participant.Summoner1Id, participant.Summoner2Id }
                .OrderBy(s => s)
                .ToList();

            var key = $"{spells[0]},{spells[1]}";

            if (!spellCounts.ContainsKey(key))
            {
                spellCounts[key] = new SpellCounter
                {
                    Spell1Id = spells[0],
                    Spell2Id = spells[1]
                };
            }

            spellCounts[key].TotalGames++;
            if (participant.Win) spellCounts[key].Wins++;
        }

        return spellCounts.Values
            .OrderByDescending(s => s.TotalGames)
            .Take(3) // Top 3 spell combos
            .Select(s => new SummonerSpellSet
            {
                Spell1Id = s.Spell1Id,
                Spell2Id = s.Spell2Id,
                WinRate = s.TotalGames > 0 ? Math.Round((double)s.Wins / s.TotalGames * 100, 1) : 0,
                PickRate = Math.Round((double)s.TotalGames / participants.Count * 100, 1),
                MatchCount = s.TotalGames
            })
            .ToList();
    }

    #endregion

    #region Counter Classes

    private class BuildCounter
    {
        public List<int> Items { get; set; } = new();
        public int TotalGames { get; set; }
        public int Wins { get; set; }
    }

    private class RuneCounter
    {
        public int PrimaryStyleId { get; set; }
        public List<int> PrimarySelections { get; set; } = new();
        public int SecondaryStyleId { get; set; }
        public List<int> SecondarySelections { get; set; } = new();
        public int TotalGames { get; set; }
        public int Wins { get; set; }
    }

    private class SkillCounter
    {
        public string Priority { get; set; } = string.Empty;
        public int TotalGames { get; set; }
        public int Wins { get; set; }
    }

    private class SpellCounter
    {
        public int Spell1Id { get; set; }
        public int Spell2Id { get; set; }
        public int TotalGames { get; set; }
        public int Wins { get; set; }
    }

    #endregion
}