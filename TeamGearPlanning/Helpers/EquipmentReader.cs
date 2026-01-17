using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using TeamGearPlanning.Models;

namespace TeamGearPlanning.Helpers;

/// <summary>
/// Helper class to read the player's currently equipped gear from the game.
/// </summary>
public static class EquipmentReader
{
    /// <summary>
    /// Get the player's currently equipped items for all gear slots.
    /// Returns a dictionary mapping GearSlot to item ID.
    /// </summary>
    public static Dictionary<GearSlot, uint> GetPlayerEquipment(IGameInventory gameInventory)
    {
        var equipment = new Dictionary<GearSlot, uint>();

        try
        {
            // Get equipped items from the game inventory
            var equippedItems = gameInventory.GetInventoryItems(GameInventoryType.EquippedItems);

            // Map equipment array indices to GearSlot enum
            // FFXIV equipment array order (correct):
            // 0=MainHand, 1=OffHand, 2=Head, 3=Body, 4=Hands, 5=Waist, 6=Legs, 7=Feet,
            // 8=Wrists, 9=Neck, 10=Ring1, 11=Ring2, 12=SoulCrystal
            var slotMappingByIndex = new GearSlot?[]
            {
                GearSlot.MainHand,    // 0
                null,                 // 1 - OffHand (skip)
                GearSlot.Head,        // 2
                GearSlot.Body,        // 3
                GearSlot.Hands,       // 4
                null,                 // 5 - Waist (skip)
                GearSlot.Legs,        // 6
                GearSlot.Feet,        // 7
                GearSlot.Ears,      // 8
                GearSlot.Neck,        // 9
                GearSlot.Wrists,       // 10
                GearSlot.Ring1,       // 11
                GearSlot.Ring2         // 12 - SoulCrystal
            };

            for (int idx = 0; idx < equippedItems.Length && idx < slotMappingByIndex.Length; idx++)
            {
                var item = equippedItems[idx];
                var gearSlot = slotMappingByIndex[idx];

                // Skip unmapped slots
                if (gearSlot == null || item.IsEmpty || item.ItemId == 0)
                    continue;

                // HQ items have 1000000 added to their item ID
                // Get the base item ID for lookups
                uint baseItemId = item.ItemId;
                if (item.IsHq && baseItemId > 1000000)
                {
                    baseItemId -= 1000000;
                    Plugin.Log.Debug($"Found [{idx}] {gearSlot}: ItemId {item.ItemId} (HQ) -> Base ItemId {baseItemId}");
                }
                else
                {
                    Plugin.Log.Debug($"Found [{idx}] {gearSlot}: ItemId {baseItemId}");
                }

                equipment[gearSlot.Value] = baseItemId;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error reading player equipment: {ex.Message}\n{ex.StackTrace}");
        }

        return equipment;
    }

    /// <summary>
    /// Determine the gear source for an item by checking ItemDatabase.
    /// Returns the category if found, otherwise GearSource.None.
    /// </summary>
    public static GearSource DetermineItemSource(uint itemId)
    {
        try
        {
            var item = ItemDatabase.GetItemById(itemId);
            if (item != null)
                return item.Category;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error determining item source for {itemId}: {ex.Message}");
        }

        return GearSource.None;
    }

    /// <summary>
    /// Sync the player's current equipped gear to a team member.
    /// This updates the "Current" gear fields with what they're actually wearing.
    /// </summary>
    public static void SyncPlayerEquipmentToMember(RaidMember member, IGameInventory gameInventory)
    {
        try
        {
            var equippedItems = GetPlayerEquipment(gameInventory);
            
            // Collect rings separately for smart matching
            var ringsToSync = new Dictionary<GearSlot, uint>();

            foreach (var kvp in equippedItems)
            {
                var slot = kvp.Key;
                var itemId = kvp.Value;
                var slotKey = slot.ToString();

                // Handle rings specially - match them to desired sources
                if (slot == GearSlot.Ring1 || slot == GearSlot.Ring2)
                {
                    ringsToSync[slot] = itemId;
                    continue;
                }

                if (!member.Gear.ContainsKey(slotKey))
                {
                    member.Gear[slotKey] = new GearPiece(slot);
                }

                var gearPiece = member.Gear[slotKey];
                gearPiece.CurrentItemId = itemId;
                gearPiece.Source = DetermineItemSource(itemId);

                Plugin.Log.Information($"Synced {slot}: Item {itemId}, Source {gearPiece.Source}");
            }

            // Now handle rings with smart matching
            if (ringsToSync.Count > 0)
            {
                SyncRingsToMember(member, ringsToSync);
            }

            Plugin.Log.Information($"Successfully synced equipment for {member.Name}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error syncing equipment to member: {ex.Message}");
        }
    }

    /// <summary>
    /// Smart ring syncing that matches ring sources to desired sources.
    /// </summary>
    public static void SyncRingsToMember(RaidMember member, Dictionary<GearSlot, uint> ringsToSync)
    {
        // Get the desired sources for both ring slots
        var ring1Key = GearSlot.Ring1.ToString();
        var ring2Key = GearSlot.Ring2.ToString();

        var ring1DesiredSource = member.Gear.ContainsKey(ring1Key) 
            ? member.Gear[ring1Key].DesiredSource 
            : GearSource.None;
        var ring2DesiredSource = member.Gear.ContainsKey(ring2Key) 
            ? member.Gear[ring2Key].DesiredSource 
            : GearSource.None;

        // Determine sources of the rings we're syncing
        var ringItems = new List<(GearSlot slot, uint itemId, GearSource source)>();
        foreach (var kvp in ringsToSync)
        {
            var source = DetermineItemSource(kvp.Value);
            ringItems.Add((kvp.Key, kvp.Value, source));
        }

        // Match rings to slots based on desired source
        // Try to match ring1 first
        var ring1Item = ringItems.FirstOrDefault(r => r.source == ring1DesiredSource);
        
        // If ring1 found a match, give ring2 the other ring
        var ring2Item = default((GearSlot slot, uint itemId, GearSource source));
        if (ring1Item.itemId > 0 && ringItems.Count > 1)
        {
            // Give ring2 the ring that wasn't matched to ring1
            ring2Item = ringItems.First(r => r.itemId != ring1Item.itemId);
        }
        else if (ring1Item.itemId == 0)
        {
            // Ring1 didn't find a match, try to match ring2
            ring2Item = ringItems.FirstOrDefault(r => r.source == ring2DesiredSource);
            
            // If ring2 found a match, give ring1 the other ring
            if (ring2Item.itemId > 0 && ringItems.Count > 1)
            {
                ring1Item = ringItems.First(r => r.itemId != ring2Item.itemId);
            }
            else if (ring2Item.itemId == 0)
            {
                // Neither matched by desired source, just use them in order
                if (ringItems.Count > 0)
                    ring1Item = ringItems[0];
                if (ringItems.Count > 1)
                    ring2Item = ringItems[1];
            }
        }

        // Sync ring 1
        if (ring1Item.itemId > 0)
        {
            if (!member.Gear.ContainsKey(ring1Key))
                member.Gear[ring1Key] = new GearPiece(GearSlot.Ring1);

            member.Gear[ring1Key].CurrentItemId = ring1Item.itemId;
            member.Gear[ring1Key].Source = ring1Item.source;
            Plugin.Log.Information($"Synced Ring1: Item {ring1Item.itemId}, Source {ring1Item.source}");
        }

        // Sync ring 2
        if (ring2Item.itemId > 0)
        {
            if (!member.Gear.ContainsKey(ring2Key))
                member.Gear[ring2Key] = new GearPiece(GearSlot.Ring2);

            member.Gear[ring2Key].CurrentItemId = ring2Item.itemId;
            member.Gear[ring2Key].Source = ring2Item.source;
            Plugin.Log.Information($"Synced Ring2: Item {ring2Item.itemId}, Source {ring2Item.source}");
        }
    }
}
