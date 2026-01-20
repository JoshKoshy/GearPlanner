using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using GearPlanner.Windows;
using GearPlanner.Models;
using GearPlanner.Helpers;

namespace GearPlanner;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IGameInventory GameInventory { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;

    private const string CommandName = "/gp";

    public Configuration Configuration { get; init; }
    public BiSLibrary BiSLibrary { get; init; }

    public readonly WindowSystem WindowSystem = new("GearPlanner");
    private MainWindow MainWindow { get; init; }
    private SetupWindow SetupWindow { get; init; }

    public Plugin()
    {
        Plugin.Log.Information("[Plugin] Initializing GearPlanner plugin...");
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Plugin.Log.Information($"[Plugin] Configuration loaded. RaidTeams count: {Configuration.RaidTeams.Count}");
        
        if (Configuration.RaidTeams.Count > 0)
        {
            foreach (var team in Configuration.RaidTeams)
            {
                Plugin.Log.Debug($"[Plugin] Team: {team.Name}, Members: {team.Members.Count}, Sheets: {team.Sheets.Count}");
            }
        }
        
        // Initialize ItemDatabase from game data
        ItemDatabase.Initialize(DataManager);
        
        // Initialize BiS library
        var configPath = PluginInterface.GetPluginConfigDirectory();
        Plugin.Log.Information($"[Plugin] Config path: {configPath}");
        BiSLibrary = new BiSLibrary(configPath);
        BiSLibrary.LoadBiSSets();
        
        // Initialize ECommons
        ECommons.ECommonsMain.Init(PluginInterface, this);

        // Create sample team ONLY if no teams exist at all
        bool hasSampleTeam = Configuration.RaidTeams.Any(t => t.Name == "Sample Raid Team");
        
        // Log detailed team info right after loading
        Plugin.Log.Debug($"[Plugin] After loading configuration:");
        foreach (var team in Configuration.RaidTeams)
        {
            Plugin.Log.Debug($"[Plugin]   Team '{team.Name}': {team.Members.Count} members (via Members property)");
            for (int i = 0; i < team.Sheets.Count; i++)
            {
                Plugin.Log.Debug($"[Plugin]     Sheet {i} '{team.Sheets[i].Name}': {team.Sheets[i].Members.Count} members (direct access)");
            }
        }
        
        if (Configuration.RaidTeams.Count == 0 && !hasSampleTeam)
        {
            CreateSampleTeam();
        }
        else if (hasSampleTeam)
        {
            Plugin.Log.Information("[Plugin] Sample team already exists, skipping creation");
        }

        MainWindow = new MainWindow(this);
        SetupWindow = new SetupWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SetupWindow);

        // Show setup window on first launch (when no teams exist)
        if (Configuration.RaidTeams.Count == 0)
        {
            SetupWindow.IsOpen = true;
        }

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Usage: /gp [config|show|hide]. Manage raid gear planning."
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;



        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"===Gear Planner Plugin Loaded===");
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();


        MainWindow.Dispose();
        SetupWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var subcommand = args.Split(' ')[0];
        
        switch (subcommand.ToLower())
        {
            case "show":
                ToggleMainUi();
                break;
            case "hide":
                MainWindow.IsOpen = false;
                break;
            case "debug":
                ExamineWindowReader.DumpExaminationDiagnostics();
                break;
            default:
                ToggleMainUi();
                break;
        }
    }
    

    public void ToggleMainUi() => MainWindow.Toggle();

    private void CreateSampleTeam()
    {
        Plugin.Log.Information("[CreateSampleTeam] Creating new sample team...");
        var sampleTeam = new RaidTeam("Sample Raid Team");
        sampleTeam.Description = "Edit this team to get started! Change names, jobs, and track gear progression.";
        
        // Create 8 sample members: 2 tanks, 2 healers, 2 melee dps, 1 ranged dps, 1 caster dps
        var memberConfigs = new (string name, string job, JobRole role)[]
        {
            ("Tank 1", "Tank", JobRole.Tank),
            ("Tank 2", "Tank", JobRole.Tank),
            ("Healer 1", "Healer", JobRole.Healer),
            ("Healer 2", "Healer", JobRole.Healer),
            ("Melee 1", "Melee", JobRole.MeleeDPS),
            ("Melee 2", "Melee", JobRole.MeleeDPS),
            ("Ranged", "Ranged", JobRole.RangedDPS),
            ("Caster", "Caster", JobRole.MagicDPS)
        };

        foreach (var config in memberConfigs)
        {
            var member = new RaidMember(config.name, config.job, config.role);
            
            // Initialize all gear slots with desired source defaults
            member.InitializeGear();
            foreach (var gear in member.Gear.Values)
            {
                gear.DesiredStatus = GearStatus.BiS;
                // Start current status as low ilvl
                gear.CurrentStatus = GearStatus.LowIlvl;
                
                // Default current gear (Source) to None
                gear.Source = GearSource.None;
                
                // Set specific desired source defaults
                if (gear.Slot == GearSlot.MainHand)
                {
                    gear.DesiredSource = GearSource.Savage;
                }
                else if (gear.Slot == GearSlot.Ring1)
                {
                    gear.DesiredSource = GearSource.TomeUp;
                }
                else if (gear.Slot == GearSlot.Ring2)
                {
                    gear.DesiredSource = GearSource.Savage;
                }
                else
                {
                    gear.DesiredSource = GearSource.None;
                }
            }
            
            sampleTeam.AddMember(member);
        }

        Configuration.RaidTeams.Add(sampleTeam);
        Configuration.SelectedTeamIndex = 0;
        Configuration.Save();
        
        Log.Information("===Sample team created. Edit team members in the config window to get started!===");
    }
}
