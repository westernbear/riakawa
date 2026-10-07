using System;
using System.IO;
using Riakawa.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Riakawa;

public sealed class Riakawa : Mod
{
    [ThreadStatic] private static long packetEnd;

    public override void Load()
    {
        On_MessageBuffer.GetData += ReadPacket;
    }

    public override void Unload()
    {
        On_MessageBuffer.GetData -= ReadPacket;
    }

    private static void ReadPacket(On_MessageBuffer.orig_GetData orig, MessageBuffer buffer,
        int start, int length, out int messageType)
    {
        long previous = packetEnd;
        packetEnd = (long)start + length;
        try { orig(buffer, start, length, out messageType); }
        finally { packetEnd = previous; }
    }

    internal void Send(CosmeticMessage message, int to = -1, int ignore = -1)
    {
        var packet = GetPacket(CosmeticMessage.Size);
        packet.Write(message.Encode());
        packet.Send(to, ignore);
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        // tML's reader wraps a reusable buffer, not a stream bounded to this packet.
        if (packetEnd - reader.BaseStream.Position != CosmeticMessage.Size ||
            !CosmeticMessage.TryDecode(reader.ReadBytes(CosmeticMessage.Size), out var message))
            return;
        bool server = Main.netMode == NetmodeID.Server;
        if (server && !message.IsClientRequestFrom(whoAmI)) return;
        var player = Main.player[message.Player];
        if (!player.active) return;
        var state = player.GetModPlayer<RiakawaPlayer>();
        state.Character = message.Character;
        if (server) state.SyncPlayer(-1, whoAmI, false);
    }
}
