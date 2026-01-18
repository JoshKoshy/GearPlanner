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
    /// Only syncs gear if the team member's job matches the player's current job.
    /// </summary>
    public static void SyncPlayerEquipmentToMember(RaidMember member, IGameInventory gameInventory)
    {
        try
        {
            // Get the player's current job
#pragma warning disable CS0618
            var playerCharacter = Plugin.ClientState.LocalPlayer;
#pragma warning restore CS0618
            if (playerCharacter == null)
            {
                Plugin.Log.Error("Cannot sync equipment: Player is not logged in");
                return;
            }

            // Get the player's current job class ID and convert to job name
            uint playerJobId = playerCharacter.ClassJob.RowId;
            var playerJobName = GetJobNameFromClassJobId(playerJobId);
            
            if (string.IsNullOrEmpty(playerJobName))
            {
                Plugin.Log.Error($"Cannot sync equipment: Unknown player job ID {playerJobId}");
                return;
            }

            Plugin.Log.Debug($"Player's current job: {playerJobName}");
            Plugin.Log.Debug($"Team member's job: {member.Job}");

            // Only allow syncing if the jobs match
            if (!playerJobName.Equals(member.Job, System.StringComparison.OrdinalIgnoreCase))
            {
                Plugin.Log.Warning($"Cannot sync equipment for {member.Name}: Player is {playerJobName} but team member is {member.Job}. Jobs must match to sync gear.");
                return;
            }

            var equippedItems = GetPlayerEquipment(gameInventory);
            
            // Get the member's job code for validation
            var jobCode = FFXIVJobs.GetJobAbbreviation(member.Job);
            
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

                // Validate that the team member's job can equip this item
                if (!ItemDiscoveryHelper.CanJobEquipItemById(itemId, jobCode, Plugin.DataManager))
                {
                    Plugin.Log.Debug($"Skipped syncing item {itemId} for {member.Name}: {member.Job} ({jobCode}) cannot equip this gear");
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
    /// Sync a targeted player's equipped gear to a team member.
    /// Reads from the Examine window (AgentInspect) which contains the examined character's equipment.
    /// Only syncs gear if the examined player's job matches the team member's job.
    /// </summary>
    public static void SyncTargetEquipmentToMember(RaidMember member, IGameInventory gameInventory)
    {
        try
        {
            // Get the local player
#pragma warning disable CS0618
            var localPlayer = Plugin.ClientState.LocalPlayer;
#pragma warning restore CS0618
            if (localPlayer == null)
            {
                Plugin.Log.Warning("Cannot sync equipment: Not logged in.");
                return;
            }

            // Get the Examine window data to determine the examined character's job
            unsafe
            {
                var agentInspect = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentInspect.Instance();
                if (agentInspect == null || agentInspect->CurrentEntityId == 0)
                {
                    Plugin.Log.Warning("Cannot sync equipment: No character is currently being examined. Please use the Examine window first.");
                    return;
                }

                uint currentEntityId = agentInspect->CurrentEntityId;
                Plugin.Log.Information($"[DIAGNOSTIC] Examined character EntityId from AgentInspect: {currentEntityId} (0x{currentEntityId:X})");

                // Try to find the examined character in the ObjectTable
                var examinedCharacter = Plugin.ObjectTable.FirstOrDefault(obj => obj.EntityId == currentEntityId);
                
                if (examinedCharacter != null && examinedCharacter is Dalamud.Game.ClientState.Objects.Types.ICharacter exChar)
                {
                    Plugin.Log.Information($"[DIAGNOSTIC] Found potential match: {exChar.Name} (EntityId: {examinedCharacter.EntityId} / 0x{examinedCharacter.EntityId:X})");
                }

                int tableCount = Plugin.ObjectTable.Count();
                Plugin.Log.Information($"[DIAGNOSTIC] ObjectTable contents ({tableCount} total):");
                int charCount = 0;
                foreach (var obj in Plugin.ObjectTable)
                {
                    if (obj is Dalamud.Game.ClientState.Objects.Types.ICharacter character)
                    {
                        string charName = character.Name.TextValue ?? character.Name.ToString();
                        Plugin.Log.Information($"  [{charCount}] {charName} - EntityId: {obj.EntityId} (0x{obj.EntityId:X})");
                        charCount++;
                    }
                }

                if (examinedCharacter == null)
                {
                    Plugin.Log.Warning("Cannot sync equipment: Could not find the examined character in the object table. Check logs above for available EntityIds.");
                    return;
                }

                var examinedPlayer = examinedCharacter as Dalamud.Game.ClientState.Objects.Types.ICharacter;
                if (examinedPlayer == null)
                {
                    Plugin.Log.Warning("Cannot sync equipment: Examined target is not a valid character.");
                    return;
                }

                var examinedPlayerName = examinedPlayer.Name.ToString();
                Plugin.Log.Information($"Syncing from examined player: {examinedPlayerName}");

                // Get the examined player's equipped items from the Examine window
                // Note: The examine window shows equipment from when the character was examined,
                // so the current job may differ. Per-item validation will ensure compatibility.
                Plugin.Log.Debug($"Reading equipment from Examine window for {examinedPlayerName}");
                var examinedEquippedItems = ExamineWindowReader.GetExaminedPlayerEquipment();

                // Get the member's job code for validation
                var jobCode = FFXIVJobs.GetJobAbbreviation(member.Job);
                
                // Collect rings separately for smart matching
                var ringsToSync = new Dictionary<GearSlot, uint>();

                foreach (var kvp in examinedEquippedItems)
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

                    // Validate that the team member's job can equip this item
                    bool canEquip = ItemDiscoveryHelper.CanJobEquipItemById(itemId, jobCode, Plugin.DataManager);
                    
                    if (!canEquip)
                    {
                        Plugin.Log.Warning($"Skipped syncing item {itemId} for {member.Name}: {member.Job} ({jobCode}) cannot equip this gear");
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

                Plugin.Log.Information($"Successfully synced equipment from player {examinedPlayerName} to {member.Name}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error syncing target equipment to member: {ex.Message}\n{ex.StackTrace}");
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
        var jobCode = FFXIVJobs.GetJobAbbreviation(member.Job);
        
        foreach (var kvp in ringsToSync)
        {
            var itemId = kvp.Value;
            
            // Validate that the job can equip this ring
            if (!ItemDiscoveryHelper.CanJobEquipItemById(itemId, jobCode, Plugin.DataManager))
            {
                Plugin.Log.Debug($"Skipped ring sync for {member.Name}: Item {itemId} cannot be equipped by {member.Job} ({jobCode})");
                continue;
            }
            
            var source = DetermineItemSource(itemId);
            ringItems.Add((kvp.Key, itemId, source));
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

    /// <summary>
    /// Convert a ClassJob ID from the game to the corresponding FFXIV job name.
    /// </summary>
    private static string? GetJobNameFromClassJobId(uint classJobId)
    {
        // Map of ClassJob IDs to job names (used in modern FFXIV)
        return classJobId switch
        {
            1 => "Gladiator",   // GLA - becomes Paladin
            2 => "Pugilist",    // PGL - becomes Monk
            3 => "Marauder",    // MRD - becomes Warrior
            4 => "Lancer",      // LNC - becomes Dragoon
            5 => "Archer",      // ARC - becomes Bard
            6 => "Conjurer",    // CNJ - becomes White Mage
            7 => "Thaumaturge", // THM - becomes Black Mage
            8 => "Carpenter",   // CRP - Crafter
            9 => "Blacksmith",  // BSM - Crafter
            10 => "Armorer",    // ARM - Crafter
            11 => "Goldsmith",  // GSM - Crafter
            12 => "Weaver",     // WVR - Crafter
            13 => "Leatherworker", // LTW - Crafter
            14 => "Alchemist",  // ALC - Crafter
            15 => "Culinarian", // CUL - Crafter
            16 => "Miner",      // MIN - Gatherer
            17 => "Botanist",   // BTN - Gatherer
            18 => "Fisher",     // FSH - Gatherer
            19 => "Paladin",    // PLD
            20 => "Monk",       // MNK
            21 => "Warrior",    // WAR
            22 => "Dragoon",    // DRG
            23 => "Bard",       // BRD
            24 => "White Mage",  // WHM
            25 => "Black Mage",  // BLM
            26 => "Arcanist",   // ACN - becomes Summoner/Scholar
            27 => "Summoner",   // SMN
            28 => "Scholar",    // SCH
            29 => "Rogue",      // ROG - becomes Ninja
            30 => "Ninja",      // NIN
            31 => "Machinist",  // MCH
            32 => "Dark Knight", // DRK
            33 => "Astrologian", // AST
            34 => "Samurai",    // SAM
            35 => "Red Mage",   // RDM
            36 => "Blue Mage",  // BLU
            37 => "Gunbreaker", // GNB
            38 => "Dancer",     // DNC
            39 => "Reaper",     // RPR
            40 => "Sage",       // SGE
            41 => "Viper",      // VPR
            42 => "Pictomancer", // PCT
            _ => null
        };
    }
}
