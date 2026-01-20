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
    public List<RaidMember> Members
    {
        get => SelectedSheetIndex >= 0 && SelectedSheetIndex < Sheets.Count ? Sheets[SelectedSheetIndex].Members : new();
        set
        {
            if (SelectedSheetIndex >= 0 && SelectedSheetIndex < Sheets.Count)
            {
                Sheets[SelectedSheetIndex].Members = value;
            }
        }
    }
    
    public RaidTeam() 
    {
        // Initialize with default Main sheet
        if (Sheets.Count == 0)
        {
            Sheets.Add(new GearSheet("Main", new List<RaidMember>()));
            SelectedSheetIndex = 0;
        }
        CleanupDuplicateSheets();
    }
    
    public RaidTeam(string name)
    {
        Name = name;
        // Initialize with default Main sheet
        if (Sheets.Count == 0)
        {
            Sheets.Add(new GearSheet("Main", new List<RaidMember>()));
            SelectedSheetIndex = 0;
        }
        CleanupDuplicateSheets();
    }
    
    /// <summary>
    /// Removes duplicate sheets with the same name, keeping only the first occurrence.
    /// This is called after deserialization to clean up any duplicates.
    /// </summary>
    public void CleanupDuplicateSheets()
    {
        var sheetNames = new HashSet<string>();
        var sheetsToRemove = new List<GearSheet>();
        
        Plugin.Log.Debug($"[CleanupDuplicateSheets] Checking team '{Name}' with {Sheets.Count} sheets");
        
        foreach (var sheet in Sheets)
        {
            if (sheetNames.Contains(sheet.Name))
            {
                // This is a duplicate
                Plugin.Log.Warning($"[CleanupDuplicateSheets] Found duplicate sheet: '{sheet.Name}'");
                sheetsToRemove.Add(sheet);
            }
            else
            {
                Plugin.Log.Debug($"[CleanupDuplicateSheets] Sheet '{sheet.Name}' is unique");
                sheetNames.Add(sheet.Name);
            }
        }
        
        // Remove duplicates
        foreach (var sheet in sheetsToRemove)
        {
            Plugin.Log.Warning($"[CleanupDuplicateSheets] Removing duplicate sheet: '{sheet.Name}'");
            Sheets.Remove(sheet);
        }
        
        Plugin.Log.Debug($"[CleanupDuplicateSheets] Team '{Name}' now has {Sheets.Count} sheets");
        
        // Ensure SelectedSheetIndex is valid
        if (SelectedSheetIndex < 0 || SelectedSheetIndex >= Sheets.Count)
        {
            SelectedSheetIndex = Sheets.Count > 0 ? 0 : -1;
        }
    }
    
    public void AddMember(RaidMember member)
    {
        if (!Members.Any(m => m.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Members.Add(member);
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
