using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace TeamGearPlanning.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Team Gear Planning##MainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Team Gear Planning");
        
        if (ImGui.Button("Show Settings"))
        {
            plugin.ToggleConfigUi();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        using (var child = ImRaii.Child("GearTrackingArea", Vector2.Zero, true))
        {
            if (child.Success)
            {
                if (plugin.Configuration.RaidTeams.Count == 0)
                {
                    ImGui.Text("No raid teams configured. Use /tgp config to create a new team.");
                    return;
                }

                var selectedTeamIndex = plugin.Configuration.SelectedTeamIndex;
                if (selectedTeamIndex < 0 || selectedTeamIndex >= plugin.Configuration.RaidTeams.Count)
                {
                    ImGui.Text("Please select a team from the configuration.");
                    return;
                }

                var team = plugin.Configuration.RaidTeams[selectedTeamIndex];
                
                ImGui.Text($"Team: {team.Name}");
                ImGui.Text($"Members: {team.Members.Count}");
                ImGui.Text($"Team Avg Gear: {team.GetTeamAverageGearLevel()}%");
                ImGui.Text($"Members Missing BiS: {team.GetMembersMissingBiS()}");
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                if (ImGui.BeginTable("GearTable", team.Members.Count + 1, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Gear Slot");
                    foreach (var member in team.Members)
                    {
                        ImGui.TableSetupColumn(member.Name);
                    }
                    ImGui.TableHeadersRow();

                    var gearSlots = System.Enum.GetNames(typeof(Models.GearSlot));
                    foreach (var slotName in gearSlots)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text(slotName);

                        int colIndex = 1;
                        foreach (var member in team.Members)
                        {
                            ImGui.TableSetColumnIndex(colIndex++);
                            if (member.Gear.TryGetValue(slotName, out var piece))
                            {
                                var color = GetStatusColor(piece.CurrentStatus);
                                ImGui.TextColored(color, piece.CurrentStatus.ToString());
                            }
                            else
                            {
                                ImGui.Text("-");
                            }
                        }
                    }

                    ImGui.EndTable();
                }
            }
        }
    }

    private Vector4 GetStatusColor(Models.GearStatus status)
    {
        return status switch
        {
            Models.GearStatus.None => new Vector4(0.7f, 0.7f, 0.7f, 1.0f), // Gray
            Models.GearStatus.LowIlvl => new Vector4(1.0f, 0.0f, 0.0f, 1.0f), // Red
            Models.GearStatus.CraftedGear => new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // White
            Models.GearStatus.AllianceRaid => new Vector4(0.5f, 0.8f, 1.0f, 1.0f), // Light Blue
            Models.GearStatus.SavageRaid => new Vector4(0.0f, 0.5f, 1.0f, 1.0f), // Blue
            Models.GearStatus.BiS => new Vector4(0.8f, 0.0f, 1.0f, 1.0f), // Purple
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // White
        };
    }
}
