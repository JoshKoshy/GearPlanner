using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using TeamGearPlanning.Models;

namespace TeamGearPlanning.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly string[] gearSourceOptions = { "Savage", "Tome Up", "Catchup", "Tome", "Relic", "Crafted", "Prep", "Trash", "Wow" };
    private readonly string[] jobOptions = Helpers.FFXIVJobs.GetAllJobOptions();

    public MainWindow(Plugin plugin)
        : base("Team Gear Planning##MainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1000, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Header
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Team Gear Planning");
        ImGui.SameLine();
        if (ImGui.Button("⚙ Config", new Vector2(80, 0)))
        {
            plugin.ToggleConfigUi();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(Use /tgp config to manage teams and members)");

        ImGui.Separator();

        if (plugin.Configuration.RaidTeams.Count == 0)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "No raid teams configured.");
            ImGui.Text("Use /tgp config to create a team or edit the sample team.");
            return;
        }

        var selectedTeamIndex = plugin.Configuration.SelectedTeamIndex;
        if (selectedTeamIndex < 0 || selectedTeamIndex >= plugin.Configuration.RaidTeams.Count)
        {
            ImGui.Text("Please select a team from the configuration.");
            return;
        }

        var team = plugin.Configuration.RaidTeams[selectedTeamIndex];

        // Team info header with editable team name
        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), "📋");
        ImGui.SameLine();
        string teamName = team.Name;
        ImGui.SetNextItemWidth(150);
        if (ImGui.InputText("##TeamName", ref teamName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            team.Name = teamName;
            plugin.Configuration.Save();
        }
        ImGui.SameLine(200);
        ImGui.Text($"Members: {team.Members.Count}/8");
        ImGui.SameLine(350);
        ImGui.Text($"Team Avg: {team.GetTeamAverageGearLevel()}%");
        ImGui.SameLine(500);
        ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), $"⚠ Incomplete: {team.GetMembersMissingBiS()}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Gear grid table
        DrawGearTable(team);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Legend
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Legend:");
        ImGui.SameLine(100);
        ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "Red");
        ImGui.SameLine(150);
        ImGui.Text("= Low iLvl");
        ImGui.SameLine(250);
        ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), "White");
        ImGui.SameLine(330);
        ImGui.Text("= Crafted");
        ImGui.SameLine(420);
        ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), "Lt.Blue");
        ImGui.SameLine(510);
        ImGui.Text("= Alliance");

        ImGui.SameLine(650);
        ImGui.TextColored(new Vector4(0.0f, 0.5f, 1.0f, 1.0f), "Blue");
        ImGui.SameLine(700);
        ImGui.Text("= Savage");
        ImGui.SameLine(800);
        ImGui.TextColored(new Vector4(0.8f, 0.0f, 1.0f, 1.0f), "Purple");
        ImGui.SameLine(880);
        ImGui.Text("= BiS");
    }

    private void DrawGearTable(Models.RaidTeam team)
    {
        var gearSlots = System.Enum.GetNames(typeof(Models.GearSlot));
        int columnCount = (team.Members.Count * 2) + 1; // +1 for slot name column, 2 per member (current and desired)

        if (ImGui.BeginTable("GearGrid", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            // Setup columns with two columns per member
            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 140);
            
            foreach (var member in team.Members)
            {
                ImGui.TableSetupColumn($"{member.Name} (C)", ImGuiTableColumnFlags.WidthFixed, 65);
                ImGui.TableSetupColumn($"{member.Name} (D)", ImGuiTableColumnFlags.WidthFixed, 65);
            }

            // Custom header row with editable character names
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableSetColumnIndex(0);
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Slot");
            
            for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
            {
                var member = team.Members[memberIdx];
                
                // Current column
                ImGui.TableSetColumnIndex((memberIdx * 2) + 1);
                string name = member.Name;
                ImGui.SetNextItemWidth(180); // Span both columns
                if (ImGui.InputText($"##CharName{memberIdx}", ref name, 50, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    member.Name = name;
                    plugin.Configuration.Save();
                }
                
                // Desired column - merge with current for spanning
                ImGui.TableSetColumnIndex((memberIdx * 2) + 2);
                // Empty - just for layout
            }

            // Job row - showing member job/role
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Job");
            
            for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
            {
                ImGui.TableSetColumnIndex((memberIdx * 2) + 1);
                var member = team.Members[memberIdx];
                
                // Job dropdown with full job names - spans across columns
                int currentJobIdx = System.Array.IndexOf(jobOptions, member.Job);
                if (currentJobIdx < 0) currentJobIdx = 0;
                
                ImGui.SetNextItemWidth(180); // Span both columns
                if (ImGui.Combo($"##Job{memberIdx}", ref currentJobIdx, jobOptions))
                {
                    member.Job = jobOptions[currentJobIdx];
                    var role = Helpers.FFXIVJobs.GetRoleForJob(member.Job);
                    if (role != Models.JobRole.Unknown)
                    {
                        member.Role = role;
                    }
                    plugin.Configuration.Save();
                }
                
                // Desired column - empty for job row to allow spanning
                ImGui.TableSetColumnIndex((memberIdx * 2) + 2);
                // Empty for layout
            }

            // Column labels row for Desired/Current gear
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            // Empty for slot column
            
            for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
            {
                // Desired gear label
                ImGui.TableSetColumnIndex((memberIdx * 2) + 1);
                ImGui.TextDisabled("Desired");
                
                // Current gear label
                ImGui.TableSetColumnIndex((memberIdx * 2) + 2);
                ImGui.TextDisabled("Current");
            }

            // Data rows - one for each gear slot
            foreach (var slotName in gearSlots)
            {
                ImGui.TableNextRow();

                // Gear Slot column
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), slotName);

                // Member gear status columns (two per member)
                for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
                {
                    var member = team.Members[memberIdx];
                    if (member.Gear.TryGetValue(slotName, out var piece))
                    {
                        var currentStatus = piece.CurrentStatus;
                        var desiredStatus = piece.DesiredStatus;
                        var desiredColor = GetStatusColor(desiredStatus);
                        
                        // Get color for current gear based on source
                        var currentColor = GetSourceColor(piece.Source);
                        
                        // Check if current source matches desired and is Savage or TomeUp - use purple if so
                        if (piece.Source == piece.DesiredSource && 
                            (piece.Source == Models.GearSource.Savage || piece.Source == Models.GearSource.TomeUp))
                        {
                            currentColor = new Vector4(0.8f, 0.0f, 1.0f, 1.0f); // Purple
                        }
                        
                        // Desired gear column
                        ImGui.TableSetColumnIndex((memberIdx * 2) + 1);
                        string desiredSourceDisplay = piece.DesiredSource.ToString() == "None" ? "" : piece.DesiredSource.ToString();
                        ImGui.TextColored(desiredColor, desiredSourceDisplay);
                        
                        // Make empty cells clickable with invisible button
                        if (desiredSourceDisplay == "")
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetTextLineHeight());
                            if (ImGui.InvisibleButton($"DesiredGear{memberIdx}_{slotName}", new System.Numerics.Vector2(-1, ImGui.GetTextLineHeight())))
                            {
                                ImGui.OpenPopup($"StatusMenu{memberIdx}_{slotName}");
                            }
                        }
                        else if (ImGui.IsItemClicked())
                        {
                            ImGui.OpenPopup($"StatusMenu{memberIdx}_{slotName}");
                        }
                        
                        // Tooltip for desired
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"{piece.DesiredSource}");
                        }
                        
                        // Current gear column
                        ImGui.TableSetColumnIndex((memberIdx * 2) + 2);
                        string sourceDisplay = piece.Source.ToString() == "None" ? "" : piece.Source.ToString();
                        ImGui.TextColored(currentColor, sourceDisplay);
                        
                        // Make empty cells clickable with invisible button
                        if (sourceDisplay == "")
                        {
                            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetTextLineHeight());
                            if (ImGui.InvisibleButton($"CurrentGear{memberIdx}_{slotName}", new System.Numerics.Vector2(-1, ImGui.GetTextLineHeight())))
                            {
                                ImGui.OpenPopup($"SourceMenu{memberIdx}_{slotName}");
                            }
                        }
                        else if (ImGui.IsItemClicked())
                        {
                            ImGui.OpenPopup($"SourceMenu{memberIdx}_{slotName}");
                        }
                        
                        // Tooltip showing full info
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip($"{piece.Source} | {currentStatus}");
                        }
                        
                        // Right-click menu for source
                        if (ImGui.BeginPopup($"SourceMenu{memberIdx}_{slotName}"))
                        {
                            ImGui.TextDisabled("Gear Source:");
                            ImGui.Separator();
                            
                            foreach (var sourceOption in gearSourceOptions)
                            {
                                var sourceEnumValue = sourceOption.Replace(" ", "");
                                if (ImGui.MenuItem(sourceOption, "", piece.Source.ToString() == sourceEnumValue))
                                {
                                    piece.Source = System.Enum.Parse<GearSource>(sourceEnumValue);
                                    plugin.Configuration.Save();
                                }
                            }
                            
                            ImGui.EndPopup();
                        }
                        
                        // Menu for desired source
                        if (ImGui.BeginPopup($"StatusMenu{memberIdx}_{slotName}"))
                        {
                            ImGui.TextDisabled("Desired Source:");
                            ImGui.Separator();
                            
                            foreach (var sourceOption in gearSourceOptions)
                            {
                                var sourceEnumValue = sourceOption.Replace(" ", "");
                                if (ImGui.MenuItem(sourceOption, "", piece.DesiredSource.ToString() == sourceEnumValue))
                                {
                                    piece.DesiredSource = System.Enum.Parse<GearSource>(sourceEnumValue);
                                    plugin.Configuration.Save();
                                }
                            }
                            
                            ImGui.EndPopup();
                        }
                    }
                }
            }

            ImGui.EndTable();
        }
    }

    private string GetStatusAbbreviation(Models.GearStatus status)
    {
        return status switch
        {
            Models.GearStatus.None => "-",
            Models.GearStatus.LowIlvl => "Low",
            Models.GearStatus.CraftedGear => "Crft",
            Models.GearStatus.AllianceRaid => "All",
            Models.GearStatus.SavageRaid => "Sav",
            Models.GearStatus.BiS => "BiS",
            _ => "?",
        };
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

    private Vector4 GetSourceColor(Models.GearSource source)
    {
        return source switch
        {
            Models.GearSource.Savage => new Vector4(0.0f, 0.5f, 1.0f, 1.0f), // Blue
            Models.GearSource.TomeUp => new Vector4(0.0f, 0.5f, 1.0f, 1.0f), // Blue
            Models.GearSource.Catchup => new Vector4(0.0f, 1.0f, 0.0f, 1.0f), // Green
            Models.GearSource.Tome => new Vector4(1.0f, 1.0f, 0.0f, 1.0f), // Yellow
            Models.GearSource.Relic => new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // White
            Models.GearSource.Crafted => new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // White
            Models.GearSource.Prep => new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // White
            Models.GearSource.Trash => new Vector4(1.0f, 0.0f, 0.0f, 1.0f), // Red
            Models.GearSource.Wow => new Vector4(1.0f, 0.0f, 0.0f, 1.0f), // Red
            Models.GearSource.None => new Vector4(0.7f, 0.7f, 0.7f, 1.0f), // Gray
            _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f), // White
        };
    }
}
