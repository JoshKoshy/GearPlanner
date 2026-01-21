using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GearPlanner.Models;
using GearPlanner.Helpers;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace GearPlanner.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly string[] gearSourceOptions = { "Savage", "Tome Up", "Catchup", "Tome", "Relic", "Crafted", "Prep", "Trash", "Wow" };
    private readonly string[] jobOptions = Helpers.FFXIVJobs.GetAllJobOptions();
    private Dictionary<int, string> memberXivGearUrlInput = new(); // Store URL input per member
    private Dictionary<int, int> memberBiSSetIndex = new(); // Store selected BiS set index per member
    private int selectedTab = 1; // 0=Individual, 1=Team, 2=Loot Planner, 3=Who Needs it?, 4=Legend
    private readonly string[] tabNames = { "Individual", "Team", "Loot Planner", "Who Needs it?", "Legend" };
    private List<Models.GearSheet> individualTabSheets = new(); // Sheets for Individual tab
    private int individualTabSelectedSheetIndex = 0;
    private Dictionary<int, string> individualSheetRenameInput = new(); // Store rename input per sheet
    private Dictionary<string, int> lootPlannerAssignments = new(); // Track loot assignments by loot name -> member index
    private List<string> lootPlannerWeeks = new(); // List of weeks for loot planning
    private int lootPlannerSelectedWeekIndex = 0; // Currently selected week
    private bool lootPlannerDataLoaded = false; // Flag to track if loot planner data has been loaded from config
    private Dictionary<string, bool> whoNeedsItCheckboxes = new(); // Track checkbox states for Who Needs It tab
    private string customXivGearJsonString = ""; // Store custom xivgear JSON string
    private int individualTabFloor1Clears = 0; // Static floor clears across all individual sheets
    private int individualTabFloor2Clears = 0;
    private int individualTabFloor3Clears = 0;
    private int individualTabFloor4Clears = 0;

    public MainWindow(Plugin plugin)
        : base("Gear Planner##MainWindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(1000, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Initialize loot planner data from configuration on first draw
        // Only load if our local list doesn't match the config (i.e., first time or plugin reloaded)
        if (!lootPlannerDataLoaded)
        {
            var configWeeks = plugin.Configuration.LootPlannerWeeks ?? new List<string>();
            lootPlannerWeeks.Clear();
            lootPlannerWeeks.AddRange(configWeeks);
            
            // Ensure we have at least one week if the list is empty
            if (lootPlannerWeeks.Count == 0)
            {
                lootPlannerWeeks.Add("Week 1");
            }
            
            lootPlannerSelectedWeekIndex = plugin.Configuration.LootPlannerSelectedWeekIndex;
            if (lootPlannerSelectedWeekIndex < 0 || lootPlannerSelectedWeekIndex >= lootPlannerWeeks.Count)
                lootPlannerSelectedWeekIndex = 0;
            lootPlannerAssignments = new Dictionary<string, int>(plugin.Configuration.LootPlannerAssignments ?? new Dictionary<string, int>());
            lootPlannerDataLoaded = true;
        }

        // Header
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Gear Planner");
        ImGui.SameLine();

        ImGui.Separator();

        // Tab bar
        if (ImGui.BeginTabBar("MainTabs", ImGuiTabBarFlags.None))
        {
            for (int i = 0; i < tabNames.Length; i++)
            {
                if (ImGui.BeginTabItem(tabNames[i]))
                {
                    selectedTab = i;
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }

        ImGui.Separator();

        // Tab content
        switch (selectedTab)
        {
            case 0: // Individual tab
                DrawIndividualTab();
                break;

            case 1: // Team tab
                DrawTeamTab();
                break;

            case 2: // Loot Planner tab
                DrawLootPlannerTab();
                break;

            case 3: // Who Needs it? tab
                DrawWhoNeedsItTab();
                break;

            case 4: // Legend tab
                DrawLegendForIndividualTab();
                break;
        }
    }

    private void DrawIndividualTab()
    {
        // Load sheets from configuration if not already loaded
        if (individualTabSheets.Count == 0 && plugin.Configuration.IndividualTabSheets.Count > 0)
        {
            individualTabSheets = plugin.Configuration.IndividualTabSheets;
            individualTabSelectedSheetIndex = plugin.Configuration.IndividualTabSelectedSheetIndex;
            if (individualTabSelectedSheetIndex < 0 || individualTabSelectedSheetIndex >= individualTabSheets.Count)
                individualTabSelectedSheetIndex = 0;
        }

        // Load floor clears from configuration
        if (individualTabFloor1Clears == 0 && individualTabFloor2Clears == 0 && individualTabFloor3Clears == 0 && individualTabFloor4Clears == 0)
        {
            individualTabFloor1Clears = plugin.Configuration.IndividualTabFloor1Clears;
            individualTabFloor2Clears = plugin.Configuration.IndividualTabFloor2Clears;
            individualTabFloor3Clears = plugin.Configuration.IndividualTabFloor3Clears;
            individualTabFloor4Clears = plugin.Configuration.IndividualTabFloor4Clears;
        }
        
        // Initialize sheets if still needed
        if (individualTabSheets.Count == 0)
        {
            var defaultMember = new Models.RaidMember("Player Name", "Paladin", Models.JobRole.Tank);
            defaultMember.InitializeGear();
            InitializeGearDefaults(defaultMember);
            
            individualTabSheets.Add(new Models.GearSheet("Sheet 1", new List<Models.RaidMember> { defaultMember }));
            individualTabSelectedSheetIndex = 0;
            plugin.Configuration.IndividualTabSheets = individualTabSheets;
            plugin.Configuration.IndividualTabSelectedSheetIndex = individualTabSelectedSheetIndex;
            plugin.Configuration.Save();
        }

        var member = individualTabSheets[individualTabSelectedSheetIndex].Members.Count > 0 ? individualTabSheets[individualTabSelectedSheetIndex].Members[0] : null;
        
        if (member == null)
        {
            ImGui.Text("No member in this sheet");
            return;
        }

        ImGui.Spacing();

        // Member tables and legend in a child window  
        var availableWidth = ImGui.GetContentRegionAvail().X;
        float legendMinWidth = 330f;
        float memberTableWidth = availableWidth - legendMinWidth;
        
        if (ImGui.BeginChild("IndividualMembersAndLegend", new Vector2(availableWidth, -1), false))
        {
            ImGui.Columns(2, "IndividualRightSection", false);
            ImGui.SetColumnWidth(0, memberTableWidth);
            ImGui.SetColumnWidth(1, legendMinWidth);
            
            // Sheet selection and naming - at the top
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Sheet:");
            ImGui.SameLine(0, 5);

            // Sheet dropdown combo
            var sheetNames = individualTabSheets.Select(s => s.Name).ToArray();
            ImGui.SetNextItemWidth(150);
            if (ImGui.Combo("##SheetSelector", ref individualTabSelectedSheetIndex, sheetNames))
            {
                // Clear the rename input for the newly selected sheet so it gets refreshed
                individualSheetRenameInput.Remove(individualTabSelectedSheetIndex);
                plugin.Configuration.IndividualTabSheets = individualTabSheets;
                plugin.Configuration.IndividualTabSelectedSheetIndex = individualTabSelectedSheetIndex;
                plugin.Configuration.Save();
            }
            
            // Recalculate currentSheet after combo in case it changed
            var currentSheet = individualTabSheets[individualTabSelectedSheetIndex];

            // Add new sheet button
            ImGui.SameLine(0, 5);
            if (ImGui.Button("+", new Vector2(25, 0)))
            {
                var newMember = new Models.RaidMember("Player Name", "Paladin", Models.JobRole.Tank);
                newMember.InitializeGear();
                InitializeGearDefaults(newMember);
                
                string newSheetName = $"Sheet {individualTabSheets.Count + 1}";
                individualTabSheets.Add(new Models.GearSheet(newSheetName, new List<Models.RaidMember> { newMember }));
                individualTabSelectedSheetIndex = individualTabSheets.Count - 1;
                individualSheetRenameInput[individualTabSelectedSheetIndex] = newSheetName;
                plugin.Configuration.IndividualTabSheets = individualTabSheets;
                plugin.Configuration.IndividualTabSelectedSheetIndex = individualTabSelectedSheetIndex;
                plugin.Configuration.Save();
            }

            ImGui.Spacing();

            // Floor Clears section - moved to top
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Floor Clears:");
            
            // Row 1: Floor 1 and Floor 2
            ImGui.Text("Floor 1:");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("##IndividualFloor1Clears", ref individualTabFloor1Clears, 1, 5))
            {
                plugin.Configuration.IndividualTabFloor1Clears = individualTabFloor1Clears;
                plugin.Configuration.Save();
            }

            ImGui.SameLine(200);
            ImGui.Text("Floor 2:");
            ImGui.SameLine(280);
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("##IndividualFloor2Clears", ref individualTabFloor2Clears, 1, 5))
            {
                plugin.Configuration.IndividualTabFloor2Clears = individualTabFloor2Clears;
                plugin.Configuration.Save();
            }

            // Row 2: Floor 3 and Floor 4
            ImGui.Text("Floor 3:");
            ImGui.SameLine(80);
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("##IndividualFloor3Clears", ref individualTabFloor3Clears, 1, 5))
            {
                plugin.Configuration.IndividualTabFloor3Clears = individualTabFloor3Clears;
                plugin.Configuration.Save();
            }

            ImGui.SameLine(200);
            ImGui.Text("Floor 4:");
            ImGui.SameLine(280);
            ImGui.SetNextItemWidth(80);
            if (ImGui.InputInt("##IndividualFloor4Clears", ref individualTabFloor4Clears, 1, 5))
            {
                plugin.Configuration.IndividualTabFloor4Clears = individualTabFloor4Clears;
                plugin.Configuration.Save();
            }

            ImGui.Spacing();

            // Delete sheet button moved below with sheet name field
            
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            // Left - Member section
            float memberSectionWidth = System.Math.Min(memberTableWidth - 10, 350);
            if (ImGui.BeginChild("IndividualMemberSection", new System.Numerics.Vector2(memberSectionWidth, 590), true))
            {
                // Sheet name - editable with delete button
                if (!individualSheetRenameInput.ContainsKey(individualTabSelectedSheetIndex))
                    individualSheetRenameInput[individualTabSelectedSheetIndex] = currentSheet.Name;

                string sheetNameInput = individualSheetRenameInput[individualTabSelectedSheetIndex];
                ImGui.SetNextItemWidth(-45);
                if (ImGui.InputText("##IndividualCharName", ref sheetNameInput, 50))
                {
                    individualSheetRenameInput[individualTabSelectedSheetIndex] = sheetNameInput;
                    currentSheet.Name = sheetNameInput;
                    plugin.Configuration.IndividualTabSheets = individualTabSheets;
                    plugin.Configuration.Save();
                }

                // Delete sheet button next to sheet name (greyed out if only one sheet)
                ImGui.SameLine(0, 5);
                if (individualTabSheets.Count <= 1)
                {
                    ImGui.BeginDisabled();
                }
                
                if (ImGui.Button("Del", new Vector2(30, 0)))
                {
                    individualTabSheets.RemoveAt(individualTabSelectedSheetIndex);
                    // Clear the rename input for the deleted sheet
                    individualSheetRenameInput.Remove(individualTabSelectedSheetIndex);
                    
                    if (individualTabSelectedSheetIndex >= individualTabSheets.Count)
                        individualTabSelectedSheetIndex = individualTabSheets.Count - 1;
                    plugin.Configuration.IndividualTabSheets = individualTabSheets;
                    plugin.Configuration.IndividualTabSelectedSheetIndex = individualTabSelectedSheetIndex;
                    plugin.Configuration.Save();
                }
                
                if (individualTabSheets.Count <= 1)
                {
                    ImGui.EndDisabled();
                }
                
                // Job dropdown
                int currentJobIdx = System.Array.IndexOf(jobOptions, member.Job);
                if (currentJobIdx < 0) currentJobIdx = 0;
                
                ImGui.SetNextItemWidth(-95);
                if (ImGui.Combo("##IndividualJob", ref currentJobIdx, jobOptions))
                {
                    member.Job = jobOptions[currentJobIdx];
                    var role = Helpers.FFXIVJobs.GetRoleForJob(member.Job);
                    if (role != Models.JobRole.Unknown)
                    {
                        member.Role = role;
                    }
                    plugin.Configuration.IndividualTabSheets = individualTabSheets;
                    plugin.Configuration.Save();
                }

                ImGui.SameLine();
                // Import BiS button
                if (ImGui.Button("Import BiS##IndividualBiS", new Vector2(-1, 0)))
                {
                    ImGui.OpenPopup("Import BiS");
                }

                // Sync buttons
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Button("Sync Self", new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0)))
                {
                    Helpers.EquipmentReader.SyncPlayerEquipmentToMember(member, Plugin.GameInventory);
                    plugin.Configuration.IndividualTabSheets = individualTabSheets;
                    plugin.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Sync Target", new Vector2(-1, 0)))
                {
                    // Execute examine first
                    unsafe
                    {
                        var target = Plugin.TargetManager.Target;
                        if (target != null)
                        {
                            Plugin.Framework.RunOnTick(() =>
                            {
                                AgentInspect.Instance()->ExamineCharacter(target.EntityId);
                                // Wait 2 seconds for examine window to update, then sync
                                Plugin.Framework.RunOnTick(() =>
                                {
                                    Helpers.EquipmentReader.SyncTargetEquipmentToMember(member, Plugin.GameInventory);
                                    plugin.Configuration.IndividualTabSheets = individualTabSheets;
                                    plugin.Configuration.Save();
                                }, TimeSpan.FromSeconds(2));
                            });
                        }
                        else
                        {
                            Plugin.ChatGui.Print("No target selected.");
                        }
                    }
                }

                // Import dialog for xivgear.app
                if (ImGui.BeginPopupModal("Import BiS", ImGuiWindowFlags.AlwaysAutoResize))
                {
                    // Initialize tab state
                    if (!memberBiSSetIndex.ContainsKey(-2))
                        memberBiSSetIndex[-2] = 0; // 0 = Preset, 1 = Custom JSON
                    
                    int importTabIndex = memberBiSSetIndex[-2];
                    
                    // Tab buttons
                    if (ImGui.Button("Preset Sets", new Vector2(150, 0)))
                        importTabIndex = 0;
                    ImGui.SameLine();
                    if (ImGui.Button("Custom JSON", new Vector2(150, 0)))
                        importTabIndex = 1;
                    
                    memberBiSSetIndex[-2] = importTabIndex;
                    ImGui.Separator();

                    if (importTabIndex == 0)
                    {
                        // Tab 1: Preset BiS sets
                        ImGui.TextWrapped("Select a BiS set for this job:");
                        ImGui.Separator();

                        // Convert job name to abbreviation for lookup
                        var jobAbbr = Helpers.FFXIVJobs.GetJobAbbreviation(member.Job);
                        var bisSets = plugin.BiSLibrary.GetBiSSetsForJob(jobAbbr);
                        
                        if (bisSets.Count > 0)
                        {
                            if (!memberBiSSetIndex.ContainsKey(-1))
                                memberBiSSetIndex[-1] = 0;

                            var setNames = bisSets.Select(s => s.Name).ToArray();
                            int selectedIndex = memberBiSSetIndex[-1];
                            
                            ImGui.SetNextItemWidth(300);
                            if (ImGui.Combo("##BiSSetSelectIndividual", ref selectedIndex, setNames))
                            {
                                memberBiSSetIndex[-1] = selectedIndex;
                            }

                            ImGui.Spacing();

                            if (ImGui.Button("Import##PresetImport", new Vector2(100, 0)))
                            {
                                if (selectedIndex >= 0 && selectedIndex < bisSets.Count)
                                {
                                    ImportBiSSet(member, bisSets[selectedIndex]);
                                    ImGui.CloseCurrentPopup();
                                }
                            }

                            ImGui.SameLine();
                            if (ImGui.Button("Cancel##PresetCancel", new Vector2(100, 0)))
                            {
                                ImGui.CloseCurrentPopup();
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), $"No BiS sets available for {member.Job}");
                        }
                    }
                    else if (importTabIndex == 1)
                    {
                        // Tab 2: Custom JSON import - simplified text field
                        ImGui.SetNextItemWidth(-1);
                        ImGui.InputTextMultiline("##CustomJsonInput", ref customXivGearJsonString, 10000, new Vector2(-1, 150), ImGuiInputTextFlags.AllowTabInput);

                        ImGui.Spacing();
                        ImGui.Spacing();

                        // Load and Cancel buttons
                        if (ImGui.Button("Load JSON", new Vector2(100, 0)))
                        {
                            if (!string.IsNullOrWhiteSpace(customXivGearJsonString))
                            {
                                var importedSet = Helpers.XivGearImporter.ImportFromJson(customXivGearJsonString, "Custom Import");
                                if (importedSet != null && importedSet.Items.Count > 0)
                                {
                                    ImportBiSSet(member, importedSet);
                                    customXivGearJsonString = "";
                                    ImGui.CloseCurrentPopup();
                                }
                                else
                                {
                                    Plugin.ChatGui.Print("Failed to parse JSON. Check plugin logs for details.");
                                }
                            }
                            else
                            {
                                Plugin.ChatGui.Print("Please paste JSON into the text field.");
                            }
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Cancel", new Vector2(100, 0)))
                        {
                            customXivGearJsonString = "";
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.SameLine();
                        // Info icon with instructions
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "(?)");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.TextUnformatted("On your xivgear.app sheet:");
                            ImGui.TextUnformatted("• Click Export...");
                            ImGui.TextUnformatted("• Click Selected Sheet");
                            ImGui.TextUnformatted("• Click 'JSON for This Sheet'");
                            ImGui.TextUnformatted("• Copy and paste into the field above");
                            ImGui.TextUnformatted("• Click Load JSON");
                            ImGui.EndTooltip();
                        }
                    }

                    ImGui.EndPopup();
                }
                
                ImGui.Spacing();
                
                DrawGearTableForIndividualMember(member);
                
                ImGui.Spacing();
                
                DrawCurrencyTableForIndividualMember(member, currentSheet, individualTabFloor1Clears, individualTabFloor2Clears, individualTabFloor3Clears, individualTabFloor4Clears);
                
                ImGui.Spacing();
                
                DrawMaterialsTableForIndividualMember(member);
                
                ImGui.EndChild();
            }

            // Right - Mini Who Needs It for individual sheet
            ImGui.NextColumn();
            if (ImGui.BeginChild("IndividualWhoNeedsIt", new Vector2(legendMinWidth - 10, -1), true))
            {
                ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Need It?");
                ImGui.Separator();
                
                // Get all gear slots
                var gearSlots = System.Enum.GetNames(typeof(Models.GearSlot));
                
                // Create mini table with gear slots
                if (ImGui.BeginTable("IndividualWhoNeedsItTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Gear", ImGuiTableColumnFlags.WidthFixed, 80);
                    ImGui.TableSetupColumn("Need", ImGuiTableColumnFlags.WidthFixed, legendMinWidth - 120);
                    
                    ImGui.TableHeadersRow();
                    
                    // Check each gear slot
                    foreach (var slotName in gearSlots)
                    {
                        // Check all sheets and collect which ones need this item
                        var sheetsNeedingGear = new List<string>();
                        
                        foreach (var sheet in individualTabSheets)
                        {
                            bool sheetNeedsThisItem = false;
                            foreach (var sheetMember in sheet.Members)
                            {
                                if (sheetMember.Gear.TryGetValue(slotName, out var gearPiece))
                                {
                                    if (gearPiece.DesiredSource == Models.GearSource.Savage && 
                                        gearPiece.Source != Models.GearSource.Savage)
                                    {
                                        sheetNeedsThisItem = true;
                                        break;
                                    }
                                }
                            }
                            if (sheetNeedsThisItem)
                            {
                                sheetsNeedingGear.Add(sheet.Name);
                            }
                        }
                        
                        bool needsItem = sheetsNeedingGear.Count > 0;
                        
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        
                        if (needsItem)
                        {
                            ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), FormatSlotName(slotName));
                        }
                        else
                        {
                            ImGui.Text(FormatSlotName(slotName));
                        }
                        
                        ImGui.TableSetColumnIndex(1);
                        if (needsItem)
                        {
                            ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "YES");
                            
                            // Add hover text showing all sheets that need this gear
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                foreach (var sheetName in sheetsNeedingGear)
                                {
                                    ImGui.Text(sheetName);
                                }
                                ImGui.EndTooltip();
                            }
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "NO");
                        }
                    }
                    
                    ImGui.EndTable();
                }
                
                ImGui.EndChild();
            }
            
            ImGui.Columns(1);
            ImGui.EndChild();
        }
    }

    private void DrawTeamTab()
    {
        if (plugin.Configuration.RaidTeams.Count == 0)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "No raid teams configured.");
            ImGui.Text("Use /gp config to create a team or edit the sample team.");
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

        // Team selector dropdown
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Select Team:");
        ImGui.SameLine(0, 5);

        var teamNames = plugin.Configuration.RaidTeams.Select(t => t.Name).ToArray();
        int selectedTeamIndexLocal = selectedTeamIndex;
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##TeamSelector", ref selectedTeamIndexLocal, teamNames))
        {
            plugin.Configuration.SelectedTeamIndex = selectedTeamIndexLocal;
            plugin.Configuration.Save();
        }

        // Add new team button
        ImGui.SameLine(0, 5);
        if (ImGui.Button("+", new Vector2(25, 0)))
        {
            int teamNumber = plugin.Configuration.RaidTeams.Count;
            string newTeamName = $"Team {teamNumber + 1}";
            
            // Create new team with default members from first team if available
            var newTeam = new Models.RaidTeam(newTeamName);
            if (plugin.Configuration.RaidTeams.Count > 0)
            {
                var firstTeam = plugin.Configuration.RaidTeams[0];
                newTeam.Members = new List<Models.RaidMember>();
                foreach (var member in firstTeam.Members)
                {
                    var newMember = new Models.RaidMember(member.Name, member.Job, member.Role);
                    newMember.InitializeGear();
                    InitializeGearDefaults(newMember);
                    newTeam.Members.Add(newMember);
                }
            }
            
            plugin.Configuration.RaidTeams.Add(newTeam);
            plugin.Configuration.SelectedTeamIndex = plugin.Configuration.RaidTeams.Count - 1;
            plugin.Configuration.Save();
        }

        // Delete team button
        ImGui.SameLine(0, 5);
        bool isDefaultTeam = plugin.Configuration.RaidTeams.Count > 0 && 
                            plugin.Configuration.RaidTeams[selectedTeamIndex].Name == "Sample Raid Team";
        if (plugin.Configuration.RaidTeams.Count <= 1 || isDefaultTeam)
        {
            ImGui.BeginDisabled();
        }
        
        if (ImGui.Button("Del", new Vector2(30, 0)))
        {
            plugin.Configuration.RaidTeams.RemoveAt(selectedTeamIndex);
            if (plugin.Configuration.SelectedTeamIndex >= plugin.Configuration.RaidTeams.Count)
                plugin.Configuration.SelectedTeamIndex = plugin.Configuration.RaidTeams.Count - 1;
            plugin.Configuration.Save();
        }
        
        if (plugin.Configuration.RaidTeams.Count <= 1 || isDefaultTeam)
        {
            ImGui.EndDisabled();
        }

        ImGui.Spacing();

        // Reload team reference after potential changes
        selectedTeamIndex = plugin.Configuration.SelectedTeamIndex;
        if (selectedTeamIndex < 0 || selectedTeamIndex >= plugin.Configuration.RaidTeams.Count)
            return;
        
        team = plugin.Configuration.RaidTeams[selectedTeamIndex];

        // Team name
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Team Name:");
        ImGui.SameLine(0, 5);
        string teamName = team.Name;
        ImGui.SetNextItemWidth(200);
        if (ImGui.InputText("##TeamName", ref teamName, 100, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            team.Name = teamName;
            plugin.Configuration.Save();
        }

        ImGui.Spacing();

        // Sheet selection dropdown - always get fresh team reference and rebuild sheet list
        var currentTeam = plugin.Configuration.RaidTeams[selectedTeamIndex];
        
        // Validate selected sheet index
        if (currentTeam.SelectedSheetIndex < 0 || currentTeam.SelectedSheetIndex >= currentTeam.Sheets.Count)
        {
            currentTeam.SelectedSheetIndex = currentTeam.Sheets.Count > 0 ? 0 : -1;
            plugin.Configuration.Save();
        }
        
        var sheetNames = currentTeam.Sheets.Select(s => s.Name).ToArray();
        int selectedSheetIndex = currentTeam.SelectedSheetIndex;
        
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Sheet:");
        ImGui.SameLine(0, 5);
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##TeamSheetSelector", ref selectedSheetIndex, sheetNames))
        {
            currentTeam.SelectedSheetIndex = selectedSheetIndex;
            plugin.Configuration.Save();
        }

        // Add new sheet button
        ImGui.SameLine(0, 5);
        if (ImGui.Button("+##AddSheet", new Vector2(25, 0)))
        {
            try
            {
                Plugin.Log.Info("Add sheet button clicked");
                
                // Count existing "Alt Job" sheets to get next number
                int altJobCount = currentTeam.Sheets.Count(s => s.Name.StartsWith("Alt Job"));
                string newSheetName = $"Alt Job {altJobCount + 1}";
                Plugin.Log.Info($"Creating new sheet: {newSheetName}, current sheet count: {currentTeam.Sheets.Count}");
                
                // Create new sheet with copy of current sheet's members
                int currentSheetIdx = currentTeam.SelectedSheetIndex;
                Plugin.Log.Info($"Current sheet index: {currentSheetIdx}, sheets count: {currentTeam.Sheets.Count}");
                
                if (currentSheetIdx < 0 || currentSheetIdx >= currentTeam.Sheets.Count)
                {
                    Plugin.Log.Error($"Invalid sheet index: {currentSheetIdx}");
                    return;
                }
                
                var currentSheet = currentTeam.Sheets[currentSheetIdx];
                Plugin.Log.Info($"Current sheet name: {currentSheet.Name}, members: {currentSheet.Members.Count}");
                
                var newMembers = new List<Models.RaidMember>();
                foreach (var member in currentSheet.Members)
                {
                    var newMember = new Models.RaidMember(member.Name, member.Job, member.Role);
                    newMember.InitializeGear();
                    // For alt job sheets, skip initializing MainHand, Ring1, and Ring2
                    var skipSlots = new HashSet<Models.GearSlot> { Models.GearSlot.MainHand, Models.GearSlot.Ring1, Models.GearSlot.Ring2 };
                    InitializeGearDefaults(newMember, skipSlots);
                    newMembers.Add(newMember);
                }
                
                Plugin.Log.Info($"Created {newMembers.Count} new members");
                
                currentTeam.Sheets.Add(new Models.GearSheet(newSheetName, newMembers));
                currentTeam.SelectedSheetIndex = currentTeam.Sheets.Count - 1;
                plugin.Configuration.Save();
                
                Plugin.Log.Info($"Sheet added successfully. Total sheets now: {currentTeam.Sheets.Count}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error adding sheet: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Delete sheet button (greyed out if main sheet or only one sheet)
        ImGui.SameLine(0, 5);
        
        // Check if we can delete (not Main sheet and more than 1 sheet)
        bool canDeleteSheet = currentTeam.Sheets.Count > 1 && 
                             currentTeam.SelectedSheetIndex >= 0 && 
                             currentTeam.SelectedSheetIndex < currentTeam.Sheets.Count &&
                             currentTeam.Sheets[currentTeam.SelectedSheetIndex].Name != "Main";
        
        if (!canDeleteSheet)
        {
            ImGui.BeginDisabled();
        }
        
        if (ImGui.Button("Del##DeleteSheet", new Vector2(30, 0)))
        {
            Plugin.Log.Info($"Delete button clicked. SelectedSheetIndex: {currentTeam.SelectedSheetIndex}, Sheets count: {currentTeam.Sheets.Count}");
            
            if (currentTeam.SelectedSheetIndex >= 0 && currentTeam.SelectedSheetIndex < currentTeam.Sheets.Count)
            {
                Plugin.Log.Info($"Deleting sheet at index {currentTeam.SelectedSheetIndex}");
                currentTeam.Sheets.RemoveAt(currentTeam.SelectedSheetIndex);
                Plugin.Log.Info($"Sheet deleted. New count: {currentTeam.Sheets.Count}");
                
                // Adjust selection if needed
                if (currentTeam.Sheets.Count == 0)
                {
                    currentTeam.SelectedSheetIndex = -1;
                }
                else if (currentTeam.SelectedSheetIndex >= currentTeam.Sheets.Count)
                {
                    currentTeam.SelectedSheetIndex = currentTeam.Sheets.Count - 1;
                }
                
                Plugin.Log.Info($"New selected index: {currentTeam.SelectedSheetIndex}");
                plugin.Configuration.Save();
            }
        }
        
        if (!canDeleteSheet)
        {
            ImGui.EndDisabled();
        }

        ImGui.Spacing();

        ImGui.Separator();
        ImGui.Spacing();

        // Update team reference for use in member rendering
        team = currentTeam;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        
        if (ImGui.BeginChild("MembersSection", new Vector2(availableWidth, -1), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            // Left - Member tables
            DrawMemberSection(team.Members, team);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.EndChild();
        }
    }

    private void DrawMemberSection(List<Models.RaidMember> members, Models.RaidTeam team)
    {
        const int maxPerRow = 4;
        int rowCount = (int)Math.Ceiling(members.Count / (float)maxPerRow);
        
        for (int rowIdx = 0; rowIdx < rowCount; rowIdx++)
        {
            var rowMembers = members.Skip(rowIdx * maxPerRow).Take(maxPerRow).ToList();
            
            // For first row with 4 members, add an extra column for floor clears
            int columnCount = rowMembers.Count;
            bool showFloorClears = (rowIdx == 0 && rowMembers.Count == maxPerRow);
            if (showFloorClears)
                columnCount++;
            
            if (ImGui.BeginChild($"Section_{rowIdx}_{string.Join("_", rowMembers.Select(m => team.Members.IndexOf(m)))}", 
                new Vector2(columnCount * 355 + (columnCount - 1) * 5, 600), false))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(5, 5));
                ImGui.Columns(columnCount, $"SectionColumns_{rowIdx}_{string.Join("_", rowMembers.Select(m => team.Members.IndexOf(m)))}", false);
                
                for (int i = 0; i < rowMembers.Count; i++)
                {
                    var memberIdx = team.Members.IndexOf(rowMembers[i]);
                    DrawMemberSection(team, memberIdx);
                    ImGui.NextColumn();
                }
                
                // Draw floor clears in the last column of first row
                if (showFloorClears)
                {
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
                }
                
                ImGui.Columns(1);
                ImGui.PopStyleVar();
                ImGui.EndChild();
            }
        }
    }

    private void DrawMemberSection(Models.RaidTeam team, int memberIdx)
    {
        var member = team.Members[memberIdx];
        
        // Constrain to a max width and height
        if (ImGui.BeginChild($"MemberSection{memberIdx}", new System.Numerics.Vector2(350, 600), true))
        {
            // Member name - editable
            string name = member.Name;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText($"##CharName{memberIdx}", ref name, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                // Update member name in current sheet
                member.Name = name;
                
                // Sync name to same member index in all other sheets
                for (int sheetIdx = 0; sheetIdx < team.Sheets.Count; sheetIdx++)
                {
                    if (sheetIdx != team.SelectedSheetIndex && memberIdx < team.Sheets[sheetIdx].Members.Count)
                    {
                        team.Sheets[sheetIdx].Members[memberIdx].Name = name;
                    }
                }
                
                plugin.Configuration.Save();
            }
            
            // Job dropdown
            int currentJobIdx = System.Array.IndexOf(jobOptions, member.Job);
            if (currentJobIdx < 0) currentJobIdx = 0;
            
            ImGui.SetNextItemWidth(-95);
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

            ImGui.SameLine();
            // Import BiS button
            if (ImGui.Button($"Import BiS##Team{memberIdx}", new Vector2(-1, 0)))
            {
                ImGui.OpenPopup($"XivGearImportPopup{memberIdx}");
            }

            // Sync buttons
            if (ImGui.Button("Sync Self", new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0)))
            {
                Helpers.EquipmentReader.SyncPlayerEquipmentToMember(member, Plugin.GameInventory);
                plugin.Configuration.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("Sync Target", new Vector2(-1, 0)))
            {
                // Execute examine first
                unsafe
                {
                    var target = Plugin.TargetManager.Target;
                    if (target != null)
                    {
                        Plugin.Framework.RunOnTick(() =>
                        {
                            AgentInspect.Instance()->ExamineCharacter(target.EntityId);
                            // Wait 2 seconds for examine window to update, then sync
                            Plugin.Framework.RunOnTick(() =>
                            {
                                Helpers.EquipmentReader.SyncTargetEquipmentToMember(member, Plugin.GameInventory);
                                plugin.Configuration.Save();
                            }, TimeSpan.FromSeconds(2));
                        });
                    }
                    else
                    {
                        Plugin.ChatGui.Print("No target selected.");
                    }
                }

            }

            // Import dialog for xivgear.app
            if (ImGui.BeginPopupModal($"XivGearImportPopup{memberIdx}", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped("Select a BiS set for this job:");
                ImGui.Separator();

                // Convert job name to abbreviation for lookup
                var jobAbbr = Helpers.FFXIVJobs.GetJobAbbreviation(member.Job);
                Plugin.Log.Information($"DEBUG: Looking up job '{member.Job}' with abbreviation '{jobAbbr}'");
                var bisSets = plugin.BiSLibrary.GetBiSSetsForJob(jobAbbr);
                Plugin.Log.Information($"DEBUG: Found {bisSets.Count} sets for job {jobAbbr}");
                
                if (bisSets.Count > 0)
                {
                    if (!memberBiSSetIndex.ContainsKey(memberIdx))
                        memberBiSSetIndex[memberIdx] = 0;

                    var setNames = bisSets.Select(s => s.Name).ToArray();
                    int selectedIndex = memberBiSSetIndex[memberIdx];
                    
                    ImGui.SetNextItemWidth(300);
                    if (ImGui.Combo($"##BiSSetSelect{memberIdx}", ref selectedIndex, setNames))
                    {
                        memberBiSSetIndex[memberIdx] = selectedIndex;
                    }

                    ImGui.Spacing();

                    if (ImGui.Button("Import", new Vector2(100, 0)))
                    {
                        if (selectedIndex >= 0 && selectedIndex < bisSets.Count)
                        {
                            ImportBiSSet(member, bisSets[selectedIndex]);
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
                else
                {
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), $"No BiS sets available for {member.Job}");
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(100, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
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
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), FormatSlotName(slotName));

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
                    string desiredSourceDisplay = piece.DesiredSource.ToString() == "None" ? "" : FormatSourceName(piece.DesiredSource.ToString());
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
                        string tooltipText = piece.DesiredSource.ToString();
                        if (piece.DesiredItemId > 0)
                        {
                            var item = Helpers.ItemDatabase.GetItemById(piece.DesiredItemId);
                            if (item != null)
                            {
                                tooltipText = item.Name;
                            }
                        }
                        ImGui.SetTooltip(tooltipText);
                        
                        // Right-click to clear desired
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        {
                            piece.DesiredSource = GearSource.None;
                            piece.DesiredItemId = 0;
                            plugin.Configuration.Save();
                        }
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
                        string tooltipText = piece.Source.ToString();
                        if (piece.CurrentItemId > 0)
                        {
                            var item = Helpers.ItemDatabase.GetItemById(piece.CurrentItemId);
                            if (item != null)
                            {
                                tooltipText = item.Name;
                            }
                        }
                        ImGui.SetTooltip(tooltipText);
                        
                        // Right-click to clear current
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        {
                            piece.Source = GearSource.None;
                            piece.CurrentItemId = 0;
                            plugin.Configuration.Save();
                        }
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
                                piece.CurrentItemId = FindItemIdForGearSlot(member.Job, slotName, piece.Source);
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
                                piece.DesiredItemId = FindItemIdForGearSlot(member.Job, slotName, piece.DesiredSource);
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

            // Spent Books row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Spent Books");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                int pagesFromClears = GetBooksFromClears(team, floor);
                int pageAdjustment = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                int totalBooksAvailable = pagesFromClears + pageAdjustment;
                
                int spentBooks = member.SpentBooks.ContainsKey(floor) ? member.SpentBooks[floor] : 0;
                int oldSpentBooks = spentBooks;
                
                ImGui.SetNextItemWidth(35);
                if (ImGui.InputInt($"##SpentBooks{memberIdx}_{floor}", ref spentBooks))
                {
                    // Validation: value must be >= 0 and <= total books available
                    if (spentBooks < 0)
                    {
                        spentBooks = 0;
                    }
                    else if (spentBooks > totalBooksAvailable)
                    {
                        spentBooks = totalBooksAvailable;
                    }
                    
                    member.SpentBooks[floor] = spentBooks;
                    plugin.Configuration.Save();
                }
            }

            // Books row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Books");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                int pagesFromClears = GetBooksFromClears(team, floor);
                int pageAdjustment = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                int totalBooksAvailable = pagesFromClears + pageAdjustment;
                
                // Initialize Books if not set
                if (!member.BookAdjustments.ContainsKey(floor))
                    member.BookAdjustments[floor] = 0;
                
                // Books is calculated from available books minus spent books
                int spentBooks = member.SpentBooks.ContainsKey(floor) ? member.SpentBooks[floor] : 0;
                int currentBooks = totalBooksAvailable - spentBooks;
                int newBooks = currentBooks;
                
                ImGui.SetNextItemWidth(35);
                if (ImGui.InputInt($"##Books{memberIdx}_{floor}", ref newBooks))
                {
                    // Validation: value must be >= 0 and <= total available
                    if (newBooks < 0)
                    {
                        newBooks = 0;
                    }
                    else if (newBooks > totalBooksAvailable)
                    {
                        newBooks = totalBooksAvailable;
                    }
                    
                    // Calculate the difference and update SpentBooks inversely
                    int difference = currentBooks - newBooks;
                    spentBooks += difference;
                    member.SpentBooks[floor] = spentBooks;
                    
                    plugin.Configuration.Save();
                }
            }

            // Book Adjust row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Book Adjust");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                ImGui.SetNextItemWidth(35);
                int adjustValue = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                if (ImGui.InputInt($"##BookAdjust{memberIdx}_{floor}", ref adjustValue))
                {
                    member.BookAdjustments[floor] = adjustValue;
                    plugin.Configuration.Save();
                }
            }

            // Books Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Books Needed");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                int pagesNeeded = CalculateBooksNeededForFloor(member, floor);
                int pagesFromClears = GetBooksFromClears(team, floor);
                int pageAdjustment = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                int totalBooks = pagesFromClears + pageAdjustment;
                int remainingBooks = Math.Max(0, pagesNeeded - totalBooks);
                ImGui.Text(remainingBooks.ToString());
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

    private int CalculateBooksNeededForFloor(Models.RaidMember member, int floor)
    {
        // Map floors to gear slots that drop on each floor
        // This is based on standard FFXIV Savage tier loot distribution
        List<string> floorSlots = floor switch
        {
            1 => new() { "Ears", "Neck", "Wrists", "Ring2" },
            2 => new() { "Head", "Hands", "Feet", "Glaze" },
            3 => new() { "Body", "Legs", "Twine"},
            4 => new() { "MainHand"},
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
                        "MainHand" => 8,
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

    private int GetBooksFromClears(Models.RaidTeam team, int floor)
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
    private string FormatSlotName(string slotName)
    {
        // Convert camelCase names to spaced names
        return slotName switch
        {
            "MainHand" => "Main Hand",
            "Ring1" => "Ring 1",
            "Ring2" => "Ring 2",
            _ => slotName
        };
    }

    private string FormatSourceName(string sourceName)
    {
        // Convert camelCase source names to spaced names
        return sourceName switch
        {
            "TomeUp" => "Tome Up",
            _ => sourceName
        };
    }

    private void SetAllDesiredSources(List<Models.RaidMember> members, Models.GearSource source)
    {
        foreach (var member in members)
        {
            foreach (var gear in member.Gear.Values)
            {
                gear.DesiredSource = source;
            }
        }
        plugin.Configuration.Save();
    }

    private void SetSavageWithTomeUpAccessories(List<Models.RaidMember> members)
    {
        foreach (var member in members)
        {
            foreach (var gear in member.Gear.Values)
            {
                // TomeUp for accessories (rings, ears, neck, wrists)
                if (gear.Slot == GearSlot.Ring1 || gear.Slot == GearSlot.Ring2 || 
                    gear.Slot == GearSlot.Ears || gear.Slot == GearSlot.Neck || 
                    gear.Slot == GearSlot.Wrists)
                {
                    gear.DesiredSource = GearSource.TomeUp;
                }
                else
                {
                    gear.DesiredSource = GearSource.Savage;
                }
            }
        }
        plugin.Configuration.Save();
    }

    private void ImportBiSSet(Models.RaidMember member, Models.BiSSet biSSet)
    {
        if (biSSet.Items == null || biSSet.Items.Count == 0)
            return;

        // Get the job code for validation
        string jobCode = Helpers.FFXIVJobs.GetJobAbbreviation(member.Job);

        // Map each item in the BiS set to the member's gear
        foreach (var kvp in biSSet.Items)
        {
            string xivGearSlot = kvp.Key;
            var biSItem = kvp.Value;

            // Map slot name from xivgear format to our internal format
            string internalSlot = Helpers.BiSLibrary.MapXivGearSlotToInternal(xivGearSlot);

            if (member.Gear.TryGetValue(internalSlot, out var gearPiece))
            {
                // Validate that the job can equip this item before assigning
                if (!Helpers.ItemDiscoveryHelper.CanJobEquipItemById((uint)biSItem.Id, jobCode, Plugin.DataManager))
                {
                    var skipItemLookup = Helpers.ItemDatabase.GetItemById((uint)biSItem.Id);
                    string skipItemName = skipItemLookup?.Name ?? "Item";
                    Plugin.Log.Debug($"Skipped BiS item for {member.Name} - {internalSlot}: Item ID {biSItem.Id} ({skipItemName}) cannot be equipped by {member.Job} ({jobCode})");
                    continue;
                }

                // Detect the gear source from the item ID using IDataManager
                var gearSource = Helpers.BiSLibrary.DetectGearSourceFromItemId(biSItem.Id);
                gearPiece.DesiredSource = gearSource;
                gearPiece.DesiredItemId = (uint)biSItem.Id;
                
                // Verify the item was found in database
                var itemLookup = Helpers.ItemDatabase.GetItemById((uint)biSItem.Id);
                string itemName = itemLookup?.Name ?? "Item not found in database";
                Plugin.Log.Debug($"Applied BiS for {member.Name} - {internalSlot}: Item ID {biSItem.Id} ({itemName}) (Source: {gearSource})");
            }
        }

        // Re-sync rings to match new desired sources if they're equipped
        var ring1Key = Models.GearSlot.Ring1.ToString();
        var ring2Key = Models.GearSlot.Ring2.ToString();
        
        bool hasRing1 = member.Gear.TryGetValue(ring1Key, out var ring1) && ring1?.CurrentItemId > 0;
        bool hasRing2 = member.Gear.TryGetValue(ring2Key, out var ring2) && ring2?.CurrentItemId > 0;
        
        if (hasRing1 || hasRing2)
        {
            // Create a dictionary of current ring items to re-sync
            var currentRings = new Dictionary<Models.GearSlot, uint>();
            if (hasRing1 && ring1 != null)
                currentRings[Models.GearSlot.Ring1] = ring1.CurrentItemId;
            if (hasRing2 && ring2 != null)
                currentRings[Models.GearSlot.Ring2] = ring2.CurrentItemId;
            
            // Re-apply ring syncing logic with new desired sources
            if (currentRings.Count > 0)
            {
                Helpers.EquipmentReader.SyncRingsToMember(member, currentRings);
                Plugin.Log.Information($"Re-synced rings for {member.Name} after BiS import");
            }
        }

        plugin.Configuration.Save();
        Plugin.Log.Information($"Imported BiS set '{biSSet.Name}' for {member.Name} ({member.Job})");
    }

    private string LookupItemName(int itemId)
    {
        // Placeholder for future item name lookup using IDataManager
        // This will be implemented when we have proper Dalamud API access
        return $"Item {itemId}";
    }

    private uint FindItemIdForGearSlot(string job, string slotName, GearSource source)
    {
        try
        {
            // Get all items matching the selected source category
            var itemsForSource = Helpers.ItemDatabase.GetItemsByCategory(source);
            if (itemsForSource.Count == 0)
                return 0;

            // Filter to items that can be equipped by this job
            var jobCode = Helpers.FFXIVJobs.GetJobCodeFromName(job);
            var itemsForJob = itemsForSource.Where(item => item.Jobs.Contains(jobCode)).ToList();

            if (itemsForJob.Count == 0)
                return 0;

            // Filter to items matching the gear slot
            if (System.Enum.TryParse<GearSlot>(slotName, out var requestedSlot))
            {
                // Special handling for Ring2: accept Ring1 items (rings are interchangeable in FFXIV)
                var filterSlot = requestedSlot == GearSlot.Ring2 ? GearSlot.Ring1 : requestedSlot;
                
                var itemsForSlot = itemsForJob.Where(item => item.Slot == filterSlot).ToList();
                Plugin.Log.Debug($"[FindItemIdForGearSlot] Slot: {slotName} ({requestedSlot}), Job: {job}, Source: {source}");
                Plugin.Log.Debug($"[FindItemIdForGearSlot] Total items for source: {itemsForSource.Count}");
                Plugin.Log.Debug($"[FindItemIdForGearSlot] Items for job: {itemsForJob.Count}");
                Plugin.Log.Debug($"[FindItemIdForGearSlot] Items for slot: {itemsForSlot.Count}");
                
                // Debug: Log all items found for this slot
                foreach (var item in itemsForSlot)
                {
                    Plugin.Log.Debug($"[FindItemIdForGearSlot]   - {item.Id} {item.Name} (Slot: {item.Slot})");
                }
                
                if (itemsForSlot.Count > 0)
                {
                    var firstItem = itemsForSlot.First();
                    Plugin.Log.Debug($"[FindItemIdForGearSlot] Selected item: {firstItem.Id} - {firstItem.Name} (Slot: {firstItem.Slot})");
                    return firstItem.Id;
                }
            }

            // Fallback: return first matching item if no slot match found
            Plugin.Log.Debug($"[FindItemIdForGearSlot] No slot match found, using fallback");
            var fallbackItem = itemsForJob.First();
            Plugin.Log.Debug($"[FindItemIdForGearSlot] Fallback item: {fallbackItem.Id} - {fallbackItem.Name} (Slot: {fallbackItem.Slot})");
            return fallbackItem.Id;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error finding item for slot {slotName}, job {job}, source {source}: {ex.Message}");
            return 0;
        }
    }

    private void DrawGearTableForIndividualMember(Models.RaidMember member)
    {
        var gearSlots = System.Enum.GetNames(typeof(Models.GearSlot));

        if (ImGui.BeginTable("IndividualGearTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
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
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1.0f), FormatSlotName(slotName));

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
                    string desiredSourceDisplay = piece.DesiredSource.ToString() == "None" ? "" : FormatSourceName(piece.DesiredSource.ToString());
                    ImGui.TextColored(desiredColor, desiredSourceDisplay);
                    
                    // Make empty cells clickable with invisible button
                    if (desiredSourceDisplay == "")
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetTextLineHeight());
                        if (ImGui.InvisibleButton($"DesiredGearIndividual_{slotName}", new System.Numerics.Vector2(-1, ImGui.GetTextLineHeight())))
                        {
                            ImGui.OpenPopup($"StatusMenuIndividual_{slotName}");
                        }
                    }
                    else if (ImGui.IsItemClicked())
                    {
                        ImGui.OpenPopup($"StatusMenuIndividual_{slotName}");
                    }
                    
                    // Tooltip for desired
                    if (ImGui.IsItemHovered())
                    {
                        string tooltipText = piece.DesiredSource.ToString();
                        if (piece.DesiredItemId > 0)
                        {
                            var item = Helpers.ItemDatabase.GetItemById(piece.DesiredItemId);
                            if (item != null)
                            {
                                tooltipText = item.Name;
                            }
                        }
                        ImGui.SetTooltip(tooltipText);
                        
                        // Right-click to clear desired
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        {
                            piece.DesiredSource = GearSource.None;
                            piece.DesiredItemId = 0;
                            plugin.Configuration.IndividualTabSheets = individualTabSheets;
                            plugin.Configuration.Save();
                        }
                    }
                    
                    // Current gear column
                    ImGui.TableSetColumnIndex(2);
                    string sourceDisplay = piece.Source.ToString() == "None" ? "" : piece.Source.ToString();
                    ImGui.TextColored(currentColor, sourceDisplay);
                    
                    // Make empty cells clickable with invisible button
                    if (sourceDisplay == "")
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ImGui.GetTextLineHeight());
                        if (ImGui.InvisibleButton($"CurrentGearIndividual_{slotName}", new System.Numerics.Vector2(-1, ImGui.GetTextLineHeight())))
                        {
                            ImGui.OpenPopup($"SourceMenuIndividual_{slotName}");
                        }
                    }
                    else if (ImGui.IsItemClicked())
                    {
                        ImGui.OpenPopup($"SourceMenuIndividual_{slotName}");
                    }
                    
                    // Tooltip showing full info
                    if (ImGui.IsItemHovered())
                    {
                        string tooltipText = piece.Source.ToString();
                        if (piece.CurrentItemId > 0)
                        {
                            var item = Helpers.ItemDatabase.GetItemById(piece.CurrentItemId);
                            if (item != null)
                            {
                                tooltipText = item.Name;
                            }
                        }
                        ImGui.SetTooltip(tooltipText);
                        
                        // Right-click to clear current
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                        {
                            piece.Source = GearSource.None;
                            piece.CurrentItemId = 0;
                            plugin.Configuration.IndividualTabSheets = individualTabSheets;
                            plugin.Configuration.Save();
                        }
                    }
                    
                    // Right-click menu for source
                    if (ImGui.BeginPopup($"SourceMenuIndividual_{slotName}"))
                    {
                        ImGui.TextDisabled("Gear Source:");
                        ImGui.Separator();
                        
                        foreach (var sourceOption in gearSourceOptions)
                        {
                            var sourceEnumValue = sourceOption.Replace(" ", "");
                            if (ImGui.MenuItem(sourceOption, "", piece.Source.ToString() == sourceEnumValue))
                            {
                                piece.Source = System.Enum.Parse<GearSource>(sourceEnumValue);
                                piece.CurrentItemId = FindItemIdForGearSlot(member.Job, slotName, piece.Source);
                                plugin.Configuration.IndividualTabSheets = individualTabSheets;
                                plugin.Configuration.Save();
                            }
                        }
                        
                        ImGui.EndPopup();
                    }
                    
                    // Menu for desired source
                    if (ImGui.BeginPopup($"StatusMenuIndividual_{slotName}"))
                    {
                        ImGui.TextDisabled("Desired Source:");
                        ImGui.Separator();
                        
                        foreach (var sourceOption in gearSourceOptions)
                        {
                            var sourceEnumValue = sourceOption.Replace(" ", "");
                            if (ImGui.MenuItem(sourceOption, "", piece.DesiredSource.ToString() == sourceEnumValue))
                            {
                                piece.DesiredSource = System.Enum.Parse<GearSource>(sourceEnumValue);
                                piece.DesiredItemId = FindItemIdForGearSlot(member.Job, slotName, piece.DesiredSource);
                                plugin.Configuration.IndividualTabSheets = individualTabSheets;
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

    private void DrawCurrencyTableForIndividualMember(Models.RaidMember member, Models.GearSheet sheet, int floor1Clears, int floor2Clears, int floor3Clears, int floor4Clears)
    {
        if (ImGui.BeginTable("IndividualCurrencyTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            // Setup columns
            ImGui.TableSetupColumn("Currency", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Floor 1", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Floor 2", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Floor 3", ImGuiTableColumnFlags.WidthFixed, 45);
            ImGui.TableSetupColumn("Floor 4", ImGuiTableColumnFlags.WidthFixed, 45);

            // Header row
            ImGui.TableHeadersRow();

            // Spent Books row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Spent Books");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                int floorClears = floor switch
                {
                    1 => floor1Clears,
                    2 => floor2Clears,
                    3 => floor3Clears,
                    4 => floor4Clears,
                    _ => 0
                };
                
                int pageAdjustment = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                int totalBooksAvailable = member.BooksEarned + floorClears + pageAdjustment;
                
                int spentBooks = member.SpentBooks.ContainsKey(floor) ? member.SpentBooks[floor] : 0;
                int oldSpentBooks = spentBooks;
                
                ImGui.SetNextItemWidth(35);
                if (ImGui.InputInt($"##IndividualSpentBooks_{floor}", ref spentBooks))
                {
                    // Validation: value must be >= 0 and <= total books available
                    if (spentBooks < 0)
                    {
                        spentBooks = 0;
                    }
                    else if (spentBooks > totalBooksAvailable)
                    {
                        spentBooks = totalBooksAvailable;
                    }
                    
                    member.SpentBooks[floor] = spentBooks;
                    plugin.Configuration.Save();
                }
            }

            // Books row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Books");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                int floorClears = floor switch
                {
                    1 => floor1Clears,
                    2 => floor2Clears,
                    3 => floor3Clears,
                    4 => floor4Clears,
                    _ => 0
                };
                
                int pageAdjustment = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                int totalBooksAvailable = member.BooksEarned + floorClears + pageAdjustment;
                
                // Books is calculated from available books minus spent books
                int spentBooks = member.SpentBooks.ContainsKey(floor) ? member.SpentBooks[floor] : 0;
                int currentBooks = totalBooksAvailable - spentBooks;
                int newBooks = currentBooks;
                
                ImGui.SetNextItemWidth(35);
                if (ImGui.InputInt($"##IndividualBooks_{floor}", ref newBooks))
                {
                    // Validation: value must be >= 0 and <= total available
                    if (newBooks < 0)
                    {
                        newBooks = 0;
                    }
                    else if (newBooks > totalBooksAvailable)
                    {
                        newBooks = totalBooksAvailable;
                    }
                    
                    // Calculate the difference and update SpentBooks inversely
                    int difference = currentBooks - newBooks;
                    spentBooks += difference;
                    member.SpentBooks[floor] = spentBooks;
                    
                    plugin.Configuration.Save();
                }
            }

            // Book Adjust row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Book Adjust");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                ImGui.SetNextItemWidth(35);
                int adjustValue = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                if (ImGui.InputInt($"##IndividualBookAdjust_{floor}", ref adjustValue))
                {
                    member.BookAdjustments[floor] = adjustValue;
                    plugin.Configuration.Save();
                }
            }

            // Books Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Books Needed");
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableSetColumnIndex(floor);
                int floorClears = floor switch
                {
                    1 => floor1Clears,
                    2 => floor2Clears,
                    3 => floor3Clears,
                    4 => floor4Clears,
                    _ => 0
                };
                
                int pagesNeeded = CalculateBooksNeededForFloor(member, floor);
                int pageAdjustment = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                int totalBooks = member.BooksEarned + floorClears + pageAdjustment;
                int remainingBooks = Math.Max(0, pagesNeeded - totalBooks);
                ImGui.Text(remainingBooks.ToString());
            }

            ImGui.EndTable();
        }
    }

    private void DrawMaterialsTableForIndividualMember(Models.RaidMember member)
    {
        if (ImGui.BeginTable("IndividualMaterialsTable", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
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

    private void InitializeGearDefaults(Models.RaidMember member)
    {
        InitializeGearDefaults(member, skipSlots: null);
    }

    private void InitializeGearDefaults(Models.RaidMember member, HashSet<Models.GearSlot>? skipSlots)
    {
        foreach (var gear in member.Gear.Values)
        {
            gear.DesiredStatus = Models.GearStatus.BiS;
            gear.CurrentStatus = Models.GearStatus.LowIlvl;
            gear.Source = Models.GearSource.None;
            
            // Skip desired source initialization for specified slots
            if (skipSlots != null && skipSlots.Contains(gear.Slot))
            {
                gear.DesiredSource = Models.GearSource.None;
                continue;
            }
            
            // Set specific desired source defaults
            if (gear.Slot == Models.GearSlot.MainHand)
            {
                gear.DesiredSource = Models.GearSource.Savage;
            }
            else if (gear.Slot == Models.GearSlot.Ring1)
            {
                gear.DesiredSource = Models.GearSource.TomeUp;
            }
            else if (gear.Slot == Models.GearSlot.Ring2)
            {
                gear.DesiredSource = Models.GearSource.Savage;
            }
            else
            {
                gear.DesiredSource = Models.GearSource.None;
            }
        }
    }

    private void DrawLegendForIndividualTab()
    {
        // Set default text color to grey
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
        
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Legend");
        ImGui.SameLine(200);
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Weapon");
        ImGui.SameLine(260);
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "ilvl");
        ImGui.Separator();

        ImGui.TextColored(new Vector4(0.0f, 0.5f, 1.0f, 1.0f), "Savage");
        ImGui.SameLine(200);
        ImGui.Text("795");
        ImGui.SameLine(260);
        ImGui.Text("790");
        ImGui.TextWrapped("Drops from Savage raid.");

        ImGui.TextColored(new Vector4(0.0f, 0.5f, 1.0f, 1.0f), "Tome Up");
        ImGui.SameLine(200);
        ImGui.Text("");
        ImGui.SameLine(260);
        ImGui.Text("");
        ImGui.TextWrapped("Upgraded capped tome gear.");

        ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Catchup");
        ImGui.SameLine(200);
        ImGui.Text("785");
        ImGui.SameLine(260);
        ImGui.Text("780");
        ImGui.TextWrapped("Aug, crafted, drops from mid-tier EX, alliance raid, etc.");

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Tome");
        ImGui.SameLine(200);
        ImGui.Text("780");
        ImGui.SameLine(260);
        ImGui.Text("");
        ImGui.TextWrapped("Non-upgraded capped tome gear.");

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), "Relic");
        ImGui.SameLine(200);
        ImGui.Text("775");
        ImGui.SameLine(260);
        ImGui.Text("770");
        ImGui.TextWrapped("Continuously upgradeable personalized gear.");

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), "Crafted");
        ImGui.SameLine(200);
        ImGui.Text("");
        ImGui.SameLine(260);
        ImGui.Text("770");
        ImGui.TextWrapped("High-quality crafted gear.");

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), "Prep");
        ImGui.SameLine(200);
        ImGui.Text("775");
        ImGui.SameLine(260);
        ImGui.Text("770");
        ImGui.TextWrapped("Drops from EX, normal raid, etc.");

        ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "Trash");
        ImGui.SameLine(200);
        ImGui.Text("765");
        ImGui.SameLine(260);
        ImGui.Text("760");
        ImGui.TextWrapped("Uncapped tome gear, dungeon gear, last tier's BiS, etc.");

        ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "Wow");
        ImGui.SameLine(200);
        ImGui.Text("745");
        ImGui.SameLine(260);
        ImGui.Text("740");
        ImGui.TextWrapped("You aren't seriously considering raiding with this, are you?");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Color Guide");
        ImGui.Separator();

        ImGui.TextColored(new Vector4(0.8f, 0.0f, 1.0f, 1.0f), "Purple");
        ImGui.TextWrapped("Already have the desired gear.");

        ImGui.TextColored(new Vector4(0.0f, 0.5f, 1.0f, 1.0f), "Blue");
        ImGui.TextWrapped("Already at/near max ilv.");

        ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Green");
        ImGui.TextWrapped("Intermediate gear.");

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Yellow");
        ImGui.TextWrapped("Needs 1 of the 3 upgrade tokens. (Chest/Pants bold.)");

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), "White");
        ImGui.TextWrapped("Potential for significant improvement.");

        ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "Red");
        ImGui.TextWrapped("Upgrade this ASAP");

        ImGui.PopStyleColor();
    }

    private void DrawLootPlannerTab() //test12
    {
        if (plugin.Configuration.RaidTeams.Count == 0)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "No raid teams configured.");
            ImGui.Text("Use /gp config to create a team or edit the sample team.");
            return;
        }

        var selectedTeamIndex = plugin.Configuration.SelectedTeamIndex;
        if (selectedTeamIndex < 0 || selectedTeamIndex >= plugin.Configuration.RaidTeams.Count)
        {
            selectedTeamIndex = 0;
            plugin.Configuration.SelectedTeamIndex = 0;
        }

        var team = plugin.Configuration.RaidTeams[selectedTeamIndex];

        ImGui.Spacing();
        ImGui.Spacing();

        // Team selector
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Select Team:");
        ImGui.SameLine(0, 20);

        var teamNames = plugin.Configuration.RaidTeams.Select(t => t.Name).ToArray();
        int selectedTeamIndexLocal = selectedTeamIndex;
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##LootPlannerTeamSelector", ref selectedTeamIndexLocal, teamNames))
        {
            plugin.Configuration.SelectedTeamIndex = selectedTeamIndexLocal;
            plugin.Configuration.Save();
            team = plugin.Configuration.RaidTeams[selectedTeamIndexLocal];
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        // Week selector
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Select Week:");
        ImGui.SameLine(0, 20);

        var weekNames = lootPlannerWeeks.ToArray();
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##LootPlannerWeekSelector", ref lootPlannerSelectedWeekIndex, weekNames))
        {
            if (lootPlannerSelectedWeekIndex < 0)
                lootPlannerSelectedWeekIndex = 0;
            plugin.Configuration.LootPlannerSelectedWeekIndex = lootPlannerSelectedWeekIndex;
            plugin.Configuration.Save();
        }

        // Add new week button
        ImGui.SameLine(0, 5);
        if (ImGui.Button("+", new Vector2(25, 0)))
        {
            string newWeekName = $"Week {lootPlannerWeeks.Count + 1}";
            lootPlannerWeeks.Add(newWeekName);
            lootPlannerSelectedWeekIndex = lootPlannerWeeks.Count - 1;
            plugin.Configuration.LootPlannerWeeks = lootPlannerWeeks;
            plugin.Configuration.LootPlannerSelectedWeekIndex = lootPlannerSelectedWeekIndex;
            plugin.Configuration.Save();
        }

        // Delete week button (greyed out if only one week)
        ImGui.SameLine(0, 5);
        if (lootPlannerWeeks.Count <= 1)
        {
            ImGui.BeginDisabled();
        }
        
        if (ImGui.Button("Del", new Vector2(30, 0)))
        {
            lootPlannerWeeks.RemoveAt(lootPlannerSelectedWeekIndex);
            if (lootPlannerSelectedWeekIndex >= lootPlannerWeeks.Count)
                lootPlannerSelectedWeekIndex = lootPlannerWeeks.Count - 1;
            plugin.Configuration.LootPlannerWeeks = lootPlannerWeeks;
            plugin.Configuration.LootPlannerSelectedWeekIndex = lootPlannerSelectedWeekIndex;
            plugin.Configuration.Save();
        }
        
        if (lootPlannerWeeks.Count <= 1)
        {
            ImGui.EndDisabled();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Define all loot drops by floor
        var lootByFloor = new Dictionary<int, List<(string Name, int Floor)>>
        {
            { 1, new() { ("Earring", 1), ("Necklace", 1), ("Bracelet", 1), ("Ring", 1) } },
            { 2, new() { ("Head", 2), ("Hands", 2), ("Feet", 2), ("Glaze", 2), ("Token", 2) } },
            { 3, new() { ("Body", 3), ("Legs", 3), ("Twine", 3), ("Solvent", 3) } },
            { 4, new() { ("Weapon", 4), ("Coffer", 4), ("Music", 4), ("Mount", 4) } }
        };

        // Create member name list for dropdowns
        var memberNames = team.Members.Select((m, idx) => m.Name).Prepend("Unassigned").Append("FFA").ToArray();

        // Get current week name for key
        string currentWeek = lootPlannerSelectedWeekIndex >= 0 && lootPlannerSelectedWeekIndex < lootPlannerWeeks.Count 
            ? lootPlannerWeeks[lootPlannerSelectedWeekIndex] 
            : "Week 1";

        // Loot table with max width/height
        var availableWidth = ImGui.GetContentRegionAvail().X;
        float maxTableWidth = Math.Min(availableWidth, 350);
        
        if (ImGui.BeginChild("LootPlannerTable", new Vector2(maxTableWidth, 635), true))
        {
            // Start the loot table
            if (ImGui.BeginTable("LootPlanner", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Floor", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Loot Item", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Assigned To", ImGuiTableColumnFlags.WidthFixed, 150);

                ImGui.TableHeadersRow();

                // Display each floor's loot
                for (int floor = 1; floor <= 4; floor++)
                {
                    var floorLoot = lootByFloor[floor];

                    for (int i = 0; i < floorLoot.Count; i++)
                    {
                        var (lootName, _) = floorLoot[i];
                        var lootKey = $"{currentWeek}_Floor{floor}_{lootName}";

                        ImGui.TableNextRow(ImGuiTableRowFlags.None, 35);

                        // Floor column (only show for first item of floor)
                        ImGui.TableSetColumnIndex(0);
                        if (i == 0)
                        {
                            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.0f, 1.0f), $"Floor {floor}");
                        }

                        // Loot Item
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(lootName);

                        // Show hover text with members who need this gear
                        if (ImGui.IsItemHovered())
                        {
                            var membersNeedingGear = new List<(string name, bool isMain)>();
                            var altMembersNeedingGear = new List<(string name, string sheetName)>();

                            // Map loot names to gear slots
                            var slotMap = new Dictionary<string, string>
                            {
                                { "Earring", "Ears" },
                                { "Necklace", "Neck" },
                                { "Bracelet", "Wrists" },
                                { "Ring", "Ring1" },
                                { "Head", "Head" },
                                { "Hands", "Hands" },
                                { "Feet", "Feet" },
                                { "Body", "Body" },
                                { "Legs", "Legs" },
                                { "Weapon", "MainHand" }
                            };

                            if (slotMap.TryGetValue(lootName, out var slotName))
                            {
                                // For rings, check both Ring1 and Ring2
                                var slotsToCheck = lootName == "Ring" ? new[] { "Ring1", "Ring2" } : new[] { slotName };

                                // Always check the main sheet (index 0)
                                var mainSheet = team.Sheets.Count > 0 ? team.Sheets[0] : null;
                                if (mainSheet != null)
                                {
                                    // Check main sheet members
                                    for (int memberIdx = 0; memberIdx < mainSheet.Members.Count; memberIdx++)
                                    {
                                        var member = mainSheet.Members[memberIdx];
                                        bool needsForMain = false;
                                    
                                    // Check all relevant slots
                                    foreach (var slot in slotsToCheck)
                                    {
                                        if (member.Gear.TryGetValue(slot, out var gearPiece))
                                        {
                                            if (gearPiece.DesiredSource == Models.GearSource.Savage && gearPiece.Source != Models.GearSource.Savage)
                                            {
                                                needsForMain = true;
                                                break;
                                            }
                                        }
                                    }
                                    
                                    if (needsForMain)
                                    {
                                        membersNeedingGear.Add((member.Name, true));
                                        continue;
                                    }

                                    // Check alts if main doesn't need
                                    for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                                    {
                                        var sheet = team.Sheets[sheetIdx];
                                        if (memberIdx < sheet.Members.Count)
                                        {
                                            var altMember = sheet.Members[memberIdx];
                                            bool needsForAlt = false;
                                            
                                            // Check all relevant slots for alt
                                            foreach (var slot in slotsToCheck)
                                            {
                                                if (altMember.Gear.TryGetValue(slot, out var altGearPiece))
                                                {
                                                    if (altGearPiece.DesiredSource == Models.GearSource.Savage && altGearPiece.Source != Models.GearSource.Savage)
                                                    {
                                                        needsForAlt = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            
                                            if (needsForAlt)
                                            {
                                                altMembersNeedingGear.Add((member.Name, sheet.Name));
                                                break;
                                            }
                                        }
                                    }
                                }

                                if (membersNeedingGear.Count > 0 || altMembersNeedingGear.Count > 0)
                                {
                                    ImGui.BeginTooltip();
                                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Members who need this:");
                                    
                                    // Show main sheet needs in red first
                                    foreach (var (name, _) in membersNeedingGear)
                                    {
                                        ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), $"  {name}");
                                    }
                                    
                                    // Then show alt needs in pink
                                    foreach (var (name, sheetName) in altMembersNeedingGear)
                                    {
                                        ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"  {name} ({sheetName})");
                                    }
                                    
                                    ImGui.EndTooltip();
                                }
                                }
                            }
                        }

                        // Assigned To (dropdown)
                        ImGui.TableSetColumnIndex(2);
                        if (!lootPlannerAssignments.ContainsKey(lootKey))
                            lootPlannerAssignments[lootKey] = 0; // Default to unassigned

                        int selectedMember = lootPlannerAssignments[lootKey];
                        
                        // Center dropdown vertically
                        float cursorY = ImGui.GetCursorPosY();
                        ImGui.SetCursorPosY(cursorY + (35 - ImGui.GetTextLineHeight()) / 2);
                        
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.Combo($"##LootAssign{lootKey}", ref selectedMember, memberNames))
                        {
                            lootPlannerAssignments[lootKey] = selectedMember;
                            plugin.Configuration.LootPlannerAssignments = lootPlannerAssignments;
                            plugin.Configuration.Save();
                        }
                    }
                }

                ImGui.EndTable();
            }
            ImGui.EndChild();
        }
    }

    private void DrawWhoNeedsItTab()
    {
        if (plugin.Configuration.RaidTeams.Count == 0)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "No raid teams configured.");
            ImGui.Text("Use /gp config to create a team or edit the sample team.");
            return;
        }

        var selectedTeamIndex = plugin.Configuration.SelectedTeamIndex;
        if (selectedTeamIndex < 0 || selectedTeamIndex >= plugin.Configuration.RaidTeams.Count)
        {
            selectedTeamIndex = 0;
            plugin.Configuration.SelectedTeamIndex = 0;
        }

        var team = plugin.Configuration.RaidTeams[selectedTeamIndex];

        ImGui.Spacing();

        // Team selector
        ImGui.TextColored(new Vector4(0.0f, 1.0f, 1.0f, 1.0f), "Select Team:");
        ImGui.SameLine(0, 5);

        var teamNames = plugin.Configuration.RaidTeams.Select(t => t.Name).ToArray();
        int selectedTeamIndexLocal = selectedTeamIndex;
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##WhoNeedsItTeamSelector", ref selectedTeamIndexLocal, teamNames))
        {
            plugin.Configuration.SelectedTeamIndex = selectedTeamIndexLocal;
            plugin.Configuration.Save();
            team = plugin.Configuration.RaidTeams[selectedTeamIndexLocal];
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Get all gear slots
        var gearSlots = System.Enum.GetNames(typeof(Models.GearSlot));

        // Create a child container with max width
        float maxTableWidth = 1030;
        float availableWidth = ImGui.GetContentRegionAvail().X;
        float childWidth = Math.Min(availableWidth, maxTableWidth);
        ImGui.BeginChild("WhoNeedsItContainer", new Vector2(childWidth, -1), false);

        // Create table with gear slots as rows and members as columns
        int columnCount = team.Members.Count + 1; // +1 for gear slot name column
        if (ImGui.BeginTable("WhoNeedsIt", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            // Setup columns - first column for gear slot names, rest for members
            ImGui.TableSetupColumn("Gear Slot", ImGuiTableColumnFlags.WidthFixed, 120);
            foreach (var member in team.Members)
            {
                ImGui.TableSetupColumn(member.Name, ImGuiTableColumnFlags.WidthFixed, 100);
            }

            ImGui.TableHeadersRow();

            // Display each gear slot
            foreach (var slotName in gearSlots)
            {
                ImGui.TableNextRow();

                // Gear slot name column with color coding
                ImGui.TableSetColumnIndex(0);
                Vector4 slotColor = slotName switch
                {
                    "MainHand" => new Vector4(0.5f, 0.8f, 1.0f, 1.0f),      // Light Blue for Main Hand
                    "Head" or "Hands" or "Feet" => new Vector4(1.0f, 1.0f, 0.0f, 1.0f),  // Yellow
                    "Body" or "Legs" => new Vector4(0.0f, 1.0f, 0.0f, 1.0f), // Green
                    "Ears" or "Neck" or "Wrists" or "Ring1" or "Ring2" => new Vector4(1.0f, 0.3f, 0.3f, 1.0f), // Light Red
                    _ => new Vector4(1.0f, 1.0f, 1.0f, 1.0f)  // White default
                };
                ImGui.TextColored(slotColor, FormatSlotName(slotName));

                // Build tooltip of who needs this gear
                var membersNeedingGear = new List<(string name, bool isMain)>();
                var altMembersNeedingGear = new List<(string name, int sheetIndex)>();
                
                for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
                {
                    var member = team.Members[memberIdx];
                    var mainSheet = team.Sheets.Count > 0 ? team.Sheets[0] : null;
                    var mainSheetMember = mainSheet != null && memberIdx < mainSheet.Members.Count ? mainSheet.Members[memberIdx] : null;
                    
                    if (mainSheetMember != null && mainSheetMember.Gear.TryGetValue(slotName, out var mainGearPiece))
                    {
                        bool needsForMain = mainGearPiece.DesiredSource == Models.GearSource.Savage && mainGearPiece.Source != Models.GearSource.Savage;
                        if (needsForMain)
                        {
                            membersNeedingGear.Add((member.Name, true));
                            continue;
                        }
                    }
                    
                    // Check alts if main doesn't need
                    for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                    {
                        var sheet = team.Sheets[sheetIdx];
                        if (memberIdx < sheet.Members.Count)
                        {
                            var altMember = sheet.Members[memberIdx];
                            if (altMember.Gear.TryGetValue(slotName, out var altGearPiece))
                            {
                                if (altGearPiece.DesiredSource == Models.GearSource.Savage && altGearPiece.Source != Models.GearSource.Savage)
                                {
                                    altMembersNeedingGear.Add((member.Name, sheetIdx));
                                    break;
                                }
                            }
                        }
                    }
                }

                // Sort alt members by sheet index
                altMembersNeedingGear = altMembersNeedingGear.OrderBy(x => x.sheetIndex).ToList();

                if (ImGui.IsItemHovered() && (membersNeedingGear.Count > 0 || altMembersNeedingGear.Count > 0))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Members who need this:");
                    
                    // Show main sheet needs in red first
                    foreach (var (name, _) in membersNeedingGear)
                    {
                        ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), $"  {name}");
                    }
                    
                    // Then show alt needs in pink, sorted by sheet index
                    foreach (var (name, sheetIdx) in altMembersNeedingGear)
                    {
                        ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"  {name} (Alt Job {sheetIdx})");
                    }
                    
                    ImGui.EndTooltip();
                }

                // Member columns with checkboxes
                for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
                {
                    ImGui.TableSetColumnIndex(memberIdx + 1);

                    var member = team.Members[memberIdx];

                    // Always check Main sheet (sheet index 0) first
                    var mainSheet = team.Sheets.Count > 0 ? team.Sheets[0] : null;
                    var mainSheetMember = mainSheet != null && memberIdx < mainSheet.Members.Count ? mainSheet.Members[memberIdx] : null;
                    
                    if (mainSheetMember != null && mainSheetMember.Gear.TryGetValue(slotName, out var mainGearPiece))
                    {
                        bool needsForMain = mainGearPiece.DesiredSource == Models.GearSource.Savage && mainGearPiece.Source != Models.GearSource.Savage;
                        
                        // Check alt job sheets for this member (only if Main doesn't need it)
                        int altSheetWithNeed = -1;
                        if (!needsForMain)
                        {
                            for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                            {
                                var sheet = team.Sheets[sheetIdx];
                                if (memberIdx < sheet.Members.Count)
                                {
                                    var altMember = sheet.Members[memberIdx];
                                    if (altMember.Gear.TryGetValue(slotName, out var altGearPiece))
                                    {
                                        if (altGearPiece.DesiredSource == Models.GearSource.Savage && altGearPiece.Source != Models.GearSource.Savage)
                                        {
                                            altSheetWithNeed = sheetIdx;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (needsForMain || altSheetWithNeed >= 0)
                        {
                            // Create unique key for checkbox
                            string checkboxKey = $"WhoNeedsIt_{memberIdx}_{slotName}";

                            // Initialize checkbox state if not exists
                            if (!whoNeedsItCheckboxes.ContainsKey(checkboxKey))
                            {
                                whoNeedsItCheckboxes[checkboxKey] = false;
                            }

                            // Get cell position for hover detection
                            var cellMin = ImGui.GetCursorScreenPos();
                            var cellMax = cellMin + new Vector2(100, ImGui.GetFrameHeight());

                            bool isChecked = whoNeedsItCheckboxes[checkboxKey];
                            if (isChecked)
                            {
                                ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "HAVE");
                            }
                            else
                            {
                                // Prioritize Main sheet need (red) over alt sheet need (pink)
                                if (needsForMain)
                                {
                                    ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "NEED");
                                    if (ImGui.IsMouseHoveringRect(cellMin, cellMax))
                                    {
                                        ImGui.BeginTooltip();
                                        string jobName = mainSheetMember?.Job ?? "Unknown Job";
                                        ImGui.TextUnformatted($"Needed for: {jobName}");
                                        ImGui.EndTooltip();
                                    }
                                }
                                else if (altSheetWithNeed >= 0)
                                {
                                    // Pink color for alt sheet needs
                                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"NEED (Alt Job {altSheetWithNeed})");
                                    if (ImGui.IsMouseHoveringRect(cellMin, cellMax))
                                    {
                                        ImGui.BeginTooltip();
                                        var altSheet = team.Sheets[altSheetWithNeed];
                                        var altMemberInSheet = memberIdx < altSheet.Members.Count ? altSheet.Members[memberIdx] : null;
                                        string altJobName = altMemberInSheet?.Job ?? "Unknown Job";
                                        ImGui.TextUnformatted($"Needed for: {altJobName}");
                                        ImGui.EndTooltip();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        // Create separate table for Books and Materials summary
        if (ImGui.BeginTable("WhoNeedsItSummary", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            // Setup columns to match the first table
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 120);
            foreach (var member in team.Members)
            {
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 100);
            }
            
            // No header row for summary table
            
            // Books Needed by floor rows
            for (int floor = 1; floor <= 4; floor++)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                
                // Color-code the floor books text
                Vector4 floorColor = floor switch
                {
                    1 => new Vector4(1.0f, 0.3f, 0.3f, 1.0f),      // Light Red for Floor 1
                    2 => new Vector4(1.0f, 1.0f, 0.0f, 1.0f),      // Yellow for Floor 2
                    3 => new Vector4(0.0f, 1.0f, 0.0f, 1.0f),      // Green for Floor 3
                    4 => new Vector4(0.5f, 0.8f, 1.0f, 1.0f),      // Light Blue for Floor 4
                    _ => new Vector4(0.0f, 1.0f, 1.0f, 1.0f)       // Cyan default
                };
                
                ImGui.TextColored(floorColor, $"Floor {floor} Books");

                // Always use main sheet members for pages
                var mainSheetMembers = team.Sheets.Count > 0 ? team.Sheets[0].Members : new List<Models.RaidMember>();
                
                for (int memberIdx = 0; memberIdx < mainSheetMembers.Count; memberIdx++)
                {
                    ImGui.TableSetColumnIndex(memberIdx + 1);
                    var member = mainSheetMembers[memberIdx];
                    
                    // Main sheet pages needed
                    int pagesNeeded = CalculateBooksNeededForFloor(member, floor);
                    int pagesFromClears = GetBooksFromClears(team, floor);
                    int pageAdjustment = member.BookAdjustments.ContainsKey(floor) ? member.BookAdjustments[floor] : 0;
                    int totalBooks = pagesFromClears + pageAdjustment;
                    int remainingBooks = Math.Max(0, pagesNeeded - totalBooks);
                    
                    // Check alt sheets for pages needed
                    var altBooksBreakdown = new List<int>();
                    if (team.Sheets.Count > 1)
                    {
                        for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                        {
                            var sheet = team.Sheets[sheetIdx];
                            if (memberIdx < sheet.Members.Count)
                            {
                                var altMember = sheet.Members[memberIdx];
                                int altBooksForSheet = CalculateBooksNeededForFloor(altMember, floor);
                                altBooksBreakdown.Add(altBooksForSheet);
                            }
                        }
                    }
                    
                    int totalAltBooks = altBooksBreakdown.Sum();
                    
                    // Get cell position for hover detection
                    ImGui.TableSetColumnIndex(memberIdx + 1);
                    var cellMin = ImGui.GetCursorScreenPos();
                    var cellMax = cellMin + new Vector2(100, ImGui.GetFrameHeight());
                    
                    // Display main pages, then alt pages in pink if there are any
                    ImGui.Text(remainingBooks.ToString());
                    
                    if (totalAltBooks > 0)
                    {
                        ImGui.SameLine(0, 5);
                        ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"+{totalAltBooks}");
                    }
                    
                    // Show tooltip if cell is hovered
                    if (ImGui.IsMouseHoveringRect(cellMin, cellMax))
                    {
                        ImGui.BeginTooltip();
                        ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), $"Books needed for {member.Name}:");
                        ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), $"  Main: {pagesNeeded}");
                        
                        for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                        {
                            int altBooksForThisSheet = sheetIdx - 1 < altBooksBreakdown.Count ? altBooksBreakdown[sheetIdx - 1] : 0;
                            ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"  Alt Job {sheetIdx}: {altBooksForThisSheet}");
                        }
                        
                        ImGui.EndTooltip();
                    }
                }
            }

            // Glazes Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Glazes Needed");

            for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
            {
                ImGui.TableSetColumnIndex(memberIdx + 1);
                var member = team.Members[memberIdx];
                int glazesNeeded = CalculateGlazesNeeded(member);
                
                // Check alt sheets for glazes needed
                var altGlazesBreakdown = new List<int>();
                if (team.Sheets.Count > 1)
                {
                    for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                    {
                        var sheet = team.Sheets[sheetIdx];
                        if (memberIdx < sheet.Members.Count)
                        {
                            var altMember = sheet.Members[memberIdx];
                            int altGlazesForSheet = CalculateGlazesNeeded(altMember);
                            altGlazesBreakdown.Add(altGlazesForSheet);
                        }
                    }
                }
                
                int totalAltGlazes = altGlazesBreakdown.Sum();
                
                // Get cell position for hover detection
                var cellMin = ImGui.GetCursorScreenPos();
                var cellMax = cellMin + new Vector2(100, ImGui.GetFrameHeight());
                
                // Display main glazes, then alt glazes in pink if there are any
                ImGui.Text(glazesNeeded.ToString());
                
                if (totalAltGlazes > 0)
                {
                    ImGui.SameLine(0, 5);
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"+{totalAltGlazes}");
                }
                
                // Show tooltip if cell is hovered
                if (ImGui.IsMouseHoveringRect(cellMin, cellMax))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), $"Glazes needed for {member.Name}:");
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), $"  Main: {glazesNeeded}");
                    
                    for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                    {
                        int altGlazesForThisSheet = sheetIdx - 1 < altGlazesBreakdown.Count ? altGlazesBreakdown[sheetIdx - 1] : 0;
                        ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"  Alt Job {sheetIdx}: {altGlazesForThisSheet}");
                    }
                    
                    ImGui.EndTooltip();
                }
            }

            // Twines Needed row
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "Twines Needed");

            for (int memberIdx = 0; memberIdx < team.Members.Count; memberIdx++)
            {
                ImGui.TableSetColumnIndex(memberIdx + 1);
                var member = team.Members[memberIdx];
                int twinesNeeded = CalculateTwinesNeeded(member);
                
                // Check alt sheets for twines needed
                var altTwinesBreakdown = new List<int>();
                if (team.Sheets.Count > 1)
                {
                    for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                    {
                        var sheet = team.Sheets[sheetIdx];
                        if (memberIdx < sheet.Members.Count)
                        {
                            var altMember = sheet.Members[memberIdx];
                            int altTwinesForSheet = CalculateTwinesNeeded(altMember);
                            altTwinesBreakdown.Add(altTwinesForSheet);
                        }
                    }
                }
                
                int totalAltTwines = altTwinesBreakdown.Sum();
                
                // Get cell position for hover detection
                var cellMin = ImGui.GetCursorScreenPos();
                var cellMax = cellMin + new Vector2(100, ImGui.GetFrameHeight());
                
                // Display main twines, then alt twines in pink if there are any
                ImGui.Text(twinesNeeded.ToString());
                
                if (totalAltTwines > 0)
                {
                    ImGui.SameLine(0, 5);
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"+{totalAltTwines}");
                }
                
                // Show tooltip if cell is hovered
                if (ImGui.IsMouseHoveringRect(cellMin, cellMax))
                {
                    ImGui.BeginTooltip();
                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), $"Twines needed for {member.Name}:");
                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), $"  Main: {twinesNeeded}");
                    
                    for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
                    {
                        int altTwinesForThisSheet = sheetIdx - 1 < altTwinesBreakdown.Count ? altTwinesBreakdown[sheetIdx - 1] : 0;
                        ImGui.TextColored(new Vector4(1.0f, 0.5f, 1.0f, 1.0f), $"  Alt Job {sheetIdx}: {altTwinesForThisSheet}");
                    }
                    
                    ImGui.EndTooltip();
                }
            }

            ImGui.EndTable();
        }
    }
}