namespace Shared.Packet.Packets; 

[Flags]
public enum MovementFlags : byte {
    IsFlat,
    IsCapThrown,
    IsSeeker
}