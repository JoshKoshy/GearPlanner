using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace GearPlanner.Windows;

public class SetupWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string teamNameInput = "My Raid Team";

    public SetupWindow(Plugin plugin) : base("Gear Planner Setup###SetupWindow")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
        IsOpen = false;

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Welcome to Gear Planner!");
        ImGui.Separator();

        ImGui.TextWrapped(
            "This plugin helps FFXIV raid teams organize and plan gear distribution, just like the " +
            "popular Google Sheets spreadsheet.\n\n" +
            "To get started, you can either:\n" +
            "1. Edit the sample team created for you\n" +
            "2. Create a new team from scratch\n\n" +
            "Team members can have any FFXIV job, and gear is color-coded by status:\n" +
            "  Red - Low Item Level\n" +
            "  White - Crafted Gear\n" +
            "  Light Blue - Alliance Raid Gear\n" +
            "  Blue - Savage Raid Gear\n" +
            "  Purple - Best in Slot (BiS)");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("Quick Setup:");
        ImGui.InputText("New Team Name##TeamNameSetup", ref teamNameInput, 100);
        
        if (ImGui.Button("Create New Team##SetupBtn", new Vector2(150, 0)))
        {
            if (!string.IsNullOrWhiteSpace(teamNameInput))
            {
                var newTeam = new Models.RaidTeam(teamNameInput);
                plugin.Configuration.RaidTeams.Add(newTeam);
                plugin.Configuration.SelectedTeamIndex = plugin.Configuration.RaidTeams.Count - 1;
                plugin.Configuration.Save();
                IsOpen = false;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Edit Sample Team##EditSampleBtn", new Vector2(150, 0)))
        {
            if (plugin.Configuration.RaidTeams.Count > 0)
            {
                plugin.Configuration.SelectedTeamIndex = 0;
                plugin.Configuration.Save();
            }
            IsOpen = false;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Commands:");
        ImGui.Text("/tgp config - Open team configuration");
        ImGui.Text("/tgp show - Show main gear tracking window");
        ImGui.Text("/tgp hide - Hide main gear tracking window");

        ImGui.Spacing();
        
        if (ImGui.Button("Close##SetupClose", new Vector2(100, 0)))
        {
            IsOpen = false;
        }
    }
}
