using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using GearPlanner.Models;

namespace GearPlanner;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool IsMainWindowMovable { get; set; } = true;
    public List<RaidTeam> RaidTeams { get; set; } = new();
    public int SelectedTeamIndex { get; set; } = -1;
    public List<GearSheet> IndividualTabSheets { get; set; } = new();
    public int IndividualTabSelectedSheetIndex { get; set; } = -1;

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        try
        {
            Plugin.Log.Debug($"[Configuration.Save] Saving configuration...");
            Plugin.PluginInterface.SavePluginConfig(this);
            Plugin.Log.Information($"[Configuration.Save] Configuration saved successfully");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[Configuration.Save] Failed to save configuration: {ex.Message}");
        }
    }
}
