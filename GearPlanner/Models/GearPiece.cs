namespace GearPlanner.Models;

[System.Serializable]
public class GearPiece
{
    public GearSlot Slot { get; set; }
    public GearStatus CurrentStatus { get; set; } = GearStatus.None;
    public GearStatus DesiredStatus { get; set; } = GearStatus.BiS;
    public GearSource Source { get; set; } = GearSource.Savage;
    public GearSource DesiredSource { get; set; } = GearSource.None;
    public uint DesiredItemId { get; set; } = 0;
    public uint CurrentItemId { get; set; } = 0;
    
    public GearPiece() { }
    
    public GearPiece(GearSlot slot)
    {
        Slot = slot;
    }
}
