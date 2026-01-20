using System;
using System.Collections.Generic;
using System.Linq;

namespace GearPlanner.Models;

[System.Serializable]
public class RaidTeam
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<GearSheet> Sheets { get; set; } = new();
    public int SelectedSheetIndex { get; set; } = 0;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public int TierNumber { get; set; } = 0; // Arcadion tier 1-4
    
    // Floor clears are team-wide, not per-sheet
    public int Floor1Clears { get; set; } = 0;
    public int Floor2Clears { get; set; } = 0;
    public int Floor3Clears { get; set; } = 0;
    public int Floor4Clears { get; set; } = 0;
    
    // Convenience properties for backward compatibility and default sheet access
    // JsonIgnore prevents this proxy property from being serialized, avoiding duplication
    [Newtonsoft.Json.JsonIgnore]
    public List<RaidMember> Members
    {
        get => SelectedSheetIndex >= 0 && SelectedSheetIndex < Sheets.Count ? Sheets[SelectedSheetIndex].Members : new();
        set
        {
            Plugin.Log.Debug($"[Members.set] Setting Members for sheet at index {SelectedSheetIndex}");
            if (SelectedSheetIndex >= 0 && SelectedSheetIndex < Sheets.Count)
            {
                Sheets[SelectedSheetIndex].Members = value;
                Plugin.Log.Debug($"[Members.set] Set {value.Count} members");
            }
        }
    }
    
    public RaidTeam() 
    {
        Plugin.Log.Debug($"[RaidTeam] Parameterless constructor called");
        // Don't add a default sheet here - this is called during JSON deserialization
        // and would duplicate sheets that already exist in the JSON
        // Cleanup will be called after deserialization in Plugin.cs
    }
    
    public RaidTeam(string name)
    {
        Plugin.Log.Debug($"[RaidTeam] Constructor called with name: {name}");
        Name = name;
        // Only add default Main sheet for newly created teams (not during deserialization)
        if (Sheets.Count == 0)
        {
            Plugin.Log.Debug($"[RaidTeam] Adding default Main sheet");
            Sheets.Add(new GearSheet("Main", new List<RaidMember>()));
            SelectedSheetIndex = 0;
        }
    }
    
    /// <summary>
    /// Removes duplicate members with the same name, keeping only the first occurrence.
    /// This is called after deserialization to clean up any duplicates.

    
    public void AddMember(RaidMember member)
    {
        var stackTrace = Environment.StackTrace;
        Plugin.Log.Debug($"[AddMember] Called for member '{member.Name}' in team '{Name}'");
        Plugin.Log.Debug($"[AddMember] Stack: {stackTrace.Split('\n')[1]}");
        
        if (!Members.Any(m => m.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Plugin.Log.Debug($"[AddMember] Adding member '{member.Name}' to team '{Name}'");
            Members.Add(member);
        }
        else
        {
            Plugin.Log.Warning($"[AddMember] Member '{member.Name}' already exists in team '{Name}', skipping");
        }
    }
    
    public void RemoveMember(string name)
    {
        var member = Members.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (member != null)
        {
            Members.Remove(member);
        }
    }
    
    public RaidMember? GetMember(string name)
    {
        return Members.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    
    public int GetTeamAverageGearLevel()
    {
        if (Members.Count == 0)
            return 0;
            
        return (int)Members.Average(m => m.GetBiSGearCount() * 100 / 12); // 12 gear slots total
    }
    
    public int GetMembersMissingBiS()
    {
        return Members.Count(m => m.GetBiSGearCount() < 12);
    }
}
