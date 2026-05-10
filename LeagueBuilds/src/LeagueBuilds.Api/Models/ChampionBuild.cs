namespace LeagueBuilds.Api.Models;

public class ChampionBuild
{
    public string ChampionId { get; set; } = string.Empty;
    public string ChampionName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<ItemBuild> ItemBuilds { get; set; } = new();
    public List<RunePage> RunePages { get; set; } = new();
    public List<SkillOrder> SkillOrders { get; set; } = new();
    public List<SummonerSpellSet> SummonerSpells { get; set; } = new();
    public string Patch { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}

public class ItemBuild
{
    public List<int> ItemIds { get; set; } = new();
    public List<string> ItemNames { get; set; } = new();
    public double WinRate { get; set; }
    public double PickRate { get; set; }
    public int MatchCount { get; set; }
}

public class RunePage
{
    public int PrimaryStyleId { get; set; }
    public string PrimaryStyleName { get; set; } = string.Empty;
    public List<int> PrimaryRuneIds { get; set; } = new();
    public List<string> PrimaryRuneNames { get; set; } = new();
    public int SecondaryStyleId { get; set; }
    public string SecondaryStyleName { get; set; } = string.Empty;
    public List<int> SecondaryRuneIds { get; set; } = new();
    public List<string> SecondaryRuneNames { get; set; } = new();
    public double WinRate { get; set; }
    public double PickRate { get; set; }
    public int MatchCount { get; set; }
}

public class SkillOrder
{
    public List<string> LevelSequence { get; set; } = new();
    public string Priority { get; set; } = string.Empty; // e.g., "Q → E → W"
    public double WinRate { get; set; }
    public double PickRate { get; set; }
    public int MatchCount { get; set; }
}

public class SummonerSpellSet
{
    public int Spell1Id { get; set; }
    public string Spell1Name { get; set; } = string.Empty;
    public int Spell2Id { get; set; }
    public string Spell2Name { get; set; } = string.Empty;
    public double WinRate { get; set; }
    public double PickRate { get; set; }
    public int MatchCount { get; set; }
}