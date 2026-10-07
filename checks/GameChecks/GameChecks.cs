using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Riakawa;
using Riakawa.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GameChecks;

// Optional QA mod, never included in dist/Riakawa.tmod.
public sealed class GameChecks : Mod { }

public sealed class HurtTrace : ModPlayer
{
    private static readonly System.Collections.Generic.List<object> Events = [];
    public override void OnHurt(Player.HurtInfo info)
    {
        if (Main.dedServ || Snapshot.Output == null || Events.Count >= 500) return;
        Events.Add(new { frame = Main.GameUpdateCount, player = Player.whoAmI,
            character = Player.GetModPlayer<RiakawaPlayer>().Character.ToString(),
            info.SoundDisabled, info.Damage, info.Knockback, info.HitDirection,
            voices = RiakawaConfig.Current.Voices });
        File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".hurt.json"), JsonSerializer.Serialize(Events));
    }
}

// An isolated QA world can hold clear noon and suppress hostile spawns. This
// environment flag is never set by the release mod and never changes player stats.
public sealed class Stage : ModSystem
{
    internal static bool Enabled => Environment.GetEnvironmentVariable("RIAKAWA_QA_SAFE") == "1";
    internal static bool Night, Pumpkin;
    public override void OnWorldLoad()
    {
        if (!Enabled || Main.netMode == NetmodeID.MultiplayerClient) return;
        for (int x = Main.spawnTileX - 50; x <= Main.spawnTileX + 50; x++)
            for (int y = Main.spawnTileY - 20; y <= Main.spawnTileY + 30; y++)
                if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.Tombstones)
                    WorldGen.KillTile(x, y, noItem: true);
    }
    public override void PreUpdateWorld()
    {
        if (!Enabled || Main.netMode == NetmodeID.MultiplayerClient) return;
        Main.dayTime = !Night;
        Main.time = Night ? 16200 : 27000;
        Main.pumpkinMoon = Pumpkin;
        Main.raining = false;
        Main.rainTime = 0;
        Main.maxRaining = 0;
        foreach (var npc in Main.npc.Where(n => n.active && !n.friendly && !n.GetGlobalNPC<StageSpawns>().Target)) {
            npc.active = false;
            NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
        }
        if (Main.GameUpdateCount % 300 == 0) NetMessage.SendData(MessageID.WorldData);
    }
}

public sealed class StageSpawns : GlobalNPC
{
    public override bool InstancePerEntity => true;
    public bool Target;
    public override bool PreAI(NPC npc)
    {
        if (!Target) return true;
        npc.velocity = Vector2.Zero;
        npc.life = npc.lifeMax = 100000;
        npc.damage = npc.defense = 0;
        npc.noGravity = npc.noTileCollide = true;
        npc.timeLeft = 750;
        return false;
    }
    public override void SendExtraAI(NPC npc, BitWriter bits, BinaryWriter writer) => bits.WriteBit(Target);
    public override void ReceiveExtraAI(NPC npc, BitReader bits, BinaryReader reader) => Target = bits.ReadBit();
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (Stage.Enabled) { spawnRate = int.MaxValue; maxSpawns = 0; }
    }
}

