using System.Collections.Generic;

namespace GearPlanner.Helpers;

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
        "Blue Mage",
        "Pictomancer"
    };

    // Job abbreviations
    private static readonly Dictionary<string, string> JobAbbreviations = new()
    {
        { "Paladin", "PLD" },
        { "Warrior", "WAR" },
        { "Dark Knight", "DRK" },
        { "Gunbreaker", "GNB" },
        { "White Mage", "WHM" },
        { "Scholar", "SCH" },
        { "Astrologian", "AST" },
        { "Sage", "SGE" },
        { "Dragoon", "DRG" },
        { "Monk", "MNK" },
        { "Ninja", "NIN" },
        { "Samurai", "SAM" },
        { "Reaper", "RPR" },
        { "Viper", "VPR" },
        { "Bard", "BRD" },
        { "Machinist", "MCH" },
        { "Dancer", "DNC" },
        { "Black Mage", "BLM" },
        { "Summoner", "SMN" },
        { "Red Mage", "RDM" },
        { "Blue Mage", "BLU" },
        { "Pictomancer", "PCT" }
    };

    public static string[] GetAllJobOptions()
    {
        var options = new List<string> { "Tank", "Healer", "Melee", "Ranged", "Caster" };
        options.AddRange(AllJobs);
        return options.ToArray();
    }

    public static string GetJobDisplayName(string jobOrRole)
    {
        // For generic roles, return as-is
        if (jobOrRole is "Tank" or "Healer" or "Melee" or "Ranged" or "Caster")
            return jobOrRole;
        
        // For FFXIV jobs, return abbreviation
        return JobAbbreviations.TryGetValue(jobOrRole, out var abbr) ? abbr : jobOrRole;
    }

    public static string GetJobAbbreviation(string job)
    {
        return JobAbbreviations.TryGetValue(job, out var abbr) ? abbr : job;
    }

    public static Models.JobRole GetRoleForJob(string job)
    {
        return job switch
        {
            "Paladin" or "Warrior" or "Dark Knight" or "Gunbreaker" => Models.JobRole.Tank,
            "White Mage" or "Scholar" or "Astrologian" or "Sage" => Models.JobRole.Healer,
            "Dragoon" or "Monk" or "Ninja" or "Samurai" or "Reaper" or "Viper" => Models.JobRole.MeleeDPS,
            "Bard" or "Machinist" or "Dancer" => Models.JobRole.RangedDPS,
            "Black Mage" or "Summoner" or "Red Mage" or "Blue Mage" or "Pictomancer" => Models.JobRole.MagicDPS,
            _ => Models.JobRole.Unknown
        };
    }

    public static string GetJobCodeFromName(string jobName)
    {
        return JobAbbreviations.TryGetValue(jobName, out var code) ? code : jobName;
    }
}
