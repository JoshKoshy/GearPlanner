using System;
using System.Collections.Generic;
using System.Linq;
using TeamGearPlanning.Models;

namespace TeamGearPlanning.Helpers;

/// <summary>
/// Detects gear source (Savage, Tome Up, etc.) from item IDs using static mappings and game data.
/// </summary>
public class GearSourceDetector
{
    // Static mapping of known item IDs to their gear sources
    // Based on xivapi.com item data analysis (133 items from BiS data)
    // Savage gear: "Grand Champion's" items (59 items, Raid Tier 3)
    // Tome Up gear: "Augmented Bygone Brass" items (38 items, tomestone upgrades)
    private static readonly Dictionary<uint, GearSource> ItemSourceMap = new()
    {
        // Savage items (Grand Champion's) - 59 items
        // Weapons
        { 49658u, GearSource.Savage }, // Falchion
        { 49659u, GearSource.Savage }, // Baghnakhs
        { 49660u, GearSource.Savage }, // War Axe
        { 49662u, GearSource.Savage }, // Longbow
        { 49663u, GearSource.Savage }, // Cane
        { 49664u, GearSource.Savage }, // Rod
        { 49665u, GearSource.Savage }, // Index
        { 49666u, GearSource.Savage }, // Codex
        { 49667u, GearSource.Savage }, // Knives
        { 49668u, GearSource.Savage }, // Guillotine
        { 49669u, GearSource.Savage }, // Pistol
        { 49670u, GearSource.Savage }, // Astrometer
        { 49671u, GearSource.Savage }, // Blade
        { 49672u, GearSource.Savage }, // Foil
        { 49673u, GearSource.Savage }, // Sawback
        { 49674u, GearSource.Savage }, // War Quoits
        { 49675u, GearSource.Savage }, // War Scythe
        { 49676u, GearSource.Savage }, // Syrinxi
        { 49677u, GearSource.Savage }, // Twinfangs
        { 49678u, GearSource.Savage }, // Flat Brush
        { 49679u, GearSource.Savage }, // Kite Shield
        // Armor
        { 49680u, GearSource.Savage }, // Headgear of Fending
        { 49682u, GearSource.Savage }, // Gloves of Fending
        { 49683u, GearSource.Savage }, // Breeches of Fending
        { 49684u, GearSource.Savage }, // Boots of Fending
        { 49686u, GearSource.Savage }, // Jacket of Maiming
        { 49689u, GearSource.Savage }, // Boots of Maiming
        { 49690u, GearSource.Savage }, // Goggles of Striking
        { 49692u, GearSource.Savage }, // Gloves of Striking
        { 49693u, GearSource.Savage }, // Hose of Striking
        { 49694u, GearSource.Savage }, // Sabatons of Striking
        { 49696u, GearSource.Savage }, // Coat of Aiming
        { 49697u, GearSource.Savage }, // Gloves of Aiming
        { 49700u, GearSource.Savage }, // Goggles of Scouting
        { 49703u, GearSource.Savage }, // Hose of Scouting
        { 49705u, GearSource.Savage }, // Headgear of Healing
        { 49707u, GearSource.Savage }, // Gloves of Healing
        { 49708u, GearSource.Savage }, // Breeches of Healing
        { 49709u, GearSource.Savage }, // Sabatons of Healing
        { 49710u, GearSource.Savage }, // Headgear of Casting
        { 49711u, GearSource.Savage }, // Coat of Casting
        { 49712u, GearSource.Savage }, // Gloves of Casting
        { 49713u, GearSource.Savage }, // Breeches of Casting
        { 49714u, GearSource.Savage }, // Sabatons of Casting
        // Accessories
        { 49715u, GearSource.Savage }, // Ear Cuff of Fending
        { 49717u, GearSource.Savage }, // Ear Cuff of Aiming
        { 49718u, GearSource.Savage }, // Ear Cuff of Healing
        { 49719u, GearSource.Savage }, // Ear Cuff of Casting
        { 49721u, GearSource.Savage }, // Neckband of Slaying
        { 49722u, GearSource.Savage }, // Neckband of Aiming
        { 49723u, GearSource.Savage }, // Neckband of Healing
        { 49724u, GearSource.Savage }, // Neckband of Casting
        { 49725u, GearSource.Savage }, // Bracelets of Fending
        { 49726u, GearSource.Savage }, // Bracelets of Slaying
        { 49729u, GearSource.Savage }, // Bracelets of Casting
        { 49730u, GearSource.Savage }, // Ring of Fending
        { 49731u, GearSource.Savage }, // Ring of Slaying
        { 49732u, GearSource.Savage }, // Ring of Aiming
        { 49733u, GearSource.Savage }, // Ring of Healing
        { 49734u, GearSource.Savage }, // Ring of Casting

        // Tome Up items (Augmented Bygone Brass) - 38 items
        // Weapons
        { 49582u, GearSource.TomeUp }, // Sainti
        // Armor
        { 49604u, GearSource.TomeUp }, // Coat of Fending
        { 49607u, GearSource.TomeUp }, // Greaves of Fending
        { 49608u, GearSource.TomeUp }, // Cap of Maiming
        { 49610u, GearSource.TomeUp }, // Gloves of Maiming
        { 49611u, GearSource.TomeUp }, // Brais of Maiming
        { 49613u, GearSource.TomeUp }, // Cap of Striking
        { 49614u, GearSource.TomeUp }, // Jacket of Striking
        { 49617u, GearSource.TomeUp }, // Sabatons of Striking
        { 49618u, GearSource.TomeUp }, // Top Hat of Aiming
        { 49621u, GearSource.TomeUp }, // Gaskins of Aiming
        { 49622u, GearSource.TomeUp }, // Boots of Aiming
        { 49624u, GearSource.TomeUp }, // Jacket of Scouting
        { 49625u, GearSource.TomeUp }, // Clawtips of Scouting
        { 49627u, GearSource.TomeUp }, // Sabatons of Scouting
        { 49629u, GearSource.TomeUp }, // Shirt of Healing
        { 49630u, GearSource.TomeUp }, // Gloves of Healing
        { 49633u, GearSource.TomeUp }, // Calot of Casting
        { 49634u, GearSource.TomeUp }, // Shirt of Casting
        { 49635u, GearSource.TomeUp }, // Gloves of Casting
        { 49636u, GearSource.TomeUp }, // Halfslops of Casting
        { 49637u, GearSource.TomeUp }, // Boots of Casting
        // Accessories
        { 49639u, GearSource.TomeUp }, // Earrings of Slaying
        { 49640u, GearSource.TomeUp }, // Earrings of Aiming
        { 49641u, GearSource.TomeUp }, // Earrings of Healing
        { 49642u, GearSource.TomeUp }, // Earrings of Casting
        { 49643u, GearSource.TomeUp }, // Choker of Fending
        { 49644u, GearSource.TomeUp }, // Choker of Slaying
        { 49647u, GearSource.TomeUp }, // Choker of Casting
        { 49648u, GearSource.TomeUp }, // Bracelet of Fending
        { 49650u, GearSource.TomeUp }, // Bracelet of Aiming
        { 49651u, GearSource.TomeUp }, // Bracelet of Healing
        { 49652u, GearSource.TomeUp }, // Bracelet of Casting
        { 49653u, GearSource.TomeUp }, // Ring of Fending
        { 49654u, GearSource.TomeUp }, // Ring of Slaying
        { 49655u, GearSource.TomeUp }, // Ring of Aiming
        { 49656u, GearSource.TomeUp }, // Ring of Healing
        { 49657u, GearSource.TomeUp }, // Ring of Casting
    };

