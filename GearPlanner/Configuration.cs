using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using GearPlanner.Models;

namespace GearPlanner;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsMainWindowMovable { get; set; } = true;
    public List<RaidTeam> RaidTeams { get; set; } = new();
    public int SelectedTeamIndex { get; set; } = -1;
    public List<GearSheet> IndividualTabSheets { get; set; } = new();
    public int IndividualTabSelectedSheetIndex { get; set; } = -1;
    public int IndividualTabFloor1Clears { get; set; } = 0;
    public int IndividualTabFloor2Clears { get; set; } = 0;
    public int IndividualTabFloor3Clears { get; set; } = 0;
    public int IndividualTabFloor4Clears { get; set; } = 0;
    public List<string> LootPlannerWeeks { get; set; } = new();
    public int LootPlannerSelectedWeekIndex { get; set; } = 0;
    public Dictionary<string, int> LootPlannerAssignments { get; set; } = new();

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        try
        {
            Plugin.Log.Debug($"[Configuration.Save] Saving configuration...");
            // Log the teams being saved
            foreach (var team in RaidTeams)
            {
                Plugin.Log.Debug($"[Configuration.Save] Team '{team.Name}': {team.Members.Count} members, {team.Sheets.Count} sheets");
                for (int i = 0; i < team.Sheets.Count; i++)
                {
                    Plugin.Log.Debug($"[Configuration.Save]   Sheet {i} '{team.Sheets[i].Name}': {team.Sheets[i].Members.Count} members");
                }
            }
            Plugin.PluginInterface.SavePluginConfig(this);
            Plugin.Log.Information($"[Configuration.Save] Configuration saved successfully");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[Configuration.Save] Failed to save configuration: {ex.Message}");
        }
    }
}
