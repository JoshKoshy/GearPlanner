using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using GearPlanner.Models;

namespace GearPlanner.Helpers;

/// <summary>
/// Dynamic item database that discovers items from FFXIV game data.
/// Uses ItemDiscoveryHelper to load items at runtime instead of hardcoding them.
/// </summary>
public static class ItemDatabase
{
    public record Item(uint Id, string Name, GearSource Category, string[] Jobs, GearSlot? Slot);

    private static List<Item>? _cachedItems;
    private static bool _initialized = false;

    /// <summary>
    /// Initialize the item database from game data.
    /// Must be called after Dalamud services are available.
    /// </summary>
    public static void Initialize(IDataManager dataManager)
    {
        if (_initialized)
            return;

        try
        {
            // Discover all Grand Champion's items (Savage)
            var savageItems = ItemDiscoveryHelper.DiscoverItems(
                dataManager,
                "Grand Champion's",
                new Dictionary<uint, string[]>()
            );

            // Discover all Augmented Bygone Brass items (Tome Up)
            var tomeUpItems = ItemDiscoveryHelper.DiscoverItems(
                dataManager,
                "Augmented Bygone Brass",
                new Dictionary<uint, string[]>()
            );

            // Discover all Courtly Lover's items (Crafted)
            var craftedItems = ItemDiscoveryHelper.DiscoverItems(
                dataManager,
                "Courtly Lover's",
                new Dictionary<uint, string[]>()
            );

            // Discover all Bygone Brass items (Tome)
            var tomeItems = ItemDiscoveryHelper.DiscoverItems(
                dataManager,
                "Bygone Brass",
                new Dictionary<uint, string[]>()
            );

            // Discover all Runaway items (Prep)
            var prepItems = ItemDiscoveryHelper.DiscoverItems(
                dataManager,
                "Runaway",
                new Dictionary<uint, string[]>()
            );

            // Combine and cache
            _cachedItems = new List<Item>();
            _cachedItems.AddRange(savageItems.Select(x => new Item(x.Id, x.Name, x.Category, x.Jobs, x.Slot)));
            _cachedItems.AddRange(tomeUpItems.Select(x => new Item(x.Id, x.Name, x.Category, x.Jobs, x.Slot)));
            _cachedItems.AddRange(craftedItems.Select(x => new Item(x.Id, x.Name, x.Category, x.Jobs, x.Slot)));
            _cachedItems.AddRange(tomeItems.Select(x => new Item(x.Id, x.Name, x.Category, x.Jobs, x.Slot)));
            _cachedItems.AddRange(prepItems.Select(x => new Item(x.Id, x.Name, x.Category, x.Jobs, x.Slot)));

            _initialized = true;
            Plugin.Log.Information($"ItemDatabase initialized with {_cachedItems.Count} items");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to initialize ItemDatabase: {ex.Message}");
            _cachedItems = new List<Item>();
            _initialized = true;
        }
    }

    /// <summary>
    /// Get all items from the database.
    /// </summary>
    public static List<Item> AllItems
    {
        get
        {
            if (!_initialized)
            {
                Plugin.Log.Warning("ItemDatabase not initialized - returning empty list");
                return new List<Item>();
            }
            return _cachedItems ?? new List<Item>();
        }
    }

    /// <summary>
    /// Look up an item by its ID.
    /// </summary>
    public static Item? GetItemById(uint id)
    {
        return AllItems.Find(item => item.Id == id);
    }

    /// <summary>
    /// Look up an item by its name (case-insensitive match).
    /// </summary>
    public static Item? GetItemByName(string name)
    {
        return AllItems.Find(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all items of a specific category.
    /// </summary>
    public static List<Item> GetItemsByCategory(GearSource category)
    {
        return AllItems.FindAll(item => item.Category == category);
    }

    /// <summary>
    /// Get all items for a specific job.
    /// </summary>
    public static List<Item> GetItemsByJob(string jobCode)
    {
        return AllItems.FindAll(item => item.Jobs.Contains(jobCode));
    }

    /// <summary>
    /// Get all items for multiple jobs.
    /// </summary>
    public static List<Item> GetItemsByJobs(params string[] jobCodes)
    {
        var jobSet = new HashSet<string>(jobCodes);
        return AllItems.FindAll(item => item.Jobs.Any(job => jobSet.Contains(job)));
    }

    /// <summary>
    /// Get items by both category and job.
    /// </summary>
    public static List<Item> GetItemsByCategoryAndJob(GearSource category, string jobCode)
    {
        return AllItems.FindAll(item => item.Category == category && item.Jobs.Contains(jobCode));
    }

    /// <summary>
    /// Get item count by category.
    /// </summary>
    public static int GetCountByCategory(GearSource category)
    {
        return AllItems.FindAll(item => item.Category == category).Count;
    }

    /// <summary>
    /// Get item count for a specific job.
    /// </summary>
    public static int GetCountByJob(string jobCode)
    {
        return AllItems.FindAll(item => item.Jobs.Contains(jobCode)).Count;
    }
}
