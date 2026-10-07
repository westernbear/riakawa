using System;
using Riakawa.Core;

if (args.Length == 4 && args[0] == "--textures") {
    XnbSizes.Export(args[1], args[2], args[3]);
    return;
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var request = new CosmeticMessage(MessageKind.Emote, 4, Character.Chiikawa, 0);
Check(CosmeticMessage.TryDecode(request.Encode(), out var decoded) && decoded == request, "Protocol roundtrip");
Check(decoded.IsClientRequestFrom(4) && !decoded.IsClientRequestFrom(5), "Reject spoofed player");
Check(!CosmeticMessage.TryDecode([1, 1, 4], out _), "Reject truncated packet");
Check(!CosmeticMessage.TryDecode([1, 1, 4, 1, 0, 0, 0], out _), "Reject extra packet bytes");
Check(!CosmeticMessage.TryDecode([2, 1, 4, 1, 0, 0], out _), "Reject version mismatch");
Check(!CosmeticMessage.TryDecode([1, 9, 4, 1, 0, 0], out _), "Reject unknown kind");
Check(!CosmeticMessage.TryDecode([1, 1, 255, 1, 0, 0], out _), "Reject invalid player");
Check(!CosmeticMessage.TryDecode([1, 1, 4, 4, 0, 0], out _), "Reject invalid character");
Check(!CosmeticMessage.TryDecode([1, 1, 4, 1, 91, 0], out _), "Reject oversized emote");
Check(!(request with { EmoteTicks = 90 }).IsClientRequestFrom(4), "Server owns emote duration");
var clock = new EmoteClock();
Check(!clock.TryStart(Character.Original, true), "Original mode has no emote");
Check(!clock.TryStart(Character.Usagi, false), "Dead player has no emote");
Check(clock.TryStart(Character.Usagi, true), "Start emote");
Check(!clock.TryStart(Character.Usagi, true), "Reject spam");
for (int tick = 0; tick < CosmeticMessage.EmoteDuration; tick++) clock.Tick();
Check(clock.Remaining == 0 && !clock.TryStart(Character.Usagi, true), "Cooldown outlasts animation");
for (int tick = CosmeticMessage.EmoteDuration; tick < CosmeticMessage.EmoteCooldown; tick++) clock.Tick();
Check(clock.TryStart(Character.Hachiware, true), "Cooldown expires");
clock.Synchronize(ushort.MaxValue);
Check(clock.Remaining == CosmeticMessage.EmoteDuration, "Clamp remote state");
Console.WriteLine("PASS: protocol bounds, sender identity, emote duration and rate limit");