public sealed class Snapshot : ModSystem
{
    internal static string? Output => Environment.GetEnvironmentVariable("RIAKAWA_QA_OUTPUT");
    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Output == null || Main.gameMenu || Main.GameUpdateCount % 30 != 0) return;
        FileCommands.Tick();
        PoseAudit.Tick();
        Write();
    }

    internal static void Write()
    {
        if (Output == null) throw new InvalidOperationException("Set RIAKAWA_QA_OUTPUT to run checks.");
        var p = Main.LocalPlayer;
        var report = new {
            utc = DateTime.UtcNow, frame = Main.GameUpdateCount, Main.netMode, Main.myPlayer, Main.drawingPlayerChat,
            configCharacter = RiakawaConfig.Current.Character.ToString(),
            settings = new { RiakawaConfig.Current.ReplaceWeapons, RiakawaConfig.Current.Decorations,
                RiakawaConfig.Current.ReducedEffects, RiakawaConfig.Current.SoundEffects, RiakawaConfig.Current.Voices },
            interfaceLanguage = RiakawaConfig.Current.InterfaceLanguage.ToString(),
            label = Language.GetTextValue("Mods.Riakawa.Configs.RiakawaConfig.Character.Label"),
            koreanGlyphs = FontAssets.MouseText.Value.AreCharactersSupported("캐릭터한국어"),
            emoteKeys = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus.TryGetValue("Riakawa/Emote", out var keys) ? keys : null,
            emoteKeyDown = PlayerInput.Triggers.Current.KeyStatus.TryGetValue("Riakawa/Emote", out bool down) && down,
            particles = new { AttackParticles.LastStyledCount,
                atlasRestored = TextureAssets.Dust.Value.Width == 1000 && TextureAssets.Dust.Value.Height == 120,
                nativeFramesIntact = Main.dust.Where(d => d.active && d.type < DustID.Count)
                    .All(d => TextureAssets.Dust.Value.Bounds.Contains(d.frame)) },
            players = Main.player.Where(x => x.active).Select(x => new {
                id = x.whoAmI, character = x.GetModPlayer<RiakawaPlayer>().Character.ToString(),
                emote = x.GetModPlayer<RiakawaPlayer>().Emote.Remaining,
                cooldown = x.GetModPlayer<RiakawaPlayer>().Emote.Cooldown, x.dead,
                position = new[] { x.position.X, x.position.Y }, x.direction, x.gravDir,
                velocity = new[] { x.velocity.X, x.velocity.Y },
                x.statLife, x.statMana, x.statDefense, x.itemAnimation, x.itemTime,
                x.invis, x.wet, mount = x.mount.Active ? x.mount.Type : -1, x.wings,
                heldItem = x.HeldItem.type, heldStack = x.HeldItem.stack,
                x.HeldItem.damage, x.HeldItem.useTime, x.HeldItem.useAnimation,
                x.itemAnimationMax, weaponDamage = x.GetWeaponDamage(x.HeldItem),
                ammunition = x.inventory.Select((item,slot) => new { slot, item.type, item.stack, item.ammo })
                    .Where(item => item.ammo > 0).ToArray()
            }).ToArray(),
            projectiles = Main.projectile.Where(x => x.active && x.type < ProjectileID.Count && x.owner < 255)
                .Select(x => new { x.identity, x.owner, x.type, weapon = x.GetGlobalProjectile<AttackOrigin>().WeaponId,
                    x.damage, x.width, x.height, position = new[] {x.position.X, x.position.Y} }).ToArray(),
            music = new { Main.curMusic, Main.musicVolume, Main.ambientVolume, Main.soundVolume,
                tracks = Main.audioSystem is Terraria.Audio.LegacyAudioSystem audio ? audio.AudioTracks
                    .Select((track, id) => new { id, type = track?.GetType().Name, fade = Main.musicFade[id] })
                    .Where(x => x.fade > 0).ToArray() : null }
        };
        // Preserve native non-finite values as named strings for diagnosis;
        // an exploratory projectile must not break the entire observation pass.
        File.WriteAllText(Output, JsonSerializer.Serialize(report, new JsonSerializerOptions {
            WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
    }
}

public sealed class CheckCommand : ModCommand
{
    public override string Command => "rq";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/rq status|character NAME|language NAME|item ID|split|export|hurt|death|loadout";
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Snapshot.Output == null || args.Length == 0) throw new UsageException(Usage);
        var player = caller.Player;
        switch (args[0]) {
            case "setting" when args.Length == 3:
                var field = typeof(RiakawaConfig).GetField(args[1], System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (field?.FieldType != typeof(bool) || !bool.TryParse(args[2], out bool enabled))
                    throw new UsageException("/rq setting BOOL_FIELD true|false");
                field.SetValue(RiakawaConfig.Current, enabled);
                break;
            case "music" when args.Length == 2 && float.TryParse(args[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float volume) && volume >= 0 && volume <= 1:
                Main.musicVolume = volume;
                break;
            case "render-report":
                AttackRenderAudit.Save();
                break;
            case "poses":
                PoseAudit.Run(args.Length >= 2 ? int.Parse(args[1]) : int.MaxValue, args.Length >= 3 ? int.Parse(args[2]) : 0);
                break;
            case "body" when args.Length==2:
                PoseAudit.CheckCurrentBody(args[1]);
                break;
            case "assets":
                RuntimeAssets.Check();
                caller.Reply("Packaged texture/audio load and all-item draw checks finished.");
                break;
            case "otherworld" when args.Length == 2 && bool.TryParse(args[1],out bool otherworld):
                Main.swapMusic = otherworld;
                break;
            case "musicbox" when args.Length == 2 && int.TryParse(args[1],out int musicBox) && musicBox >= 0 && musicBox < ItemID.Count:
                player.armor[3].SetDefaults(musicBox);
                NetMessage.SendData(MessageID.SyncEquipment,number: player.whoAmI,number2: 62);
                break;
            case "export-binding" when args.Length == 3 && int.TryParse(args[2], out int bindingId):
                var assetField = typeof(TextureAssets).GetField(args[1], System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
                var slot = assetField?.GetValue(null);
                if (assetField?.Name == "Item" && bindingId > 0 && bindingId < ItemID.Count) Main.instance.LoadItem(bindingId);
                if (assetField?.Name == "ItemFlame" && bindingId > 0 && bindingId < ItemID.Count) Main.instance.LoadItemFlames(bindingId);
                var bound = slot is ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>[] textures &&
                    bindingId >= 0 && bindingId < textures.Length ? textures[bindingId] :
                    bindingId == 0 ? slot as ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> : null;
                if (bound == null) throw new UsageException("Unknown texture slot/index.");
                var directory = Path.Combine(Path.GetDirectoryName(Snapshot.Output)!, "reference-textures");
                Directory.CreateDirectory(directory);
                using (var output = File.Create(Path.Combine(directory, $"{assetField!.Name}_{bindingId}.png")))
                    bound.Value.SaveAsPng(output, bound.Value.Width, bound.Value.Height);
                break;
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
                player.inventory[0].stack = player.inventory[0].maxStack;
                player.selectedItem = 0;
                break;
            case "ammo" when args.Length == 2 && int.TryParse(args[1], out int ammoId) &&
                    ammoId > 0 && ammoId < ItemID.Count && ContentSamples.ItemsByType[ammoId].ammo > 0:
                player.inventory[54].SetDefaults(ammoId);
                player.inventory[54].stack = player.inventory[54].maxStack;
                break;
            case "split":
                int parent = Projectile.NewProjectile(new EntitySource_ItemUse(player, player.HeldItem),
                    player.Center, Vector2.Zero, ProjectileID.WoodenArrowFriendly, 1, 0, player.whoAmI);
                int child = Projectile.NewProjectile(new EntitySource_Parent(Main.projectile[parent]),
                    player.Center, new Vector2(1, -1), ProjectileID.WoodenArrowFriendly, 1, 0, player.whoAmI);
                int weapon = Main.projectile[child].GetGlobalProjectile<AttackOrigin>().WeaponId;
                if (weapon != player.HeldItem.type) throw new Exception("Child projectile lost weapon provenance");
                caller.Reply($"PASS parent/child weapon {weapon}");
                break;
            case "export":
                var destination = Path.Combine(Path.GetDirectoryName(Snapshot.Output)!, "reference-textures");
                Directory.CreateDirectory(destination);
                var mod = ModLoader.GetMod("Riakawa");
                using (var manifest = JsonDocument.Parse(mod.GetFileBytes("Assets/manifest.json"))) {
                    foreach (int item in manifest.RootElement.GetProperty("weapons").EnumerateArray()
                        .Select(x => x.GetProperty("itemId").GetInt32()).Distinct()) {
                        Main.instance.LoadItem(item);
                        var texture = TextureAssets.Item[item].Value;
                        using var file = File.Create(Path.Combine(destination, $"Item_{item}.png"));
                        texture.SaveAsPng(file, texture.Width, texture.Height);
                    }
                }
                caller.Reply("Exported installed weapon art for anchor/reference inspection.");
                break;
            case "export-attacks":
                var attackDirectory = Path.Combine(Path.GetDirectoryName(Snapshot.Output)!, "reference-textures");
                Directory.CreateDirectory(attackDirectory);
                for (int type = 1; type < ProjectileID.Count; type++) {
                    Main.instance.LoadProjectile(type);
                    var texture = TextureAssets.Projectile[type].Value;
                    using var file = File.Create(Path.Combine(attackDirectory, $"Projectile_{type}.png"));
                    texture.SaveAsPng(file, texture.Width, texture.Height);
                }
                caller.Reply("Exported installed attack sheets for frame/binding inspection.");
                break;
            case "status": break;
            case "reset":
                player.mount.Dismount(player);
                for (int i=player.buffType.Length-1;i>=0;i--) player.DelBuff(i);
                player.gravDir=1;
                break;
            case "buff" when args.Length==2 && int.TryParse(args[1],out int buff) && buff>0 && buff<BuffID.Count:
                player.AddBuff(buff,1800);
                break;
            case "mount":
                if (player.mount.Active) player.mount.Dismount(player);
                else player.mount.SetMount(MountID.Slime,player);
                break;
            case "position" when args.Length==3 && float.TryParse(args[1],out float px) && float.TryParse(args[2],out float py) &&
                    px>160 && py>160 && px<Main.maxTilesX*16-160 && py<Main.maxTilesY*16-160:
                player.Teleport(new Vector2(px,py));
                if (Main.netMode==NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.TeleportEntity,number:0,number2:player.whoAmI,number3:px,number4:py);
                break;
            case "sound":
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, player.Center);
                break;
            case "loadout":
                // Native consumables/items in an isolated test player, no stat hooks.
                player.inventory[1].SetDefaults(ItemID.WoodenArrow);
                player.inventory[1].stack = 999;
                player.inventory[2].SetDefaults(ItemID.ManaPotion);
                player.inventory[2].stack = 999;
                player.statManaMax = 200;
                player.ConsumedManaCrystals = 9;
                player.statMana = 200;
                break;
            case "surface":
                var surface = new Vector2(Main.spawnTileX * 16 + 320, Main.spawnTileY * 16 - 160);
                player.Teleport(surface);
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.TeleportEntity,number: 0,number2: player.whoAmI,
                        number3: surface.X,number4: surface.Y);
                break;
            case "equipment":
                player.armor[0].SetDefaults(ItemID.IronHelmet);
                player.armor[1].SetDefaults(ItemID.IronChainmail);
                player.armor[2].SetDefaults(ItemID.IronGreaves);
                player.armor[3].SetDefaults(ItemID.AngelWings);
                player.miscEquips[3].SetDefaults(ItemID.SlimySaddle);
                player.inventory[3].SetDefaults(ItemID.GravitationPotion);
                player.inventory[3].stack = 10;
                player.inventory[4].SetDefaults(ItemID.InvisibilityPotion);
                player.inventory[4].stack = 10;
                break;
            case "summonequipment":
                player.armor[0].SetDefaults(ItemID.StardustHelmet);
                player.armor[1].SetDefaults(ItemID.StardustBreastplate);
                player.armor[2].SetDefaults(ItemID.StardustLeggings);
                player.armor[3].SetDefaults(ItemID.PapyrusScarab);
                player.armor[4].SetDefaults(ItemID.NecromanticScroll);
                player.armor[5].SetDefaults(ItemID.PygmyNecklace);
                break;
            case "hurt":
                player.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral("QA hurt cue")), 10, 1);
                break;
            case "mutedhurt":
                player.Hurt(new Player.HurtInfo { Damage = 10, SoundDisabled = true,
                    DamageSource = PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral("QA intentionally silent hurt")) });
                break;
            case "death":
                player.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral("QA death cue")), 1000, 1);
                break;
            case "makeplayer":
                Terraria.IO.PlayerFileData.CreateAndSave(new Player { name = "Riakawa QA Two", difficulty = 0 });
                caller.Reply("Created second local QA player.");
                break;
            default: throw new UsageException(Usage);
        }
        Snapshot.Write();
        caller.Reply("QA snapshot written.");
    }
}

