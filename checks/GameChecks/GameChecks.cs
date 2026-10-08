using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Terraria.GameInput;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Riakawa;
using Riakawa.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.UI;
using Terraria.ModLoader;

namespace GameChecks;

// Optional QA mod. It is never packaged with Riakawa.
public sealed class GameChecks : Mod { }

public sealed class Stage : ModSystem
{
    internal static bool Boss; // /rqboss keeps one boss alive for the music check
    public override void PreUpdateWorld()
    {
        if (Environment.GetEnvironmentVariable("RIAKAWA_QA_SAFE") != "1" || Main.netMode == NetmodeID.MultiplayerClient)
            return;
        Main.dayTime = true;
        Main.time = 27000;
        Main.raining = false;
        foreach (var npc in Main.npc.Where(n => n.active && !n.friendly && !(Boss && n.boss))) {
            npc.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
        }
    }
}

// Main.hideUI corrupts the software-rendered frame here; switching interface layers off keeps captures clean.
public sealed class CleanScreen : ModSystem
{
    internal static string? Match; // null: all layers drawn; otherwise hide layers whose name contains it
    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (Match != null) foreach (var layer in layers.Where(l => l.Name.Contains(Match))) layer.Active = false;
    }
}

// Sprite QA only: keep the corpse opaque and free of blood and tombstones so every death frame can be read.
public sealed class CleanDeath : ModPlayer
{
    internal static bool On;
    private static readonly int[] Tombstones = [ProjectileID.Tombstone, ProjectileID.GraveMarker, ProjectileID.CrossGraveMarker,
        ProjectileID.Headstone, ProjectileID.Gravestone, ProjectileID.Obelisk, ProjectileID.RichGravestone1,
        ProjectileID.RichGravestone2, ProjectileID.RichGravestone3, ProjectileID.RichGravestone4, ProjectileID.RichGravestone5];
    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust,
        ref PlayerDeathReason damageSource)
    {
        if (On) genDust = false;
        return true;
    }
    public override void UpdateDead()
    {
        if (!On || Player.whoAmI != Main.myPlayer) return;
        Player.immuneAlpha = 0;
        foreach (var projectile in Main.projectile.Where(p => p.active && p.owner == Player.whoAmI && Tombstones.Contains(p.type)))
            projectile.active = false;
    }
}

// The virtual display only delivers the first pointer motion; QA aims through a fixed in-game cursor instead.
public sealed class QaCursor : ModSystem
{
    internal static Point? Target;
    public override void PostUpdateInput()
    {
        if (Target is not { } p) return;
        Main.LocalPlayer.statMana = Main.LocalPlayer.statManaMax2; // keep magic guns firing through the sweep
        Main.mouseX = PlayerInput.MouseX = p.X;
        Main.mouseY = PlayerInput.MouseY = p.Y;
        foreach (var (name, value) in new[] { ("_originalMouseX", p.X), ("_originalMouseY", p.Y) })
            typeof(PlayerInput).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, value);
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
            music = Main.curMusic,
            mouse = new[] { Main.mouseX, Main.mouseY },
            jukeboxMusic = MusicLoader.GetMusicSlot("Riakawa/Assets/Music/SirenIsland"),
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
                p.itemRotation,
                p.itemAnimation,
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
    public override string Usage => "/rq status|character NAME|language NAME|item ID|item Mod/Name|boots|cleararmor|teleport X Y|mount|speed X|hurt|kill|hideui [off|LAYER]|cleandeath [off]|buff ID|cursor X Y|cursor off|zoom X";

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
            case "item" when args.Length == 2 && (int.TryParse(args[1], out int id) ? id > 0 && id < ItemID.Count :
                    ModContent.TryFind<ModItem>(args[1], out var modItem) && (id = modItem.Type) > 0):
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
            case "hurt":
                player.immune = false;
                player.immuneTime = 0;
                player.statLife = player.statLifeMax2;
                player.Hurt(PlayerDeathReason.LegacyDefault(), 1, -player.direction, dodgeable: false);
                break;
            case "kill":
                player.KillMe(PlayerDeathReason.LegacyDefault(), 9999, 0);
                break;
            case "hideui":
                CleanScreen.Match = args.Length < 2 ? "" : args[1] == "off" ? null : args[1];
                break;
            case "zoom" when args.Length == 2 && float.TryParse(args[1], out float zoom) && zoom >= 1 && zoom <= 2:
                Main.GameZoomTarget = zoom;
                break;
            case "cleandeath":
                CleanDeath.On = args.Length < 2 || args[1] != "off";
                break;
            case "buff" when args.Length == 2 && int.TryParse(args[1], out int buff) && buff > 0 && buff < BuffID.Count:
                player.AddBuff(buff, 36000);
                break;
            case "cursor" when args.Length == 3 && int.TryParse(args[1], out int cx) && int.TryParse(args[2], out int cy):
                QaCursor.Target = new Point(cx, cy);
                break;
            case "cursor" when args.Length == 2 && args[1] == "off":
                QaCursor.Target = null;
                break;
            case "status": break;
            default: throw new UsageException(Usage);
        }
        Snapshot.Write();
        caller.Reply("PASS " + input);
    }
}

// Safe QA server only: spawn (on) or clear (off) a boss next to the caller to check the boss theme.
public sealed class BossScenarioCommand : ModCommand
{
    public override string Command => "rqboss";
    public override CommandType Type => CommandType.World;
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Environment.GetEnvironmentVariable("RIAKAWA_QA_SAFE") != "1" || args.Length != 1 || args[0] is not ("on" or "off"))
            throw new UsageException("Safe QA server only: /rqboss on|off");
        Stage.Boss = args[0] == "on";
        foreach (var npc in Main.npc.Where(n => n.active && n.boss)) {
            npc.active = false;
            NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
        }
        if (Stage.Boss) {
            int id = NPC.NewNPC(caller.Player.GetSource_Misc("RiakawaQA"), (int)caller.Player.Center.X + 300,
                (int)caller.Player.Center.Y - 200, NPCID.KingSlime);
            NetMessage.SendData(MessageID.SyncNPC, number: id);
        }
        caller.Reply("PASS /rqboss " + args[0]);
    }
}
