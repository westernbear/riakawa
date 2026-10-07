using System;

namespace Riakawa.Core;

public enum Character : byte { Original, Chiikawa, Hachiware, Usagi }
public enum MessageKind : byte { State, Emote }

// Shared by the mod and the dependency-free protocol check.
public readonly record struct CosmeticMessage(MessageKind Kind, byte Player, Character Character, ushort EmoteTicks)
{
    public const byte Version = 1;
    public const int Size = 6;
    public const ushort EmoteDuration = 90;
    public const int EmoteCooldown = 180;

    public byte[] Encode() => [Version, (byte)Kind, Player, (byte)Character,
        (byte)(EmoteTicks & 255), (byte)(EmoteTicks >> 8)];

    public static bool TryDecode(ReadOnlySpan<byte> data, out CosmeticMessage message)
    {
        message = default;
        if (data.Length != Size || data[0] != Version || data[1] > (byte)MessageKind.Emote ||
            data[2] == 255 || data[3] > (byte)Character.Usagi)
            return false;
        ushort ticks = (ushort)(data[4] | (data[5] << 8));
        if (ticks > EmoteDuration)
            return false;
        message = new((MessageKind)data[1], data[2], (Character)data[3], ticks);
        return true;
    }

    public bool IsClientRequestFrom(int sender) => sender >= 0 && sender < 255 && Player == sender &&
        EmoteTicks == 0 && (Kind != MessageKind.Emote || Character != Character.Original);
}

public sealed class EmoteClock
{
    public ushort Remaining { get; private set; }
    public int Cooldown { get; private set; }

    public bool TryStart(Character character, bool alive)
    {
        if (!alive || character == Character.Original || Cooldown > 0)
            return false;
        Remaining = CosmeticMessage.EmoteDuration;
        Cooldown = CosmeticMessage.EmoteCooldown;
        return true;
    }

    public void Synchronize(ushort ticks) => Remaining = Math.Min(ticks, CosmeticMessage.EmoteDuration);
    public void Tick()
    {
        if (Remaining > 0) Remaining--;
        if (Cooldown > 0) Cooldown--;
    }
}
