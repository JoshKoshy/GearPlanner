namespace TeamGearPlanning.Models;

[System.Serializable]
public enum GearStatus
{
    None = 0,              // No gear
    LowIlvl = 1,           // Red - low item level
    CraftedGear = 2,       // White - crafted gear
    AllianceRaid = 3,      // Light blue - alliance raid gear
    SavageRaid = 4,        // Blue - savage raid gear
    BiS = 5                // Purple - Best in Slot gear
}
