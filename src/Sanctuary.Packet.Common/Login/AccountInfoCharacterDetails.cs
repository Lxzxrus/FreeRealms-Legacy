using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public class AccountInfoCharacterDetails
{
    public string? HeadshotUrl;

    public AccountInfoCharacterData Data = new();

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        writer.Write(HeadshotUrl);

        Data.Serialize(writer);

        return writer.Buffer;
    }
}