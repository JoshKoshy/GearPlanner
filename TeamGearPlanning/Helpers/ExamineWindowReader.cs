using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Runtime.CompilerServices;

namespace GearPlanner.Helpers
{
    /// <summary>
    /// Provides methods to read actual item IDs from the FFXIV Examine window.
    /// The Examine window displays real equipped items, which are stored in AgentInspect._items.
    /// </summary>
    public static class ExamineWindowReader
    {
        // Equipment slot mapping for the 13 examined item slots
        // Matches the order in AgentInspect._items array
        private static readonly (int SlotIndex, Models.GearSlot GearSlot)[] SlotMapping = new[]
        {
            (0, Models.GearSlot.Head),
            (1, Models.GearSlot.Body),
            (2, Models.GearSlot.Hands),
            (3, Models.GearSlot.Legs),
            (4, Models.GearSlot.Feet),
            (5, Models.GearSlot.Neck),
            (6, Models.GearSlot.Wrists),
            (7, Models.GearSlot.Ring1),
            (8, Models.GearSlot.Ring2),
            (9, Models.GearSlot.MainHand), // Waist/Belt
            (10, Models.GearSlot.MainHand), // Ear Left
            (11, Models.GearSlot.MainHand), // Ear Right
            (12, Models.GearSlot.MainHand)  // Mainhand Weapon
        };

        /// <summary>
        /// Checks if examination window has valid data ready to read
        /// </summary>
        /// <returns>True if examination data is valid and ready</returns>
        public static bool IsExaminationDataValid()
        {
            unsafe
            {
                var agentInspect = AgentInspect.Instance();
                if (agentInspect == null)
                {
                    Plugin.Log.Debug("ExamineWindowReader: AgentInspect is null");
                    return false;
                }

                // Status: 0 = Nothing, 1 = Fetching, 2 = Ready, 3 = Failed
                var status = agentInspect->FetchCharacterDataStatus;
                Plugin.Log.Debug($"ExamineWindowReader: Examination status = {status} (0=Nothing, 1=Fetching, 2=Ready, 3=Failed)");
                
                if (status == 2)
                {
                    Plugin.Log.Debug($"ExamineWindowReader: Examination data is ready");
                    return true;
                }
                
                if (status == 1)
                {
                    Plugin.Log.Debug($"ExamineWindowReader: Examination still fetching, please wait a moment and try again");
                    return false;
                }
                
                return false;
            }
        }

        /// <summary>
        /// Gets all equipped items from the currently examined player
        /// </summary>
        /// <returns>Dictionary mapping GearSlot to item ID, or empty dict if no data available</returns>
        public static Dictionary<Models.GearSlot, uint> GetExaminedPlayerEquipment()
        {
            var equipment = new Dictionary<Models.GearSlot, uint>();

            unsafe
            {
                var agentInspect = AgentInspect.Instance();

                if (agentInspect == null)
                {
                    Plugin.Log.Debug("ExamineWindowReader: AgentInspect not available");
                    return equipment;
                }

                // Direct offsets into AgentInspect where actual item IDs are stored
                // These offsets work for both HQ and non-HQ items
                var offsets = new (int Offset, Models.GearSlot Slot)[]
                {
                    (0x2A8, Models.GearSlot.MainHand),
                    (0x2E0, Models.GearSlot.Head),
                    (0x2FC, Models.GearSlot.Body),
                    (0x318, Models.GearSlot.Hands),
                    (0x334, Models.GearSlot.Legs),
                    (0x350, Models.GearSlot.Feet),
                    (0x36C, Models.GearSlot.Ears),
                    (0x388, Models.GearSlot.Neck),
                    (0x3A4, Models.GearSlot.Wrists),
                    (0x3C0, Models.GearSlot.Ring1),
                    (0x3DC, Models.GearSlot.Ring2),
                };

                // Read item IDs from the offsets
                foreach (var (offset, slot) in offsets)
                {
                    uint itemId = *(uint*)((nint)agentInspect + offset);
                    uint normalized = NormalizeItemId(itemId);
                    
                    if (normalized > 0)
                    {
                        equipment[slot] = normalized;
                        Plugin.Log.Debug($"ExamineWindowReader: {slot} = {normalized} {(itemId > 1000000 ? "(HQ)" : "")}");
                    }
                }

                Plugin.Log.Debug($"ExamineWindowReader: Retrieved {equipment.Count} equipped items");
                
                return equipment;
            }
        }

