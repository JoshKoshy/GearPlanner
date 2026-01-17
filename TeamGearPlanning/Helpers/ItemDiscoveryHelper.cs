using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TeamGearPlanning.Models;

namespace TeamGearPlanning.Helpers;

/// <summary>
/// Helper class to discover items from FFXIV game data using Dalamud's IDataManager.
/// This allows dynamic discovery of new items without requiring code updates.
/// </summary>
public static class ItemDiscoveryHelper
{
    /// <summary>
    /// Discover all items matching a search pattern and extract their job associations.
    /// </summary>
    /// <param name="dataManager">Dalamud's IDataManager service</param>
    /// <param name="searchPattern">Name pattern to search for (e.g., "Grand Champion's")</param>
    /// <param name="jobAssignments">Dictionary mapping item ID to job codes</param>
    /// <returns>List of discovered items with their metadata</returns>
    public static List<(uint Id, string Name, GearSource Category, string[] Jobs)> DiscoverItems(
        IDataManager dataManager,
        string searchPattern,
        Dictionary<uint, string[]> jobAssignments)
    {
        var results = new List<(uint, string, GearSource, string[])>();

        try
        {
            var itemSheet = dataManager.GetExcelSheet<Item>();
            var classJobSheet = dataManager.GetExcelSheet<ClassJob>();
            
            if (itemSheet == null || classJobSheet == null)
                return results;

            // Build a map of ClassJob RowId to job codes
            var jobCodeMap = new Dictionary<uint, string>();
            foreach (var classJob in classJobSheet)
            {
                if (classJob.RowId == 0 || string.IsNullOrWhiteSpace(classJob.Abbreviation.ToString()))
                    continue;
                jobCodeMap[classJob.RowId] = classJob.Abbreviation.ToString();
            }

            foreach (var item in itemSheet)
            {
                // Skip invalid items
                if (item.RowId == 0 || string.IsNullOrWhiteSpace(item.Name.ToString()))
                    continue;

                var itemName = item.Name.ToString();

                // Check if item matches search pattern
                if (!itemName.Contains(searchPattern, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Determine gear category from item name patterns
                var category = DetermineCategoryFromName(itemName);

                // Get job associations from game data
                var jobs = jobAssignments.ContainsKey(item.RowId)
                    ? jobAssignments[item.RowId]
                    : GetJobsFromGameData(item, jobCodeMap);

                results.Add((item.RowId, itemName, category, jobs));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error discovering items: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Get job associations directly from game data using ClassJobCategory.
    /// </summary>
    private static string[] GetJobsFromGameData(Item item, Dictionary<uint, string> jobCodeMap)
    {
        var jobs = new HashSet<string>();

        try
        {
            // Check if item has ClassJobCategory restrictions
            if (item.ClassJobCategory.RowId > 0)
            {
                var classJobCategory = item.ClassJobCategory.Value;
                
                // Iterate through all jobs and check if they can equip this item
                foreach (var kvp in jobCodeMap)
                {
                    uint jobRowId = kvp.Key;
                    string jobCode = kvp.Value;

                    // Check if this specific job can equip the item
                    if (CanJobEquipItem(classJobCategory, jobCode))
                    {
                        jobs.Add(jobCode);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Error getting jobs from game data for item {item.RowId}: {ex.Message}");
        }

        // Fall back to name-based detection if no jobs found
        if (jobs.Count == 0)
        {
            return DetectJobsFromItemName(item.Name.ToString());
        }

        return jobs.ToArray();
    }

    /// <summary>
    /// Check if a job can equip an item based on ClassJobCategory.
    /// ClassJobCategory has boolean properties for each job abbreviation.
    /// </summary>
    private static bool CanJobEquipItem(ClassJobCategory classJobCategory, string jobCode)
    {
        try
        {
            var type = classJobCategory.GetType();
            var property = type.GetProperty(jobCode, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public);
            
            if (property != null && property.PropertyType == typeof(bool))
            {
                var value = property.GetValue(classJobCategory);
                return value is bool boolValue && boolValue;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determine the gear category (Savage vs TomeUp) based on item name patterns.
    /// </summary>
    private static GearSource DetermineCategoryFromName(string itemName)
    {
        if (itemName.Contains("Grand Champion's", StringComparison.OrdinalIgnoreCase))
            return GearSource.Savage;
        
        if (itemName.Contains("Augmented Bygone Brass", StringComparison.OrdinalIgnoreCase))
            return GearSource.TomeUp;

        if (itemName.Contains("Bygone Brass", StringComparison.OrdinalIgnoreCase))
            return GearSource.Tome;

        if (itemName.Contains("Courtly Lover's", StringComparison.OrdinalIgnoreCase))
            return GearSource.Crafted;

        if (itemName.Contains("Runaway", StringComparison.OrdinalIgnoreCase))
            return GearSource.Prep;

        // Default to Savage if uncertain
        return GearSource.Savage;
    }

    /// <summary>
    /// Detect job associations from item name patterns.
    /// </summary>
    private static string[] DetectJobsFromItemName(string itemName)
    {
        var jobs = new HashSet<string>();

        // Role-based patterns (must come first for armor pieces)
        if (itemName.Contains("Fending", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "PLD", "WAR", "DRK", "GNB" });
            return jobs.ToArray();
        }
        if (itemName.Contains("Maiming", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "DRG", "RPR" });
            return jobs.ToArray();
        }
        if (itemName.Contains("Striking", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "MNK", "SAM" });
            return jobs.ToArray();
        }
        if (itemName.Contains("Scouting", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "NIN", "VPR" });
            return jobs.ToArray();
        }
        if (itemName.Contains("Aiming", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "DNC", "BRD", "MCH" });
            return jobs.ToArray();
        }
        if (itemName.Contains("Healing", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "WHM", "SCH", "AST", "SGE" });
            return jobs.ToArray();
        }
        if (itemName.Contains("Casting", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "BLM", "SMN", "RDM", "PCT" });
            return jobs.ToArray();
        }
        if (itemName.Contains("Slaying", StringComparison.OrdinalIgnoreCase))
        {
            jobs.UnionWith(new[] { "MNK", "SAM", "RPR" });
            return jobs.ToArray();
        }

        // Weapon-specific patterns (for weapons and shields)
        string lowerName = itemName.ToLower();
        
        if (lowerName.Contains("falchion")) { jobs.Add("PLD"); }
        if (lowerName.Contains("war axe")) { jobs.Add("WAR"); }
        if (lowerName.Contains("guillotine")) { jobs.Add("DRK"); }
        if (lowerName.Contains("sawback")) { jobs.Add("GNB"); }
        if (lowerName.Contains("cane")) { jobs.Add("WHM"); }
        if (lowerName.Contains("rod")) { jobs.Add("BLM"); }
        if (lowerName.Contains("index")) { jobs.Add("SMN"); }
        if (lowerName.Contains("codex")) { jobs.Add("SCH"); }
        if (lowerName.Contains("knives")) { jobs.Add("NIN"); }
        if (lowerName.Contains("pistol")) { jobs.Add("MCH"); }
        if (lowerName.Contains("astrometer")) { jobs.Add("AST"); }
        if (lowerName.Contains("blade") && !lowerName.Contains("twinfang")) { jobs.Add("SAM"); }
        if (lowerName.Contains("foil")) { jobs.Add("RDM"); }
        if (lowerName.Contains("longbow")) { jobs.Add("BRD"); }
        if (lowerName.Contains("baghnakhs")) { jobs.Add("MNK"); }
        if (lowerName.Contains("scythe") || lowerName.Contains("war scythe")) { jobs.Add("RPR"); }
        if (lowerName.Contains("quoits")) { jobs.Add("DNC"); }
        if (lowerName.Contains("syrinxi")) { jobs.Add("SGE"); }
        if (lowerName.Contains("twinfang")) { jobs.Add("VPR"); }
        if (lowerName.Contains("brush")) { jobs.Add("PCT"); }
        if (lowerName.Contains("kite shield")) { jobs.Add("PLD"); }
        if (lowerName.Contains("sainti")) { jobs.Add("MNK"); }

        return jobs.Count > 0 ? jobs.ToArray() : Array.Empty<string>();
    }

    /// <summary>
    /// Print discovered items to the plugin log.
    /// </summary>
    public static void LogDiscoveredItems(List<(uint Id, string Name, GearSource Category, string[] Jobs)> items)
    {
        Plugin.Log.Information($"Discovered {items.Count} items:");
        foreach (var item in items.OrderBy(x => x.Id))
        {
            var jobsStr = string.Join(", ", item.Jobs);
            Plugin.Log.Information($"  {item.Id}: {item.Name} ({item.Category}) - Jobs: [{jobsStr}]");
        }
    }
}