public sealed class MusicScenarioCommand : ModCommand
{
    public override string Command => "rqmusic";
    public override CommandType Type => CommandType.World;
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (!Stage.Enabled || args.Length != 1 || args[0] is not ("day" or "night" or "event" or "boss" or "final"))
            throw new UsageException("Safe QA server only: /rqmusic day|night|event|boss|final");
        Stage.Night = args[0] is "night" or "event";
        Stage.Pumpkin = args[0] == "event";
        foreach (var npc in Main.npc.Where(n => n.active && n.GetGlobalNPC<StageSpawns>().Target)) {
            npc.active = false;
            NetMessage.SendData(MessageID.SyncNPC,number: npc.whoAmI);
        }
        if (args[0] is "boss" or "final") {
            int id = NPC.NewNPC(caller.Player.GetSource_Misc("RiakawaMusicQA"),
                (int)caller.Player.Center.X+250,(int)caller.Player.Center.Y,
                args[0] == "boss" ? NPCID.EyeofCthulhu : NPCID.MoonLordCore);
            Main.npc[id].GetGlobalNPC<StageSpawns>().Target = true;
            NetMessage.SendData(MessageID.SyncNPC,number: id);
        }
        Main.dayTime = !Stage.Night;
        Main.time = Stage.Night ? 16200 : 27000;
        Main.pumpkinMoon = Stage.Pumpkin;
        NetMessage.SendData(MessageID.WorldData);
        caller.Reply("Native music selection scenario: "+args[0]+" (boss AI is frozen for audio inspection).");
    }
}
