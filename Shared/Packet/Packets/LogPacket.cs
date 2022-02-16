using System.Text;

namespace Shared.Packet.Packets; 

public struct LogPacket : IPacket {
    public string Text;
    public void Serialize(Span<byte> data) {
        
    }

    public void Deserialize(Span<byte> data) {
        Text = Encoding.UTF8.GetString(data[..]).TrimNullTerm();
    }
}