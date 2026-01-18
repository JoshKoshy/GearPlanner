using System;
using System.Collections.Generic;

namespace GearPlanner.Models;

public class BiSLibraryData
{
    public Dictionary<string, List<BiSGroup>> Jobs { get; set; } = new();
}

public class BiSGroup
{
    public string Name { get; set; } = "";
    public List<BiSSet> Sets { get; set; } = new();
}

public class BiSSet
{
    public string Name { get; set; } = "";
    public Dictionary<string, BiSItem> Items { get; set; } = new();
    public int Food { get; set; }
    public string Description { get; set; } = "";
    public bool IsSeparator { get; set; }
}

public class BiSItem
{
    public int Id { get; set; }
    public List<BiSMateria> Materia { get; set; } = new();
}

public class BiSMateria
{
    public int Id { get; set; }
    public bool Locked { get; set; }
}
