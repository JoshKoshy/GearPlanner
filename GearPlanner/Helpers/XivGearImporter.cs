using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using GearPlanner.Models;

namespace GearPlanner.Helpers;

/// <summary>
/// Handles importing custom gear sets from xivgear.app JSON exports.
/// </summary>
public class XivGearImporter
{
    /// <summary>
    /// Import a gear set from a xivgear.app JSON file.
    /// </summary>
    public static BiSSet? ImportFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Plugin.Log.Warning($"Import file not found: {filePath}");
                return null;
            }

            var content = File.ReadAllText(filePath);
            return ImportFromJson(content, Path.GetFileNameWithoutExtension(filePath));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to import gear set from file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Import a gear set from xivgear.app JSON string.
    /// Handles the xivgear export format which can vary.
    /// </summary>
    public static BiSSet? ImportFromJson(string jsonContent, string setName = "Imported Set")
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            var biSSet = new BiSSet
            {
                Name = setName,
                Items = new Dictionary<string, BiSItem>(),
                Food = 0,
                Description = "Imported from xivgear.app"
            };

            // xivgear.app exports can have different structures
            // Try to find the gear items in various possible locations
            var items = ExtractItemsFromJson(root);
            
            if (items.Count == 0)
            {
                Plugin.Log.Warning("No gear items found in xivgear JSON");
                return null;
            }

            // Parse items
            foreach (var kvp in items)
            {
                var slot = kvp.Key;
                var itemData = kvp.Value;

                if (itemData.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var itemId))
                {
                    var biSItem = new BiSItem
                    {
                        Id = itemId,
                        Materia = new List<BiSMateria>()
                    };

                    // Try to extract materia if present
                    if (itemData.TryGetProperty("materia", out var materiaElement))
                    {
                        if (materiaElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var materiaItem in materiaElement.EnumerateArray())
                            {
                                if (materiaItem.TryGetProperty("id", out var materiaIdElement) && 
                                    materiaIdElement.TryGetInt32(out var materiaId))
                                {
                                    bool locked = false;
                                    if (materiaItem.TryGetProperty("locked", out var lockedElement))
                                    {
                                        locked = lockedElement.GetBoolean();
                                    }

                                    biSItem.Materia.Add(new BiSMateria
                                    {
                                        Id = materiaId,
                                        Locked = locked
                                    });
                                }
                            }
                        }
                    }

                    biSSet.Items[slot] = biSItem;
                }
            }

            // Try to extract food
            if (root.TryGetProperty("food", out var foodElement) && foodElement.TryGetInt32(out var foodId))
            {
                biSSet.Food = foodId;
            }

            Plugin.Log.Information($"Successfully imported gear set '{setName}' with {biSSet.Items.Count} items");
            return biSSet;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to parse xivgear JSON: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extract gear items from various possible JSON structures in xivgear exports.
    /// </summary>
    private static Dictionary<string, JsonElement> ExtractItemsFromJson(JsonElement root)
    {
        var items = new Dictionary<string, JsonElement>();

        // Try direct items property
        if (root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in itemsElement.EnumerateObject())
            {
                items[prop.Name] = prop.Value;
            }
            return items;
        }

        // Try properties that might contain gear data
        var gearSlots = new[] 
        { 
            "Weapon", "Head", "Body", "Hand", "Legs", "Feet", "Ears", "Neck", "Wrist", 
            "RingLeft", "RingRight", "MainHand", "Hands", "Wrists", "Ring1", "Ring2"
        };

        foreach (var slot in gearSlots)
        {
            if (root.TryGetProperty(slot, out var slotElement) && 
                (slotElement.ValueKind == JsonValueKind.Object || slotElement.ValueKind == JsonValueKind.Array))
            {
                // Handle both direct object and array formats
                if (slotElement.ValueKind == JsonValueKind.Object)
                {
                    items[slot] = slotElement;
                }
                else if (slotElement.ValueKind == JsonValueKind.Array && slotElement.GetArrayLength() > 0)
                {
                    // If it's an array, take the first element (which might be the current gear)
                    var firstElement = slotElement[0];
                    if (firstElement.ValueKind == JsonValueKind.Object)
                    {
                        items[slot] = firstElement;
                    }
                }
            }
        }

        return items;
    }

    /// <summary>
    /// Validate that an imported set has required gear data.
    /// </summary>
    public static bool IsValidGearSet(BiSSet? set)
    {
        if (set == null || set.Items == null || set.Items.Count == 0)
            return false;

        // Should have at least some core slots
        var requiredSlots = new[] { "Weapon", "MainHand", "Head" };
        return set.Items.Keys.Any(k => requiredSlots.Contains(k));
    }
}
