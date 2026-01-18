using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace GearPlanner.Helpers;

public class BiSLibrary
{
    private Models.BiSLibraryData libraryData = new();

    public BiSLibrary(string pluginConfigPath)
    {
        // Load from embedded resource instead of file system
    }

    /// <summary>
    /// Load all BiS sets from the embedded resource JSON file.
    /// </summary>
    public void LoadBiSSets()
    {
        libraryData = new();

        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "GearPlanner.Data.BiSData.json";
            
            Plugin.Log.Information($"DEBUG: Attempting to load resource '{resourceName}'");
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Plugin.Log.Warning($"BiS data embedded resource not found: {resourceName}");
                    var resources = assembly.GetManifestResourceNames();
                    Plugin.Log.Warning($"Available resources: {string.Join(", ", resources)}");
                    return;
                }

                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();
                    Plugin.Log.Information($"DEBUG: Read {content.Length} bytes from resource");
                    Plugin.Log.Information($"DEBUG: First 200 chars: {content.Substring(0, Math.Min(200, content.Length))}");
                    
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var loaded = JsonSerializer.Deserialize<Models.BiSLibraryData>(content, options);
                    if (loaded != null)
                    {
                        libraryData = loaded;
                        Plugin.Log.Information($"DEBUG: Deserialized successfully. Jobs count: {libraryData.Jobs.Count}");
                        foreach (var kvp in libraryData.Jobs)
                        {
                            Plugin.Log.Information($"DEBUG: Job '{kvp.Key}' has {kvp.Value.Count} groups");
                            foreach (var group in kvp.Value)
                            {
                                Plugin.Log.Information($"DEBUG:   Group '{group.Name}' has {group.Sets.Count} sets");
                            }
                        }
                        int totalSets = libraryData.Jobs.Values.Sum(groups => groups.Sum(g => g.Sets.Count));
                        Plugin.Log.Information($"Loaded BiS library with {libraryData.Jobs.Count} jobs and {totalSets} total sets");
                    }
                    else
                    {
                        Plugin.Log.Warning("Failed to deserialize BiS data - loaded is null");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"Failed to load BiS data: {ex.Message}");
            Plugin.Log.Warning($"Stack trace: {ex.StackTrace}");
            libraryData = new();
        }
    }

    /// <summary>
    /// Get all BiS sets for a specific job (flattened from all groups).
    /// </summary>
    public List<Models.BiSSet> GetBiSSetsForJob(string jobCode)
    {
        Plugin.Log.Information($"DEBUG BiSLibrary: Looking for job code '{jobCode}'");
        Plugin.Log.Information($"DEBUG BiSLibrary: Available jobs: {string.Join(", ", libraryData.Jobs.Keys)}");
        
        if (!libraryData.Jobs.ContainsKey(jobCode))
        {
            Plugin.Log.Warning($"DEBUG BiSLibrary: Job code '{jobCode}' not found");
            return new();
        }

        var allSets = new List<Models.BiSSet>();
        foreach (var group in libraryData.Jobs[jobCode])
        {
            allSets.AddRange(group.Sets);
        }
        Plugin.Log.Information($"DEBUG BiSLibrary: Returning {allSets.Count} sets for job {jobCode}");
        return allSets;
    }

    /// <summary>
    /// Get a specific BiS set by job and name.
    /// </summary>
    public Models.BiSSet? GetBiSSet(string jobCode, string setName)
    {
        if (!libraryData.Jobs.ContainsKey(jobCode))
            return null;

        foreach (var group in libraryData.Jobs[jobCode])
        {
            var set = group.Sets.FirstOrDefault(s => s.Name == setName);
            if (set != null)
                return set;
        }
        return null;
    }

    /// <summary>
    /// Save the entire library to file (not applicable for embedded resource).
    /// </summary>
    public void SaveBiSSets()
    {
        Plugin.Log.Information("Cannot save BiS library - using embedded resource. Edit BiSData.json in the project directly.");
    }

    /// <summary>
    /// Add or update a BiS set in the library (not applicable for embedded resource).
    /// </summary>
    public void AddOrUpdateBiSSet(Models.BiSSet set)
    {
        Plugin.Log.Information("Cannot modify BiS library - using embedded resource. Edit BiSData.json in the project directly.");
    }

    /// <summary>
    /// Get all jobs that have BiS sets.
    /// </summary>
    public List<string> GetAvailableJobs()
    {
        return libraryData.Jobs.Keys.ToList();
    }

    /// <summary>
    /// Map xivgear slot names to our internal GearSlot names.
    /// </summary>
    public static string MapXivGearSlotToInternal(string xivGearSlot)
    {
        return xivGearSlot switch
        {
            "Weapon" => "MainHand",
            "Hand" => "Hands",
            "Wrist" => "Wrists",
            "RingLeft" => "Ring1",
            "RingRight" => "Ring2",
            _ => xivGearSlot
        };
    }

    /// <summary>
    /// Detect the gear source (Savage, Tome Up, etc.) from an item ID using game data.
    /// </summary>
    public static Models.GearSource DetectGearSourceFromItemId(int itemId)
    {
        // Use the GearSourceDetector which handles item name analysis and caching
        return GearSourceDetector.DetectGearSource(itemId);
    }

    /// <summary>
    /// Convert a source string from BiSData to a GearSource enum value.
    /// </summary>
    public static Models.GearSource StringToGearSource(string? sourceString)
    {
        if (string.IsNullOrWhiteSpace(sourceString))
            return Models.GearSource.None;

        return sourceString.ToLower() switch
        {
            "savage" => Models.GearSource.Savage,
            "tomeup" => Models.GearSource.TomeUp,
            "tome" => Models.GearSource.Tome,
            "catchup" => Models.GearSource.Catchup,
            "relic" => Models.GearSource.Relic,
            "crafted" => Models.GearSource.Crafted,
            "prep" => Models.GearSource.Prep,
            "trash" => Models.GearSource.Trash,
            "wow" => Models.GearSource.Wow,
            _ => Models.GearSource.None
        };
    }
}

