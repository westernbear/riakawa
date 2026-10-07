using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using Riakawa.Core;
using Terraria;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace Riakawa;

public sealed class Riakawa : Mod
{
    internal static ModKeybind? EmoteKey;
    [ThreadStatic] private static long packetEnd;

    public override void Load()
    {
        On_MessageBuffer.GetData += ReadPacket;
        if (!Main.dedServ)
            EmoteKey = KeybindLoader.RegisterKeybind(this, "Emote", "V");
    }

    public override void Unload()
    {
        On_MessageBuffer.GetData -= ReadPacket;
        EmoteKey = null;
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
        if (server && !message.IsClientRequestFrom(whoAmI))
            return;
        var player = Main.player[message.Player];
        if (!player.active) return;
        var state = player.GetModPlayer<RiakawaPlayer>();
        if (message.Kind == MessageKind.State) {
            state.Character = message.Character;
            if (server)
                state.SyncPlayer(-1, -1, false);
            else
                state.Emote.Synchronize(message.EmoteTicks);
        }
        else if (server) {
            if (message.Character != state.Character || !state.Emote.TryStart(state.Character, !player.dead))
                return;
            Send(new(MessageKind.Emote, message.Player, state.Character, state.Emote.Remaining));
        }
        else {
            state.Emote.Synchronize(message.EmoteTicks);
            state.PlayEmote();
        }
    }
}

// This tML version registers new mod keybinds as unassigned. Install the requested
// default once per input profile, preserving later remaps and deliberate unbinding.
public sealed class EmoteBinding : ModSystem
{
    private HashSet<string>? initialized;
    private bool failed;

    public override void PostUpdateInput()
    {
        if (Main.dedServ || failed) return;
        var profile = PlayerInput.CurrentProfile;
        if (profile == null || !profile.InputModes[InputMode.Keyboard].KeyStatus.TryGetValue("Riakawa/Emote", out var keys))
            return;
        string path = Path.Combine(Main.SavePath, "Riakawa-keybinds.json");
        try {
            initialized ??= File.Exists(path) ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path))
                ?? throw new JsonException("Empty keybind profile state") : [];
            if (!initialized.Add(profile.Name)) return;
            if (keys.Count == 0) keys.Add("V");
            PlayerInput.Save();
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(initialized));
            File.Move(path + ".tmp", path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            failed = true;
            Mod.Logger.Warn("Could not initialize emote binding; assign it in Controls.", ex);
        }
    }
}
