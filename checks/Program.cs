using System;
using Riakawa.Core;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

foreach (var character in Enum.GetValues<Character>()) {
    var state = new CosmeticMessage(4, character);
    Check(CosmeticMessage.TryDecode(state.Encode(), out var copy) && copy == state,
        $"Roundtrip {character}");
    Check(state.IsClientRequestFrom(4) && !state.IsClientRequestFrom(5), "Reject spoofed player");
}
Check(!CosmeticMessage.TryDecode([2, 4], out _), "Reject truncated packet");
Check(!CosmeticMessage.TryDecode([2, 4, 1, 0], out _), "Reject extra packet bytes");
Check(!CosmeticMessage.TryDecode([1, 4, 1], out _), "Reject old protocol");
Check(!CosmeticMessage.TryDecode([2, 255, 1], out _), "Reject invalid player");
Check(!CosmeticMessage.TryDecode([2, 4, 6], out _), "Reject invalid character");
Check((byte)Character.Usagi == 3 && (byte)Character.Momonga == 4 && (byte)Character.Doro == 5,
    "Existing character IDs remain stable");
Check(!SkinMotion.FastRun(4.5f, 3, 6, true, true, false), "Sound threshold is strict");
Check(SkinMotion.FastRun(4.6f, 3, 6, true, true, false), "Hermes run switches pose");
Check(!SkinMotion.FastRun(6, 3, 3, true, true, false), "Normal running stays upright");
Check(!SkinMotion.FastRun(6, 3, 6, false, true, false), "Sliding stays upright");
Check(!SkinMotion.FastRun(6, 3, 6, true, false, false), "Airborne stays upright");
Check(!SkinMotion.FastRun(6, 3, 6, true, true, true), "Mount stays upright");
Console.WriteLine("PASS: appearance protocol and Hermes-speed pose switch");
