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

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Use columns to layout team info on left and member tables on right
        ImGui.Columns(2, "TeamLayout", false);
        ImGui.SetColumnWidth(0, 200);

        // Left column - Team info
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Team:");
        ImGui.SameLine(0, 5);
        string teamName = team.Name;
        ImGui.SetNextItemWidth(130);
        if (ImGui.InputText("##TeamName", ref teamName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            team.Name = teamName;
            plugin.Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        // Floor clears section
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Floor Clears:");
        ImGui.Text("Floor 1:");
        ImGui.SameLine(80);
        ImGui.SetNextItemWidth(80);
        int floor1Clears = team.Floor1Clears;
        if (ImGui.InputInt("##Floor1Clears", ref floor1Clears, 1, 5))
        {
            team.Floor1Clears = floor1Clears;
            plugin.Configuration.Save();
        }

        ImGui.Text("Floor 2:");
        ImGui.SameLine(80);
        ImGui.SetNextItemWidth(80);
        int floor2Clears = team.Floor2Clears;
        if (ImGui.InputInt("##Floor2Clears", ref floor2Clears, 1, 5))
        {
            team.Floor2Clears = floor2Clears;
            plugin.Configuration.Save();
        }

        ImGui.Text("Floor 3:");
        ImGui.SameLine(80);
        ImGui.SetNextItemWidth(80);
        int floor3Clears = team.Floor3Clears;
        if (ImGui.InputInt("##Floor3Clears", ref floor3Clears, 1, 5))
        {
            team.Floor3Clears = floor3Clears;
            plugin.Configuration.Save();
        }

        ImGui.Text("Floor 4:");
        ImGui.SameLine(80);
        ImGui.SetNextItemWidth(80);
        int floor4Clears = team.Floor4Clears;
        if (ImGui.InputInt("##Floor4Clears", ref floor4Clears, 1, 5))
        {
            team.Floor4Clears = floor4Clears;
            plugin.Configuration.Save();
        }

        ImGui.NextColumn();

        // Right column - Member tables
        DrawMemberSection(team.Members, team);

        ImGui.Columns(1);

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
                int pagesFromClears = GetPagesFromClears(team, floor);
                int pageAdjustment = member.PageAdjustments.ContainsKey(floor) ? member.PageAdjustments[floor] : 0;
                int totalPages = pagesFromClears + pageAdjustment;
                ImGui.Text(totalPages.ToString());
            }

            // Pages Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Pages Needed");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                int pagesNeeded = CalculatePagesNeededForFloor(member, floor);
                int pagesFromClears = GetPagesFromClears(team, floor);
                int pageAdjustment = member.PageAdjustments.ContainsKey(floor) ? member.PageAdjustments[floor] : 0;
                int totalPages = pagesFromClears + pageAdjustment;
                int remainingPages = Math.Max(0, pagesNeeded - totalPages);
                ImGui.Text(remainingPages.ToString());
            }

            // Page Adjust row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Page Adjust");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                ImGui.SetNextItemWidth(35);
                int adjustValue = member.PageAdjustments.ContainsKey(floor) ? member.PageAdjustments[floor] : 0;
                if (ImGui.InputInt($"##PageAdjust{memberIdx}_{floor}", ref adjustValue))
                {
                    member.PageAdjustments[floor] = adjustValue;
                    plugin.Configuration.Save();
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
            int glazesNeeded = CalculateGlazesNeeded(member);
            ImGui.Text(glazesNeeded.ToString());

            // Twines Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Twines Needed");
            ImGui.TableSetColumnIndex(1);
            int twinesNeeded = CalculateTwinesNeeded(member);
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

    private int CalculatePagesNeededForFloor(Models.RaidMember member, int floor)
    {
        // Map floors to gear slots that drop on each floor
        // This is based on standard FFXIV Savage tier loot distribution
        List<string> floorSlots = floor switch
        {
            1 => new() { "Ears", "Neck", "Wrists", "Ring2" },
            2 => new() { "Head", "Hands", "Feet", "Glaze" },
            3 => new() { "Body", "Legs", "Twine"},
            4 => new() { "MainHand", "OffHand"},
            _ => new()
        };

        int pagesNeeded = 0;

        // Count gear pieces on this floor that need pages
        foreach (var slotName in floorSlots)
        {
            if (member.Gear.TryGetValue(slotName, out var piece))
            {
                // Only calculate pages if desired source is Savage and doesn't match current
                if (piece.DesiredSource == Models.GearSource.Savage && piece.Source != piece.DesiredSource)
                {
                    // Specific page costs for each piece type in FFXIV
                    int pageCost = slotName switch
                    {
                        "MainHand" or "OffHand" => 8,
                        "Body" or "Legs" => 6,
                        "Head" or "Hands" or "Feet" or "Twine" => 4,
                        "Ears" or "Neck" or "Wrists" or "Ring1" or "Ring2" or "Glaze" => 3,
                        _ => 0
                    };
                    pagesNeeded += pageCost;
                }
            }
        }

        // Add extra pages needed for TomeUp materials
        if (floor == 2)
        {
            int glazesNeeded = CalculateGlazesNeeded(member);
            pagesNeeded += glazesNeeded * 3;
        }
        else if (floor == 3)
        {
            int twinesNeeded = CalculateTwinesNeeded(member);
            pagesNeeded += twinesNeeded * 4;
        }

        return pagesNeeded;
    }

    private int CalculateGlazesNeeded(Models.RaidMember member)
    {
        // Glazes are needed for TomeUp gear on ears, neck, wrists, ring 1, or ring 2
        List<string> glazeSlots = new() { "Ears", "Neck", "Wrists", "Ring1", "Ring2" };
        int glazesNeeded = 0;

        foreach (var slotName in glazeSlots)
        {
            if (member.Gear.TryGetValue(slotName, out var piece))
            {
                if (piece.DesiredSource == Models.GearSource.TomeUp && piece.Source != piece.DesiredSource)
                {
                    glazesNeeded += 1;
                }
            }
        }

        return glazesNeeded;
    }

    private int CalculateTwinesNeeded(Models.RaidMember member)
    {
        // Twines are needed for TomeUp gear on head, body, hands, legs, or feet
        List<string> twineSlots = new() { "Head", "Body", "Hands", "Legs", "Feet" };
        int twinesNeeded = 0;

        foreach (var slotName in twineSlots)
        {
            if (member.Gear.TryGetValue(slotName, out var piece))
            {
                if (piece.DesiredSource == Models.GearSource.TomeUp && piece.Source != piece.DesiredSource)
                {
                    twinesNeeded += 1;
                }
            }
        }

        return twinesNeeded;
    }

    private int GetPagesFromClears(Models.RaidTeam team, int floor)
    {
        // Each clear of a floor gives a certain number of pages
        // In FFXIV, each savage floor clear gives pages that can be used for gear
        int clears = floor switch
        {
            1 => team.Floor1Clears,
            2 => team.Floor2Clears,
            3 => team.Floor3Clears,
            4 => team.Floor4Clears,
            _ => 0
        };

        // Each floor clear provides pages (typical FFXIV loot: 4 pages per clear)
        return clears;
    }
}
