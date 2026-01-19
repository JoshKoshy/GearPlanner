using System;
using System.Collections.Generic;

namespace GearPlanner.Models;

[System.Serializable]
public class RaidMember
{
    public string Name { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
    public JobRole Role { get; set; } = JobRole.Unknown;
    public Dictionary<string, GearPiece> Gear { get; set; } = new();
    public int BooksEarned { get; set; } = 0;
    public int TokensEarned { get; set; } = 0;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public Dictionary<int, int> BookAdjustments { get; set; } = new() { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } };
    public Dictionary<int, int> FloorClears { get; set; } = new() { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } };
    
    public RaidMember() { }
    
    public RaidMember(string name, string job, JobRole role)
    {
        Name = name;
        Job = job;
        Role = role;
        InitializeGear();
    }
    
    public void InitializeGear()
    {
        Gear.Clear();
        foreach (GearSlot slot in Enum.GetValues(typeof(GearSlot)))
        {
            Gear[slot.ToString()] = new GearPiece(slot);
        }
    }
    
    public int GetGearPiecesAtStatus(GearStatus status)
    {
        int count = 0;
        foreach (var piece in Gear.Values)
        {
            if (piece.CurrentStatus == status)
                count++;
        }
        return count;
    }
    
    public int GetBiSGearCount()
    {
        return GetGearPiecesAtStatus(GearStatus.BiS);
    }
}
