using System;

namespace Riakawa.Core;

public enum Character : byte { Original, Chiikawa, Hachiware, Usagi, Momonga, Doro }

// A single bounded appearance message covers join, reconnect and config changes.
public readonly record struct CosmeticMessage(byte Player, Character Character)
{
    public const byte Version = 2;
    public const int Size = 3;

    public byte[] Encode() => [Version, Player, (byte)Character];

    public static bool TryDecode(ReadOnlySpan<byte> data, out CosmeticMessage message)
    {
        message = default;
        if (data.Length != Size || data[0] != Version || data[1] == 255 ||
            data[2] > (byte)Character.Doro) return false;
        message = new(data[1], (Character)data[2]);
        return true;
    }

    public bool IsClientRequestFrom(int sender) => sender >= 0 && sender < 255 && Player == sender;
}

public static class SkinMotion
{
    // Return through the middle poses instead of jumping from the last to the first.
    public static int DoroWalkFrame(ulong tick, int ticksPerFrame)
    {
        int phase = (int)(tick / (uint)Math.Max(1, ticksPerFrame) % 6);
        return phase < 4 ? phase : 6 - phase;
    }

    // Matches the midpoint used by Terraria's Hermes-style run sound and dust.
    public static bool FastRun(float velocityX, float normalSpeed, float sprintSpeed,
        bool movingWithInput, bool grounded, bool mounted) =>
        sprintSpeed > normalSpeed && movingWithInput && grounded && !mounted &&
        MathF.Abs(velocityX) > (normalSpeed + sprintSpeed) / 2f;
}
