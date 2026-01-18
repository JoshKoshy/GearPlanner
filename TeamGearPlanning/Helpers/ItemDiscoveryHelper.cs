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
    private static readonly HashSet<string> LoggedClassJobCategoryTypes = new();

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
    /// Falls back to name-based detection if ClassJobCategory cannot be inspected.
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
                bool foundAnyJobRestrictions = false;
                
                // Iterate through all jobs and check if they can equip this item
                foreach (var kvp in jobCodeMap)
                {
                    uint jobRowId = kvp.Key;
                    string jobCode = kvp.Value;

                    // Check if this specific job can equip the item
                    // Only add if we successfully found a matching property (returns false only after property check)
                    bool canEquip = CanJobEquipItemDirect(classJobCategory, jobCode);
                    if (canEquip)
                    {
                        jobs.Add(jobCode);
                        foundAnyJobRestrictions = true;
                    }
                }
                
                // If we successfully found restrictions, return them
                if (foundAnyJobRestrictions && jobs.Count > 0)
                {
                    return jobs.ToArray();
                }
                
                // If we found a ClassJobCategory but couldn't parse it, fall through to name detection
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Error getting jobs from game data for item {item.RowId}: {ex.Message}");
        }

        // Fall back to name-based detection
        var detectedJobs = DetectJobsFromItemName(item.Name.ToString());
        return detectedJobs;
    }

    /// <summary>
    /// Check if a job can equip an item based on ClassJobCategory.
    /// ClassJobCategory may have boolean properties for each job, named by job abbreviation or other formats.
    /// Returns true if the job can equip it, or if we cannot determine (defaults to allowing).
    /// </summary>
    /// <summary>
    /// Check if a specific job can equip an item based on ClassJobCategory.
    /// Returns false if the property is not found (not "default to true").
    /// Only returns true if the property exists and is true.
    /// </summary>
    private static bool CanJobEquipItemDirect(ClassJobCategory classJobCategory, string jobCode)
    {
        try
        {
            var type = classJobCategory.GetType();
            
            // Look for property with the job code name (must include Instance flag)
            var property = type.GetProperty(jobCode, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (property != null && property.PropertyType == typeof(bool))
            {
                var value = property.GetValue(classJobCategory);
                bool result = value is bool boolValue && boolValue;
                return result;
            }
            
            // Property not found - return false (don't default to true)
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[CanJobEquipItemDirect] Exception for job '{jobCode}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if a specific job can equip an item based on ClassJobCategory.
    /// This is the old method used by CanJobEquipItemById - defaults to true on failure for lenient validation.
    /// </summary>
    public static bool CanJobEquipItem(ClassJobCategory classJobCategory, string jobCode)
    {
        try
        {
            var type = classJobCategory.GetType();
            
            // Look for property with the job code name (must include Instance flag)
            var property = type.GetProperty(jobCode, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (property != null && property.PropertyType == typeof(bool))
            {
                var value = property.GetValue(classJobCategory);
                bool result = value is bool boolValue && boolValue;
                Plugin.Log.Debug($"[CanJobEquipItem] Job '{jobCode}': {result}");
                return result;
            }
            
            // Property not found - default to allowing (lenient for items not in database)
            Plugin.Log.Debug($"[CanJobEquipItem] Job '{jobCode}': Property not found, defaulting to allowed");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[CanJobEquipItem] Exception for job '{jobCode}': {ex.Message}");
            // Default to allowing on exception
            return true;
        }
    }

    /// <summary>
    /// Check if a specific item can be equipped by a job using the ItemDatabase.
    /// This is more reliable than ClassJobCategory inspection since ItemDatabase
    /// already has job compatibility data populated from game discovery.
    /// </summary>
    public static bool CanJobEquipItemById(uint itemId, string jobCode, IDataManager dataManager)
    {
        try
        {
            // First, check ItemDatabase which has pre-populated job compatibility data
            var dbItem = ItemDatabase.GetItemById(itemId);
            if (dbItem != null && dbItem.Jobs != null && dbItem.Jobs.Length > 0)
            {
                // Item is in database with job data - check if this job can equip it
                bool canEquip = dbItem.Jobs.Contains(jobCode);
                Plugin.Log.Debug($"[CanJobEquipItemById] Item {itemId} in database for job '{jobCode}': {canEquip} (Jobs: {string.Join(",", dbItem.Jobs)})");
                return canEquip;
            }

            Plugin.Log.Debug($"[CanJobEquipItemById] Item {itemId} not in database or has no job data, checking game data for job '{jobCode}'");
            
            // Fallback to game data inspection if not in database
            var itemSheet = dataManager.GetExcelSheet<Item>();
            if (itemSheet == null)
            {
                Plugin.Log.Debug($"[CanJobEquipItemById] ItemSheet is null for item {itemId}");
                return true; // Assume valid if we can't check
            }
            
            var itemRow = itemSheet.FirstOrDefault(i => i.RowId == itemId);
            if (itemRow.RowId == 0)
            {
                Plugin.Log.Debug($"[CanJobEquipItemById] Item {itemId} not found in sheet");
                return true; // Assume valid if item not found
            }
            
            // If item has no ClassJobCategory restrictions, it's for all jobs
            if (itemRow.ClassJobCategory.RowId == 0)
            {
                Plugin.Log.Debug($"[CanJobEquipItemById] Item {itemId} has no ClassJobCategory restrictions");
                return true;
            }
            
            var classJobCategory = itemRow.ClassJobCategory.Value;
            var result = CanJobEquipItem(classJobCategory, jobCode);
            Plugin.Log.Debug($"[CanJobEquipItemById] Item {itemId} for job '{jobCode}': {result}");
            return result;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[CanJobEquipItemById] Exception for item {itemId}, job '{jobCode}': {ex.Message}\n{ex.StackTrace}");
            return true; // Assume valid on error
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

    /// <summary>
    /// Convert a model/set ID to an actual item ID by searching the item database.
    /// This is used when reading equipment from DrawData, which stores model IDs instead of item IDs.
    /// </summary>
    public static uint? GetItemIdFromModelId(IDataManager dataManager, uint modelId, GearSlot slot)
    {
        try
        {
            var itemSheet = dataManager.GetExcelSheet<Item>();
            if (itemSheet == null)
                return null;

            // In FFXIV, the CharacterArmor.Id from DrawData is the ITEM ID itself, not a model ID
            // We should check if this ID directly exists in the item sheet
            try
            {
                var directItem = itemSheet.GetRow(modelId);
                if (directItem.RowId > 0)
                {
                    Plugin.Log.Debug($"[GetItemIdFromModelId] Item {modelId} ({directItem.Name}) found directly in sheet");
                    return modelId;
                }
            }
            catch
            {
                // GetRow may throw if ID doesn't exist
            }

            // If not found directly, try searching by model fields
            // Equipment can be matched by multiple model-related fields
            foreach (var item in itemSheet)
            {
                if (item.RowId == 0 || item.RowId > 50000) // Skip very high ID items that might be special
                    continue;

                try
                {
                    // Check ModelMain
                    if ((uint)item.ModelMain == modelId)
                    {
                        Plugin.Log.Debug($"[GetItemIdFromModelId] Found item {item.RowId} ({item.Name}) via ModelMain");
                        return item.RowId;
                    }

                    // Some items might have the model in other fields
                    // Check if the item's model matches via other properties
                    if (item.ModelSub > 0 && (uint)item.ModelSub == modelId)
                    {
                        Plugin.Log.Debug($"[GetItemIdFromModelId] Found item {item.RowId} ({item.Name}) via ModelSub");
                        return item.RowId;
                    }
                }
                catch
                {
                    // Skip items where model access fails
                    continue;
                }
            }

            Plugin.Log.Debug($"[GetItemIdFromModelId] No item found with model ID {modelId}");
            return null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[GetItemIdFromModelId] Error converting model ID {modelId}: {ex.Message}");
            return null;
        }
    }
}