        /// <summary>
        /// Normalizes an item ID by removing the HQ offset (1000000) if present
        /// HQ items have 1000000 added to their base item ID
        /// </summary>
        private static uint NormalizeItemId(uint itemId)
        {
            const uint HQ_OFFSET = 1000000;
            
            // If the item ID is greater than HQ_OFFSET, it's an HQ item
            // Return the base item ID by subtracting the offset
            if (itemId > HQ_OFFSET)
                return itemId - HQ_OFFSET;
            
            return itemId;
        }

        /// <summary>
        /// Gets detailed item information including glamour data
        /// </summary>
        public static Dictionary<Models.GearSlot, (uint ItemId, uint GlamourItemId)> GetExaminedPlayerEquipmentDetailed()
        {
            var equipment = new Dictionary<Models.GearSlot, (uint, uint)>();

            unsafe
            {
                var agentInspect = AgentInspect.Instance();

                if (agentInspect == null || agentInspect->FetchCharacterDataStatus != 2)
                    return equipment;

                var items = agentInspect->Items;

                if (items[0].ItemId > 0)
                    equipment[Models.GearSlot.Head] = (items[0].ItemId, items[0].GlamourItemId);

                if (items[1].ItemId > 0)
                    equipment[Models.GearSlot.Body] = (items[1].ItemId, items[1].GlamourItemId);

                if (items[2].ItemId > 0)
                    equipment[Models.GearSlot.Hands] = (items[2].ItemId, items[2].GlamourItemId);

                if (items[3].ItemId > 0)
                    equipment[Models.GearSlot.Legs] = (items[3].ItemId, items[3].GlamourItemId);

                if (items[4].ItemId > 0)
                    equipment[Models.GearSlot.Feet] = (items[4].ItemId, items[4].GlamourItemId);

                if (items[5].ItemId > 0)
                    equipment[Models.GearSlot.Neck] = (items[5].ItemId, items[5].GlamourItemId);

                if (items[6].ItemId > 0)
                    equipment[Models.GearSlot.Wrists] = (items[6].ItemId, items[6].GlamourItemId);

                if (items[7].ItemId > 0)
                    equipment[Models.GearSlot.Ring1] = (items[7].ItemId, items[7].GlamourItemId);

                if (items[8].ItemId > 0)
                    equipment[Models.GearSlot.Ring2] = (items[8].ItemId, items[8].GlamourItemId);

                if (items[12].ItemId > 0)
                    equipment[Models.GearSlot.MainHand] = (items[12].ItemId, items[12].GlamourItemId);

                return equipment;
            }
        }

        /// <summary>
        /// Gets a single item ID from a specific slot
        /// </summary>
        /// <param name="slotIndex">0-12 for examination item slots</param>
        /// <returns>Item ID or 0 if empty/unavailable</returns>
        public static uint GetExaminedItemInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex > 12)
                return 0;

            unsafe
            {
                var agentInspect = AgentInspect.Instance();

                if (agentInspect == null || agentInspect->FetchCharacterDataStatus != 2)
                    return 0;

                var items = agentInspect->Items;
                return items[slotIndex].ItemId;
            }
        }

        /// <summary>
        /// Dumps full diagnostic information about examination data
        /// </summary>
        public static void DumpExaminationDiagnostics()
        {
            Plugin.Log.Information("=== EXAMINATION WINDOW DIAGNOSTICS ===");
            
            unsafe
            {
                var agentInspect = AgentInspect.Instance();

                if (agentInspect == null)
                {
                    Plugin.Log.Error("AgentInspect.Instance() returned null!");
                    return;
                }

                Plugin.Log.Information($"AgentInspect address: 0x{(nint)agentInspect:X}");
                Plugin.Log.Information($"CurrentEntityId: {agentInspect->CurrentEntityId}");

                Plugin.Log.Information("=== Equipment read from AgentInspect ===");
                
                var offsets = new (int Offset, string SlotName)[]
                {
                    (0x2A8, "MainHand"),
                    (0x2E0, "Head"),
                    (0x2FC, "Body"),
                    (0x318, "Hands"),
                    (0x334, "Legs"),
                    (0x350, "Feet"),
                    (0x36C, "Ears"),
                    (0x388, "Neck"),
                    (0x3A4, "Wrist"),
                    (0x3C0, "Ring1"),
                    (0x3DC, "Ring2"),
                };

                const uint HQ_OFFSET = 1000000;
                
                foreach (var (offset, slotName) in offsets)
                {
                    uint itemId = *(uint*)((nint)agentInspect + offset);
                    uint normalized = itemId > HQ_OFFSET ? itemId - HQ_OFFSET : itemId;
                    bool isHQ = itemId > HQ_OFFSET;
                    
                    if (normalized > 0)
                    {
                        Plugin.Log.Information($"  {slotName}: {normalized} {(isHQ ? "(HQ)" : "")}");
                    }
                }
            }
        }
    }
}
