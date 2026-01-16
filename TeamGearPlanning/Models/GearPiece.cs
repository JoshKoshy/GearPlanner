namespace TeamGearPlanning.Models;

[System.Serializable]
public class GearPiece
{
    public GearSlot Slot { get; set; }
    public GearStatus CurrentStatus { get; set; } = GearStatus.None;
    public GearStatus DesiredStatus { get; set; } = GearStatus.BiS;
    public GearSource Source { get; set; } = GearSource.Savage;
    public GearSource DesiredSource { get; set; } = GearSource.None;
    
    public GearPiece() { }
    
    public GearPiece(GearSlot slot)
    {
        Slot = slot;
        // Default OffHand to None/blank
        if (slot == GearSlot.OffHand)
        {
            Source = GearSource.None;
        }
    }
}
