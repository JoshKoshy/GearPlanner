using System;
using System.Collections.Generic;

namespace GearPlanner.Models;

[System.Serializable]
public class GearSheet
{
    public string Name { get; set; } = string.Empty;
    public List<RaidMember> Members { get; set; } = new();
    public int Floor1Clears { get; set; } = 0;
    public int Floor2Clears { get; set; } = 0;
    public int Floor3Clears { get; set; } = 0;
    public int Floor4Clears { get; set; } = 0;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public GearSheet() { }

    public GearSheet(string name, List<RaidMember> members)
    {
        Name = name;
        Members = new List<RaidMember>(members);
    }
}
