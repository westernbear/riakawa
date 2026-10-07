using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Riakawa;
using Riakawa.Core;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GameChecks;

// Optional QA mod. It is never packaged with Riakawa.
public sealed class GameChecks : Mod { }

public sealed class Stage : ModSystem
{
    public override void PreUpdateWorld()
    {
        if (Environment.GetEnvironmentVariable("RIAKAWA_QA_SAFE") != "1" || Main.netMode == NetmodeID.MultiplayerClient)
            return;
        Main.dayTime = true;
        Main.time = 27000;
        Main.raining = false;
        foreach (var npc in Main.npc.Where(n => n.active && !n.friendly)) npc.active = false;
    }
}

public sealed class Snapshot : ModSystem
{
    internal static string? Output => Environment.GetEnvironmentVariable("RIAKAWA_QA_OUTPUT");

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Output == null || Main.gameMenu || Main.GameUpdateCount % 30 != 0) return;
        FileCommands.Tick();
        Write();
    }

    internal static void Write()
    {
        if (Output == null) return;
        var report = new {
            utc = DateTime.UtcNow,
            frame = Main.GameUpdateCount,
            Main.netMode,
            Main.myPlayer,
            configCharacter = RiakawaConfig.Current.Character.ToString(),
            interfaceLanguage = RiakawaConfig.Current.InterfaceLanguage.ToString(),
            characterLabel = Language.GetTextValue($"Mods.Riakawa.Configs.Character.{RiakawaConfig.Current.Character}.Label"),
            koreanGlyphs = FontAssets.MouseText.Value.AreCharactersSupported("치이카와하치와레우사기모몽가도로롱"),
            players = Main.player.Where(p => p.active).Select(p => new {
                id = p.whoAmI,
                character = p.GetModPlayer<RiakawaPlayer>().Character.ToString(),
                p.dead,
                position = new[] { p.position.X, p.position.Y },
                velocity = new[] { p.velocity.X, p.velocity.Y },
                p.direction,
                p.gravDir,
                p.wet,
                mount = p.mount.Active,
                p.maxRunSpeed,
                p.accRunSpeed,
                fastRun = SkinMotion.FastRun(p.velocity.X, p.maxRunSpeed, p.accRunSpeed,
                    p.velocity.X < 0 ? p.controlLeft : p.controlRight, p.velocity.Y == 0, p.mount.Active),
                heldItem = p.HeldItem.type,
                p.statLife,
                p.statMana,
                p.statDefense
            }).ToArray()
        };
        File.WriteAllText(Output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class CheckCommand : ModCommand
{
    public override string Command => "rq";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/rq status|character NAME|language NAME|item ID|boots|cleararmor|teleport X Y|mount|speed X";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Snapshot.Output == null || args.Length == 0) throw new UsageException(Usage);
        var player = caller.Player;
        switch (args[0]) {
            case "character" when args.Length == 2 && Enum.TryParse<Character>(args[1], true, out var character) &&
                    Enum.IsDefined(character):
                RiakawaConfig.Current.Character = character;
                break;
            case "language" when args.Length == 2 && Enum.TryParse<InterfaceLanguage>(args[1], true, out var language) &&
                    Enum.IsDefined(language):
                RiakawaConfig.Current.InterfaceLanguage = language;
                RiakawaConfig.Current.OnChanged();
                break;
            case "item" when args.Length == 2 && int.TryParse(args[1], out int id) && id > 0 && id < ItemID.Count:
                player.inventory[0].SetDefaults(id);
                player.selectedItem = 0;
                break;
            case "boots":
                player.armor[3].SetDefaults(ItemID.HermesBoots);
                break;
            case "cleararmor":
                for (int i = 0; i < player.armor.Length; i++) player.armor[i].TurnToAir();
                break;
            case "teleport" when args.Length == 3 && float.TryParse(args[1], out float x) &&
                    float.TryParse(args[2], out float y) && x >= 0 && y >= 0 &&
                    x < Main.maxTilesX * 16 && y < Main.maxTilesY * 16:
                player.Teleport(new Vector2(x, y));
                break;
            case "mount":
                if (player.mount.Active) player.mount.Dismount(player);
                else player.mount.SetMount(MountID.Slime, player);
                break;
            case "speed" when args.Length == 2 && float.TryParse(args[1], out float speed) && Math.Abs(speed) <= 15:
                player.velocity = new Vector2(speed, 0);
                break;
            case "status": break;
            default: throw new UsageException(Usage);
        }
        Snapshot.Write();
        caller.Reply("PASS " + input);
    }
}
