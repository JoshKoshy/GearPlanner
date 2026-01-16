using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TeamGearPlanning.Helpers;
using TeamGearPlanning.Models;

namespace TeamGearPlanning.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private string newTeamName = string.Empty;
    private string newMemberName = string.Empty;
    private int selectedJobIndex = 0;
    private const int MaxTeamSize = 8;

    public ConfigWindow(Plugin plugin) : base("Team Configuration###TeamGearPlanningConfig")
    {
        Flags = ImGuiWindowFlags.AlwaysAutoResize;
        Size = new Vector2(600, 500);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        ImGui.Text("Team Gear Planning Configuration");
        ImGui.Separator();

        // Team Creation Section
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Create New Team");
        ImGui.InputText("Team Name##NewTeam", ref newTeamName, 100);
        ImGui.SameLine();
        if (ImGui.Button("Create Team##CreateBtn"))
        {
            if (!string.IsNullOrWhiteSpace(newTeamName))
            {
                var newTeam = new RaidTeam(newTeamName);
                configuration.RaidTeams.Add(newTeam);
                configuration.SelectedTeamIndex = configuration.RaidTeams.Count - 1;
                newTeamName = string.Empty;
                configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Team Selection
        if (configuration.RaidTeams.Count > 0)
        {
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Manage Teams");
            
            var teamNames = new string[configuration.RaidTeams.Count];
            for (int i = 0; i < configuration.RaidTeams.Count; i++)
            {
                teamNames[i] = configuration.RaidTeams[i].Name;
            }

            int selectedTeam = configuration.SelectedTeamIndex < 0 ? 0 : configuration.SelectedTeamIndex;
            if (ImGui.Combo("Select Team##TeamCombo", ref selectedTeam, teamNames, teamNames.Length))
            {
                configuration.SelectedTeamIndex = selectedTeam;
                configuration.Save();
            }

            ImGui.Spacing();

            // Display selected team info and management
            if (configuration.SelectedTeamIndex >= 0 && configuration.SelectedTeamIndex < configuration.RaidTeams.Count)
            {
                var team = configuration.RaidTeams[configuration.SelectedTeamIndex];

                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), $"Team: {team.Name}");
                ImGui.Text($"Members: {team.Members.Count}/{MaxTeamSize}");

                if (ImGui.Button("Delete Team##DeleteTeamBtn", new Vector2(100, 0)))
                {
                    configuration.RaidTeams.RemoveAt(configuration.SelectedTeamIndex);
                    configuration.SelectedTeamIndex = configuration.RaidTeams.Count > 0 ? 0 : -1;
                    configuration.Save();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Add Member Section
                if (team.Members.Count < MaxTeamSize)
                {
                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Add Member");
                    ImGui.InputText("Member Name##NewMember", ref newMemberName, 100);
                    ImGui.SameLine();

                    if (ImGui.Combo("Job##JobCombo", ref selectedJobIndex, FFXIVJobs.AllJobs, FFXIVJobs.AllJobs.Length))
                    {
                        // Job selection updated
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Add Member##AddMemberBtn"))
                    {
                        if (!string.IsNullOrWhiteSpace(newMemberName))
                        {
                            var selectedJob = FFXIVJobs.AllJobs[selectedJobIndex];
                            var role = FFXIVJobs.GetRoleForJob(selectedJob);
                            var newMember = new RaidMember(newMemberName, selectedJob, role);
                            team.AddMember(newMember);
                            newMemberName = string.Empty;
                            configuration.Save();
                        }
                    }

                    ImGui.Spacing();
                }
                else
                {
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), $"Team is full ({MaxTeamSize} members)");
                    ImGui.Spacing();
                }

                // Members List
                ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Team Members");
                ImGui.Separator();

                using (var table = ImRaii.Table("MembersTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    if (table.Success)
                    {
                        ImGui.TableSetupColumn("Name");
                        ImGui.TableSetupColumn("Job");
                        ImGui.TableSetupColumn("Role");
                        ImGui.TableSetupColumn("Action");
                        ImGui.TableHeadersRow();

                        int memberToRemove = -1;

                        for (int i = 0; i < team.Members.Count; i++)
                        {
                            var member = team.Members[i];
                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text(member.Name);

                            ImGui.TableSetColumnIndex(1);
                            ImGui.Text(member.Job);

                            ImGui.TableSetColumnIndex(2);
                            ImGui.Text(member.Role.ToString());

                            ImGui.TableSetColumnIndex(3);
                            if (ImGui.Button($"Remove##RemoveBtn{i}", new Vector2(60, 0)))
                            {
                                memberToRemove = i;
                            }
                        }

                        if (memberToRemove >= 0)
                        {
                            team.Members.RemoveAt(memberToRemove);
                            configuration.Save();
                        }
                    }
                }
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "No teams created. Create one above!");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Settings
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Settings");
        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }
    }
}

