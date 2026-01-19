using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace GearPlanner.Windows;

public class SetupWindow : Window, IDisposable
{
    private readonly Plugin plugin;

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
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Commands:");
        ImGui.Text("/gp config - Open team configuration");
        ImGui.Text("/gp show - Show main gear tracking window");
        ImGui.Text("/gp hide - Hide main gear tracking window");

        ImGui.Spacing();
        
        if (ImGui.Button("Close##SetupClose", new Vector2(100, 0)))
        {
            IsOpen = false;
        }
    }
}
