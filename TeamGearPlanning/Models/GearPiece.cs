namespace TeamGearPlanning.Models;

[System.Serializable]
public class GearPiece
{
    public GearSlot Slot { get; set; }
    public GearStatus CurrentStatus { get; set; } = GearStatus.None;
    public GearStatus DesiredStatus { get; set; } = GearStatus.BiS;
    
    public GearPiece() { }
    
    public GearPiece(GearSlot slot)
    {
        Slot = slot;
    }
}
