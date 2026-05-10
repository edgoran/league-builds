namespace LeagueBuilds.Api.Models;

public class MatchData
{
    public string MatchId { get; set; } = string.Empty;
    public long GameDuration { get; set; }
    public string GameVersion { get; set; } = string.Empty;
    public List<ParticipantData> Participants { get; set; } = new();
}

public class ParticipantData
{
    public string ChampionName { get; set; } = string.Empty;
    public int ChampionId { get; set; }
    public bool Win { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Lane { get; set; } = string.Empty;
    public List<int> Items { get; set; } = new();
    public int Spell1Id { get; set; }
    public int Spell2Id { get; set; }
    public PerksData Perks { get; set; } = new();
    public SkillLevelData Skills { get; set; } = new();
}

public class PerksData
{
    public int PrimaryStyleId { get; set; }
    public List<int> PrimarySelections { get; set; } = new();
    public int SecondaryStyleId { get; set; }
    public List<int> SecondarySelections { get; set; } = new();
}

public class SkillLevelData
{
    public List<int> SkillOrder { get; set; } = new();
}