namespace TeamGearPlanning.Helpers;

public static class FFXIVJobs
{
    public static readonly string[] AllJobs = new[]
    {
        // Tanks
        "Paladin",
        "Warrior",
        "Dark Knight",
        "Gunbreaker",
        
        // Healers
        "White Mage",
        "Scholar",
        "Astrologian",
        "Sage",
        
        // Melee DPS
        "Dragoon",
        "Monk",
        "Ninja",
        "Samurai",
        "Reaper",
        "Viper",
        
        // Ranged Physical DPS
        "Bard",
        "Machinist",
        "Dancer",
        
        // Ranged Magical DPS
        "Black Mage",
        "Summoner",
        "Red Mage",
        "Blue Mage"
    };

    public static Models.JobRole GetRoleForJob(string job)
    {
        return job switch
        {
            "Paladin" or "Warrior" or "Dark Knight" or "Gunbreaker" => Models.JobRole.Tank,
            "White Mage" or "Scholar" or "Astrologian" or "Sage" => Models.JobRole.Healer,
            "Dragoon" or "Monk" or "Ninja" or "Samurai" or "Reaper" or "Viper" => Models.JobRole.MeleeDPS,
            "Bard" or "Machinist" or "Dancer" => Models.JobRole.RangedDPS,
            "Black Mage" or "Summoner" or "Red Mage" or "Blue Mage" => Models.JobRole.MagicDPS,
            _ => Models.JobRole.Unknown
        };
    }
}