    // Dynamic cache for items not in the static map
    private static Dictionary<uint, GearSource> itemSourceCache = new();

    /// <summary>
    /// Detect the gear source from an item ID.
    /// Checks static map first, then dynamic cache, then falls back to game data lookup.
    /// </summary>
    public static GearSource DetectGearSource(int itemId)
    {
        try
        {
            uint uitemId = (uint)itemId;
            
            // Check static map first (instant lookup for known items)
            if (ItemSourceMap.ContainsKey(uitemId))
            {
                return ItemSourceMap[uitemId];
            }
            
            // Check dynamic cache
            if (itemSourceCache.ContainsKey(uitemId))
            {
                return itemSourceCache[uitemId];
            }

            // Try to detect from game data for unmapped items
            var source = DetectFromGameData(uitemId);
            
            // Cache the result
            itemSourceCache[uitemId] = source;
            
            return source;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"Error detecting gear source for item {itemId}: {ex.Message}");
            return GearSource.None;
        }
    }

    /// <summary>
    /// Detect gear source by looking up the item in game data and analyzing its name and properties.
    /// </summary>
    private static GearSource DetectFromGameData(uint itemId)
    {
        try
        {
            // Get the Item sheet via reflection
            var method = typeof(Dalamud.Plugin.Services.IDataManager)
                .GetMethod("GetExcelSheet", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (method == null)
                return GearSource.None;

            var luminaItemType = Type.GetType("Lumina.Excel.GeneratedSheets.Item");
            if (luminaItemType == null)
                return GearSource.None;

            var genericMethod = method.MakeGenericMethod(luminaItemType);
            dynamic? itemSheet = genericMethod.Invoke(Plugin.DataManager, null);
            
            if (itemSheet == null)
                return GearSource.None;

            dynamic? item = itemSheet.GetRow(itemId);
            if (item == null)
                return GearSource.None;

            // Get the item name
            string itemName = item.Name?.RawString ?? "";
            
            Plugin.Log.Debug($"Item {itemId}: {itemName}");

            // Detect based on item name patterns
            if (itemName.Contains("Grand Champion's"))
                return GearSource.Savage;
            
            if (itemName.Contains("Augmented Bygone Brass"))
                return GearSource.TomeUp;
            
            if (itemName.Contains("Augmented") && itemName.Contains("Brass"))
                return GearSource.TomeUp;
            
            // Get item level as fallback
            uint itemLevel = item.LevelItem?.Row ?? 0;
            
            Plugin.Log.Debug($"Item {itemId} ({itemName}) has ilvl {itemLevel}");
            
            // Fallback to ilvl detection
            if (itemLevel >= 665)
                return GearSource.Savage;
            else if (itemLevel >= 660)
                return GearSource.TomeUp;
            else if (itemLevel >= 655)
                return GearSource.Tome;

            return GearSource.None;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Error detecting from game data: {ex.Message}");
            return GearSource.None;
        }
    }

    /// <summary>
    /// Clear the dynamic cache (useful for reload scenarios).
    /// </summary>
    public static void ClearCache()
    {
        itemSourceCache.Clear();
    }
}
