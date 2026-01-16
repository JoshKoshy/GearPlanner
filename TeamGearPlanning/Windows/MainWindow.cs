using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
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
        if (ImGui.Button("Settings", new Vector2(80, 0)))
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
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Team:");
        ImGui.SameLine();
        string teamName = team.Name;
        ImGui.SetNextItemWidth(150);
        if (ImGui.InputText("##TeamName", ref teamName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            team.Name = teamName;
            plugin.Configuration.Save();
        }
        // ImGui.SameLine(200);
        // ImGui.Text($"Members: {team.Members.Count}/8");
        // ImGui.SameLine(350);
        // ImGui.Text($"Team Avg: {team.GetTeamAverageGearLevel()}%");
        // ImGui.SameLine(500);
        // ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), $"⚠ Incomplete: {team.GetMembersMissingBiS()}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Display all members in sections as desired
        // Example: Draw all members together, or split by custom logic
        DrawMemberSection(team.Members, team);

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

    private void DrawMemberSection(List<Models.RaidMember> members, Models.RaidTeam team)
    {
        const int maxPerRow = 4;
        int rowCount = (int)Math.Ceiling(members.Count / (float)maxPerRow);
        
        for (int rowIdx = 0; rowIdx < rowCount; rowIdx++)
        {
            var rowMembers = members.Skip(rowIdx * maxPerRow).Take(maxPerRow).ToList();
            
            if (ImGui.BeginChild($"Section_{rowIdx}_{string.Join("_", rowMembers.Select(m => team.Members.IndexOf(m)))}", 
                new Vector2(rowMembers.Count * 355 + (rowMembers.Count - 1) * 5, 565), false))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(5, 5));
                ImGui.Columns(rowMembers.Count, $"SectionColumns_{rowIdx}_{string.Join("_", rowMembers.Select(m => team.Members.IndexOf(m)))}", false);
                
                for (int i = 0; i < rowMembers.Count; i++)
                {
                    var memberIdx = team.Members.IndexOf(rowMembers[i]);
                    DrawMemberSection(team, memberIdx);
                    ImGui.NextColumn();
                }
                
                ImGui.Columns(1);
                ImGui.PopStyleVar();
                ImGui.EndChild();
            }
            
            if (rowIdx < rowCount - 1)
            {
                ImGui.Spacing();
                ImGui.Spacing();
            }
        }
    }

    private void DrawMemberSection(Models.RaidTeam team, int memberIdx)
    {
        var member = team.Members[memberIdx];
        
        // Constrain to a max width and height
        if (ImGui.BeginChild($"MemberSection{memberIdx}", new System.Numerics.Vector2(350, 565), true))
        {
            // Member name - editable
            string name = member.Name;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText($"##CharName{memberIdx}", ref name, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                member.Name = name;
                plugin.Configuration.Save();
            }
            
            // Job dropdown
            int currentJobIdx = System.Array.IndexOf(jobOptions, member.Job);
            if (currentJobIdx < 0) currentJobIdx = 0;
            
            ImGui.SetNextItemWidth(-1);
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
            
            ImGui.Spacing();
            
            DrawGearTableForMember(team, memberIdx);
            
            ImGui.Spacing();
            
            DrawCurrencyRowForMember(team, memberIdx);
            
            ImGui.Spacing();
            
            DrawMaterialsTableForMember(team, memberIdx);
            
            ImGui.EndChild();
        }
    }

    private void DrawGearTableForMember(Models.RaidTeam team, int memberIdx)
    {
        var member = team.Members[memberIdx];
        var gearSlots = System.Enum.GetNames(typeof(Models.GearSlot));

        if (ImGui.BeginTable($"GearGrid{memberIdx}", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            // Setup columns: Slot, Desired, Current
            ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Desired", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupColumn("Current", ImGuiTableColumnFlags.WidthFixed, 65);

            // Header row
            ImGui.TableHeadersRow();

            // Data rows - one for each gear slot
            foreach (var slotName in gearSlots)
            {
                ImGui.TableNextRow(ImGuiTableRowFlags.None, 25);

                // Gear Slot column
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), slotName);

                // Desired gear column
                if (member.Gear.TryGetValue(slotName, out var piece))
                {
                    var desiredColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // Default white
                    var currentColor = GetSourceColor(piece.Source);
                    
                    // Check if desired source matches current source - use purple if so
                    if (piece.DesiredSource != Models.GearSource.None && piece.Source == piece.DesiredSource)
                    {
                        desiredColor = new Vector4(0.8f, 0.0f, 1.0f, 1.0f); // Purple
                    }
                    
                    // Check if current source matches desired - use purple if so
                    if (piece.Source == piece.DesiredSource && piece.Source != Models.GearSource.None)
                    {
                        currentColor = new Vector4(0.8f, 0.0f, 1.0f, 1.0f); // Purple
                    }
                    
                    ImGui.TableSetColumnIndex(1);
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
                    ImGui.TableSetColumnIndex(2);
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
                        ImGui.SetTooltip($"{piece.Source} | {piece.CurrentStatus}");
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

            ImGui.EndTable();
        }
    }

    private void DrawCurrencyRowForMember(Models.RaidTeam team, int memberIdx)
    {
        var member = team.Members[memberIdx];

        if (ImGui.BeginTable($"CurrencyTable{memberIdx}", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            // Setup columns
            ImGui.TableSetupColumn("Currency", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Floor 1", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Floor 2", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Floor 3", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Floor 4", ImGuiTableColumnFlags.WidthFixed, 45);

            // Header row
            ImGui.TableHeadersRow();

            // Pages row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Pages");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                ImGui.Text("0"); // Placeholder - add data tracking per floor as needed
            }

            // Pages Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Pages Needed");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                ImGui.Text("4"); // Placeholder - calculate based on pages earned per floor
            }

            // Page Adjust row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Page Adjust");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                ImGui.SetNextItemWidth(35);
                int adjustValue = 0; // Placeholder - add data tracking to model
                if (ImGui.InputInt($"##PageAdjust{memberIdx}_{floor}", ref adjustValue))
                {
                    // Update member data when changed
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawMaterialsTableForMember(Models.RaidTeam team, int memberIdx)
    {
        var member = team.Members[memberIdx];

        if (ImGui.BeginTable($"MaterialsTable{memberIdx}", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            // Setup columns
            ImGui.TableSetupColumn("Materials", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 130);

            // Header row
            ImGui.TableHeadersRow();

            // Glazes Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Glazes Needed");
            ImGui.TableSetColumnIndex(1);
            int glazesNeeded = (4 - member.PagesEarned) * 4;
            ImGui.Text(glazesNeeded.ToString());

            // Twines Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Twines Needed");
            ImGui.TableSetColumnIndex(1);
            int twinesNeeded = (4 - member.PagesEarned) * 8;
            ImGui.Text(twinesNeeded.ToString());

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
