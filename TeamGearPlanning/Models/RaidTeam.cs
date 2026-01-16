using System;
using System.Collections.Generic;
using System.Linq;

namespace TeamGearPlanning.Models;

[System.Serializable]
public class RaidTeam
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RaidMember> Members { get; set; } = new();
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public int TierNumber { get; set; } = 0; // Arcadion tier 1-4
    public int Floor1Clears { get; set; } = 0;
    public int Floor2Clears { get; set; } = 0;
    public int Floor3Clears { get; set; } = 0;
    public int Floor4Clears { get; set; } = 0;
    
    public RaidTeam() { }
    
    public RaidTeam(string name)
    {
        Name = name;
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
