using System.Collections.Generic;
using GearPlanner.Models;

namespace GearPlanner.Helpers;

/// <summary>
/// Configuration for gear name patterns used to discover items from FFXIV game data.
/// Update these strings here when gear names change in future patches.
/// </summary>
public static class GearNamePatterns
{
    /// <summary>
    /// Maps GearSource categories to their corresponding item name patterns.
    /// A category can have multiple patterns (e.g., Trash has different names for different raid tiers).
    /// Used by ItemDiscoveryHelper to find items in the game database.
    /// </summary>
    public static Dictionary<GearSource, string[]> CategoryPatterns = new()
    {
        { GearSource.Savage, new string[] { "Grand Champion's" } },
        { GearSource.TomeUp, new string[] { "Augmented Bygone Brass" } },
        { GearSource.Crafted, new string[] { "Courtly Lover's" } },
        { GearSource.Tome, new string[] { "Bygone Brass" } },
        { GearSource.Prep, new string[] { "Runaway" } },
        { GearSource.Trash, new string[] { 
            "Historia", "Mistwake", "Babyface Champion's"
        } },
        { GearSource.Relic, new string[] { } }, // TODO: Add relic patterns when needed
        { GearSource.Catchup, new string[] { } }, // TODO: Add catchup patterns when needed
        { GearSource.None, new string[] { } },
    };

    /// <summary>
    /// Get all patterns for a specific gear source.
    /// </summary>
    public static string[] GetPatterns(GearSource source)
    {
        return CategoryPatterns.TryGetValue(source, out var patterns) ? patterns : new string[] { };
    }

    /// <summary>
    /// Get all patterns that have been configured (non-empty arrays).
    /// </summary>
    public static List<(GearSource Category, string[] Patterns)> GetConfiguredPatterns()
    {
        var result = new List<(GearSource, string[])>();
        foreach (var kvp in CategoryPatterns)
        {
            if (kvp.Value.Length > 0)
            {
                result.Add((kvp.Key, kvp.Value));
            }
        }
        return result;
    }
}

