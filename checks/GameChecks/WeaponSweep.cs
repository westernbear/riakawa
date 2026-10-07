using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Riakawa;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace GameChecks;

// Native-use exploration in the isolated test world. Infinite mana and a frozen,
// harmless enemy make this an attack-discovery run, NOT a balance/parity test.
public sealed class WeaponSweep : ModPlayer
{
    private int[] weapons = [];
    private int[] ammunition = [];
    private readonly HashSet<int> observed = [];
    private int index, ticks, duration;
    private bool summonSweep;
    private bool cycleCharacters = true, alternateUse;
    private Vector2 position;
    private readonly List<object> completed = [];
    public bool Running => weapons.Length > 0;
    public int Current => Running ? weapons[index] : 0;

    public void Start(int limit, bool summons = false, int[]? selected = null, bool original = false, bool alternate = false)
    {
        if (Main.npc.All(n => !n.active || !n.GetGlobalNPC<StageSpawns>().Target))
            throw new UsageException("Run /rqtarget on the safe QA server first.");
        using var manifest = JsonDocument.Parse(ModLoader.GetMod("Riakawa").GetFileBytes("Assets/manifest.json"));
        weapons = manifest.RootElement.GetProperty("weapons").EnumerateArray()
            .Select(w => w.GetProperty("itemId").GetInt32()).Distinct().Order()
            .Where(id => (!summons || ContentSamples.ItemsByType[id].DamageType == DamageClass.Summon) &&
                (selected == null || selected.Contains(id))).Take(limit).ToArray();
        if (weapons.Length == 0) throw new UsageException("No supported weapons selected.");
        summonSweep = summons;
        cycleCharacters = !original && Environment.GetEnvironmentVariable("RIAKAWA_QA_FIXED_CHARACTER") == null;
        alternateUse = alternate;
        if (original) RiakawaConfig.Current.Character = Riakawa.Core.Character.Original;
        ammunition = [];
        Begin();
    }

    public void StartAmmo()
    {
        if (Main.npc.All(n => !n.active || !n.GetGlobalNPC<StageSpawns>().Target))
            throw new UsageException("Run /rqtarget first.");
        string path = Environment.GetEnvironmentVariable("RIAKAWA_QA_CATALOG") ??
            throw new UsageException("Set RIAKAWA_QA_CATALOG to the pinned export.");
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(path));
        // One real firing case per distinct PickAmmo result, then inspect actual
        // shots/children: ItemCheck can override PickAmmo's provisional type.
        var pairs = catalog.RootElement.GetProperty("ammoProjectiles").EnumerateArray()
            .GroupBy(x => x.GetProperty("projectileId").GetInt32()).Select(g => g.First()).ToArray();
        weapons = pairs.Select(x => x.GetProperty("weaponId").GetInt32()).ToArray();
        ammunition = pairs.Select(x => x.GetProperty("ammoId").GetInt32()).ToArray();
        summonSweep = false;
        cycleCharacters = Environment.GetEnvironmentVariable("RIAKAWA_QA_FIXED_CHARACTER") == null;
        alternateUse = false;
        Begin();
    }

    private void Begin()
    {
        index = 0; completed.Clear(); position = Player.position;
        Player.mount.Dismount(Player);
        Player.downedDD2EventAnyDifficulty = true; // Isolated QA character: permit sentries outside the event.
        Main.playerInventory = false;
        Equip();
    }

    public void Stop()
    {
        weapons = [];
        Player.controlUseItem = false;
        Save();
    }

    private void Equip()
    {
        observed.Clear();
        var item = Player.inventory[0];
        item.SetDefaults(weapons[index]);
        item.stack = item.maxStack;
        Player.selectedItem = 0;
        Player.inventory[54].TurnToAir();
        if (item.useAmmo > 0) {
            var ammo = ContentSamples.ItemsByType.Values.Where(a => a.type < ItemID.Count && a.ammo == item.useAmmo)
                .OrderBy(a => a.type).First();
            Player.inventory[54].SetDefaults(ammunition.Length > 0 ? ammunition[index] : ammo.type);
            Player.inventory[54].stack = 9999;
        }
        Player.ConsumedManaCrystals = 9;
        Player.statMana = 200;
        duration = item.DamageType == Terraria.ModLoader.DamageClass.Summon ? 300 :
            item.channel ? 180 : item.shoot > ProjectileID.None ? 120 : 60;
        if (AttackRenderAudit.Enabled) duration = Math.Max(duration,item.channel ? 720 : 270);
        ticks = 0;
        NetMessage.SendData(MessageID.SyncEquipment, number: Player.whoAmI, number2: 0);
        NetMessage.SendData(MessageID.SyncEquipment, number: Player.whoAmI, number2: 54);
        Save();
    }

    public void Observe(Projectile projectile)
    {
        if (Running && projectile.GetGlobalProjectile<AttackOrigin>().WeaponId == Current)
            observed.Add(projectile.type);
    }

    public override void SetControls()
    {
        if (!Running || Player.whoAmI != Main.myPlayer || Main.drawingPlayerChat) return;
        var target = Main.npc.FirstOrDefault(n => n.active && n.GetGlobalNPC<StageSpawns>().Target);
        if (target == null) { Stop(); return; }
        var aim = summonSweep ? Player.Center + new Vector2(64,0) : target.Center;
        Main.mouseX = (int)(aim.X - Main.screenPosition.X);
        Main.mouseY = (int)(aim.Y - Main.screenPosition.Y);
        Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = Player.controlJump = false;
        Player.controlUseItem = ticks < duration - 15 &&
            (summonSweep ? ticks < 2 : Player.HeldItem.channel || ticks % 40 != 39);
        Player.controlUseTile = alternateUse && Player.controlUseItem;
        if (alternateUse) Player.controlUseItem = false;
        Player.MinionAttackTargetNPC = target.whoAmI;
    }

    public override void PostUpdate()
    {
        if (!Running || Player.whoAmI != Main.myPlayer || Main.drawingPlayerChat) return;
        if (AttackRenderAudit.Enabled) {
            if (cycleCharacters) RiakawaConfig.Current.Character = (Riakawa.Core.Character)(1+ticks/60%(Enum.GetValues<Riakawa.Core.Character>().Length-1));
            Player.immune = true; Player.immuneNoBlink = true; Player.immuneTime = 2; Player.statLife = Player.statLifeMax2;
        }
        Player.position = position;
        Player.velocity = Vector2.Zero;
        Player.statMana = Player.statManaMax2;
        if (++ticks < duration) return;
        foreach (var projectile in Main.projectile.Where(p => p.active && p.owner == Player.whoAmI).ToArray())
            projectile.Kill();
        completed.Add(new { itemId = Current, ammoId = ammunition.Length > 0 ? ammunition[index] : 0,
            ticks, finishedUtc = DateTime.UtcNow, observedProjectiles = observed.Order().ToArray() });
        // Remove any children created by cleanup so they cannot contaminate the
        // next ammo case. This harness is explicitly not a combat-parity test.
        foreach (var projectile in Main.projectile.Where(p => p.active && p.owner == Player.whoAmI)) {
            projectile.active = false;
            NetMessage.SendData(MessageID.KillProjectile,number: projectile.identity,number2: projectile.owner);
        }
        for (int i = Player.buffType.Length - 1; i >= 0; i--) Player.DelBuff(i);
        if (++index >= weapons.Length) {
            weapons = []; Player.controlUseItem = false;
            Main.NewText("QA weapon sweep finished; inspect the report and missing attack families.");
            Save();
        }
        else Equip();
    }

    private void Save()
    {
        if (Snapshot.Output == null) return;
        AttackRenderAudit.Save();
        File.WriteAllText(Path.ChangeExtension(Snapshot.Output, ".sweep.json"),
            JsonSerializer.Serialize(new { running = Running, current = Current, summonSweep, alternateUse, cycleCharacters, ammoSweep = ammunition.Length > 0, completed,
                scope = "standard-use discovery plus projectile cleanup; DD2 unlocked, frozen QA target/infinite mana; not combat parity or full coverage" },
                new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class AttackTrace : GlobalProjectile
{
    internal static readonly HashSet<(int Weapon, int Parent, int Child)> Seen = [];
    public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation) => projectile.type < ProjectileID.Count;
    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (Main.dedServ || Snapshot.Output == null) return;
        int weapon = projectile.GetGlobalProjectile<AttackOrigin>().WeaponId;
        int parent = source is EntitySource_Parent { Entity: Projectile p } ? p.type : 0;
        if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            Main.player[projectile.owner].GetModPlayer<WeaponSweep>().Observe(projectile);
        if (weapon == 0 || !Seen.Add((weapon, parent, projectile.type))) return;
        File.WriteAllText(Path.ChangeExtension(Snapshot.Output, ".attacks.json"),
            JsonSerializer.Serialize(Seen.OrderBy(x => x.Weapon).ThenBy(x => x.Parent).ThenBy(x => x.Child)
                .Select(x => new { weaponId = x.Weapon, parentProjectileId = x.Parent, projectileId = x.Child }),
                new JsonSerializerOptions { WriteIndented = true }));
    }
}

// Observe the production hook's existing scope; add no second item/projectile
// execution wrapper. Reflection is confined to this optional development mod.
public sealed class SoundTrace : ModSystem
{
    private static readonly FieldInfo ProjectileScope = typeof(VisualHooks)
        .GetField("soundProjectile", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo PlayerScope = typeof(VisualHooks)
        .GetField("soundPlayer", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly HashSet<(int Weapon, int Projectile, string Sound)> Seen = [];
    private static readonly HashSet<(int Weapon, string Requested, string Played, bool Enabled)> Playback = [];
    private static readonly List<object> ManaPayments = [];
    private static readonly List<object> Voices = [];
    public override void Load()
    {
        if (!Main.dedServ) {
            On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback += Observe;
            On_Player.CheckMana_Item_int_bool_bool += CheckMana;
        }
    }
    public override void Unload()
    {
        if (!Main.dedServ) {
            On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback -= Observe;
            On_Player.CheckMana_Item_int_bool_bool -= CheckMana;
        }
        Seen.Clear(); Playback.Clear(); ManaPayments.Clear(); Voices.Clear();
    }
    private static bool CheckMana(On_Player.orig_CheckMana_Item_int_bool_bool orig, Player player,
        Item item, int amount, bool pay, bool blockQuickMana)
    {
        int before = player.statMana;
        bool result = orig(player,item,amount,pay,blockQuickMana);
        if (Snapshot.Output != null && pay && result && item.mana > 0 && player.whoAmI == Main.myPlayer) {
            ManaPayments.Add(new { utc = DateTime.UtcNow, frame = Main.GameUpdateCount, itemId = item.type,
                character = player.GetModPlayer<RiakawaPlayer>().Character.ToString(), before, after = player.statMana });
            File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".mana.json"),JsonSerializer.Serialize(ManaPayments));
        }
        return result;
    }
    public override void PostSetupContent()
    {
        if (Main.dedServ || Snapshot.Output == null) return;
        var previousCounter = Main.projectile[Main.maxProjectiles - 1];
        try {
            foreach (var (context, counterType, bodyType, weapon) in new[] {
                ("StormTigerTierSwap",831,833,4607), ("StormTigerTierSwap",831,834,4607),
                ("StormTigerTierSwap",831,835,4607), ("AbigailTierSwap",970,963,5114) }) {
                var counter = new Projectile(); counter.SetDefaults(counterType);
                counter.active = true; counter.owner = 0;
                counter.GetGlobalProjectile<AttackOrigin>().WeaponId = (ushort)weapon;
                Main.projectile[Main.maxProjectiles - 1] = counter;
                foreach (var (owner, type, sourceContext, expected) in new[] {
                    (0,bodyType,context,weapon), (1,bodyType,context,0),
                    (0,1,context,0), (0,bodyType,"SetBonus_Stardust",0) }) {
                    var body = new Projectile(); body.SetDefaults(type); body.owner = owner;
                    var origin = body.GetGlobalProjectile<AttackOrigin>();
                    origin.OnSpawn(body,new EntitySource_Misc(sourceContext));
                    if (origin.WeaponId != expected)
                        throw new InvalidOperationException("Tiered summon provenance crossed owner/context/type boundaries.");
                }
            }
            Mod.Logger.Info("PASS: all tiered summon bodies inherit only their owner's matching counter source");
        }
        finally { Main.projectile[Main.maxProjectiles - 1] = previousCounter; }
        var shaderScope = typeof(VisualHooks).GetField("shaderTextures",BindingFlags.Static | BindingFlags.NonPublic)!;
        var savedScope = shaderScope.GetValue(null);
        try {
            var shader = (MiscShaderData)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MiscShaderData));
            var images = typeof(MiscShaderData).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.FieldType == typeof(ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>)).ToArray();
            if (images.Length != 3) throw new InvalidOperationException("Pinned native shader image slots changed.");
            foreach (var field in images) field.SetValue(shader,TextureAssets.MagicPixel);
            shaderScope.SetValue(null,new Dictionary<string,ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D>> {
                [TextureAssets.MagicPixel.Name] = TextureAssets.Dust });
            var apply = typeof(VisualHooks).GetMethod("ApplyShader",BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (bool fail in new[] { false,true }) {
                var original = new On_MiscShaderData.orig_Apply((s,data) => {
                    if (!ReferenceEquals(s,shader) || data != null || images.Any(f => !ReferenceEquals(f.GetValue(s),TextureAssets.Dust)))
                        throw new InvalidOperationException("Shader bindings or draw arguments changed.");
                    if (fail) throw new IOException("QA deliberate draw failure");
                });
                try { apply.Invoke(null,[original,shader,null]); }
                catch (TargetInvocationException ex) when (fail && ex.InnerException is IOException) { }
                if (images.Any(f => !ReferenceEquals(f.GetValue(shader),TextureAssets.MagicPixel)))
                    throw new InvalidOperationException("Shared shader images were not restored.");
            }
            Mod.Logger.Info("PASS: native trail shader bindings restore after normal and failed draws");
        }
        finally { shaderScope.SetValue(null,savedScope); }
        var replace = typeof(VisualHooks).GetMethod("ReplacementSound", BindingFlags.Static | BindingFlags.NonPublic)!;
        float previousPitch = Main.musicPitch;
        try {
            Main.musicPitch = 0.2f;
            var source = SoundID.Item26 with { Volume = 0.4f, Pitch = 0.15f, IsLooped = true,
                PauseBehavior = PauseBehavior.PauseWithGame, PlayOnlyIfFocused = true, Identifier = "QA/group" };
            var cached = source.GetSoundEffect();
            var result = (SoundStyle)replace.Invoke(null, [source, "Assets/Sounds/chiikawa-emote"])!;
            if (Math.Abs(result.Volume - 0.26f) > 0.001f || Math.Abs(result.Pitch - 0.35f) > 0.001f ||
                !result.IsLooped || !result.PlayOnlyIfFocused || result.PauseBehavior != source.PauseBehavior || result.Identifier != "Riakawa/QA/group" ||
                result.MaxInstances != source.MaxInstances || result.SoundLimitBehavior != source.SoundLimitBehavior ||
                ReferenceEquals(cached, result.GetSoundEffect()) || source.Volume != 0.4f)
                throw new InvalidOperationException("Sound replacement lost native playback settings or reused the native cache.");
            Mod.Logger.Info("PASS: sound replacement preserves loop/pause/pitch/limits, leaves source unchanged, uses new audio");
            var previousPlayer = PlayerScope.GetValue(null);
            var previousProjectile = ProjectileScope.GetValue(null);
            var marker = new Player();
            try {
                PlayerScope.SetValue(null, marker);
                var strike = typeof(VisualHooks).GetMethod("StrikeNpc", BindingFlags.Static | BindingFlags.NonPublic)!;
                int calls = 0;
                var original = new On_NPC.orig_StrikeNPC_HitInfo_bool_bool((npc, hit, fromNet, noInteraction) => {
                    calls++;
                    if (PlayerScope.GetValue(null) != null || ProjectileScope.GetValue(null) != null ||
                        hit.Damage != 17 || fromNet || noInteraction)
                        throw new InvalidOperationException("NPC effect scope or damage arguments changed.");
                    return 17;
                });
                int damage = (int)strike.Invoke(null, [original, new NPC(), new NPC.HitInfo { Damage = 17 }, false, false])!;
                if (damage != 17 || calls != 1 || !ReferenceEquals(PlayerScope.GetValue(null),marker))
                    throw new InvalidOperationException("NPC strike result/call count/outer scope changed.");
                Mod.Logger.Info("PASS: nested NPC effects clear attack ownership; native strike arguments/result preserved");
            }
            finally { PlayerScope.SetValue(null,previousPlayer); ProjectileScope.SetValue(null,previousProjectile); }
            int previousMode = Main.netMode;
            var previousRemote = Main.player[1];
            try {
                Main.netMode = NetmodeID.MultiplayerClient;
                Main.player[1] = new Player { active = true, whoAmI = 1 };
                var packet = typeof(VisualHooks).GetMethod("InstrumentPacket", BindingFlags.Static | BindingFlags.NonPublic)!;
                var buffer = new MessageBuffer();
                foreach (var (offset,length,kind,owner,expected) in new[] {
                    (0,6,58,1,true), (0,5,58,1,false), (-1,6,58,1,false),
                    (buffer.readBuffer.Length-2,6,58,1,false), (0,6,58,255,false), (0,6,60,1,false) }) {
                    buffer.readBuffer[0] = (byte)kind; buffer.readBuffer[1] = (byte)owner;
                    var original = new On_MessageBuffer.orig_GetData((MessageBuffer b,int start,int size,out int messageType) => {
                        if (ReferenceEquals(PlayerScope.GetValue(null),Main.player[1]) != expected || start != offset || size != length)
                            throw new InvalidOperationException("Instrument packet scope/bounds changed.");
                        messageType = kind;
                    });
                    object?[] arguments = [original,buffer,offset,length,null];
                    packet.Invoke(null,arguments);
                    if ((int)arguments[4]! != kind || !ReferenceEquals(PlayerScope.GetValue(null),previousPlayer))
                        throw new InvalidOperationException("Instrument packet result/scope was not restored.");
                }
                Mod.Logger.Info("PASS: instrument packet scope validates bounds/type/owner and preserves original parsing");
            }
            finally { Main.netMode = previousMode; Main.player[1] = previousRemote; }
        }
        finally { Main.musicPitch = previousPitch; }
    }
    private static SlotId Observe(On_SoundEngine.orig_PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback orig,
        ref SoundStyle style, Vector2? position, SoundUpdateCallback callback)
    {
        int weapon = 0;
        if (Snapshot.Output != null) {
            var projectile = ProjectileScope.GetValue(null) as Projectile;
            var player = PlayerScope.GetValue(null) as Player;
            weapon = projectile != null && projectile.type < ProjectileID.Count ? projectile.GetGlobalProjectile<AttackOrigin>().WeaponId :
                player?.HeldItem.type ?? 0;
            string key = style.SoundPath + (style.Variants.IsEmpty ? "" : ":" + string.Join(",", style.Variants.ToArray()));
            if (weapon > 0 && Seen.Add((weapon, projectile?.type ?? 0, key)))
                File.WriteAllText(Path.ChangeExtension(Snapshot.Output, ".sounds.json"),
                    JsonSerializer.Serialize(Seen.OrderBy(x => x.Weapon).ThenBy(x => x.Projectile).ThenBy(x => x.Sound)
                        .Select(x => new { weaponId = x.Weapon, projectileId = x.Projectile, sound = x.Sound }),
                        new JsonSerializerOptions { WriteIndented = true }));
        }
        var result = orig(ref style, position, callback);
        if (Snapshot.Output != null && Voices.Count < 500 &&
            (style.SoundPath.StartsWith("Riakawa/Assets/Sounds/") &&
                (style.SoundPath.EndsWith("-hurt") || style.SoundPath.EndsWith("-death") || style.SoundPath.EndsWith("-emote")) ||
             style.SoundPath.Contains("Player_Hit") || style.SoundPath.Contains("Player_Killed") ||
             style.SoundPath.Contains("Female_Hit"))) {
            bool activeVoice = SoundEngine.TryGetActiveSound(result, out var voice);
            Voices.Add(new { frame = Main.GameUpdateCount, utc = DateTime.UtcNow, requested = style.SoundPath,
                played = activeVoice ? voice!.Style.SoundPath : null, voices = RiakawaConfig.Current.Voices,
                position = position is { } point ? new[] { point.X, point.Y } : null });
            File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".voices.json"),JsonSerializer.Serialize(Voices));
        }
        if (Snapshot.Output != null && SoundEngine.TryGetActiveSound(result, out var active) &&
            Playback.Add((weapon, style.SoundPath, active.Style.SoundPath, RiakawaConfig.Current.SoundEffects)))
            File.WriteAllText(Path.ChangeExtension(Snapshot.Output, ".playback.json"),
                JsonSerializer.Serialize(Playback.Select(p => new { weaponId = p.Weapon, requested = p.Requested,
                    played = p.Played, enabled = p.Enabled }), new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }
}

public sealed class TargetCommand : ModCommand
{
    public override string Command => "rqtarget";
    public override CommandType Type => CommandType.World;
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (!Stage.Enabled) throw new UsageException("Target creation requires the isolated safe QA server.");
        foreach (var old in Main.npc.Where(n => n.TryGetGlobalNPC<StageSpawns>(out var stage) && stage.Target)) {
            old.active = false;
            NetMessage.SendData(MessageID.SyncNPC, number: old.whoAmI);
        }
        int index = NPC.NewNPC(caller.Player.GetSource_Misc("RiakawaQA"),
            (int)caller.Player.Center.X + (args.Length == 1 && args[0] == "far" ? 320 : 42),
            (int)caller.Player.Bottom.Y, NPCID.Zombie);
        if (index >= Main.maxNPCs) throw new UsageException("No NPC slot available.");
        var npc = Main.npc[index];
        npc.GetGlobalNPC<StageSpawns>().Target = true;
        npc.life = npc.lifeMax = 100000;
        npc.damage = npc.defense = 0;
        npc.knockBackResist = 0;
        npc.noGravity = npc.noTileCollide = true;
        npc.netUpdate = true;
        NetMessage.SendData(MessageID.SyncNPC, number: index);
        caller.Reply("Harmless stationary QA target created.");
    }
}

public sealed class SweepCommand : ModCommand
{
    public override string Command => "rqsweep";
    public override CommandType Type => CommandType.Chat;
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Snapshot.Output == null) throw new UsageException("QA output path is not set.");
        var sweep = caller.Player.GetModPlayer<WeaponSweep>();
        if (args.Length == 1 && args[0] == "stop") { sweep.Stop(); return; }
        if (args.Length == 1 && args[0] == "ammo") { sweep.StartAmmo(); return; }
        if (args.Length == 1 && args[0] == "minions") { sweep.Start(int.MaxValue, true); return; }
        if (args.Length == 2 && args[0] is "items" or "original" or "alternate") {
            var ids = args[1].Split(',').Select(int.Parse).ToArray();
            sweep.Start(int.MaxValue, selected: ids, original: args[0]=="original", alternate: args[0]=="alternate");
            return;
        }
        int limit = int.MaxValue;
        if (args.Length > 0 && (!int.TryParse(args[0], out limit) || limit <= 0))
            throw new UsageException("/rqsweep [positive count|stop|minions]");
        sweep.Start(limit);
        caller.Reply("Native weapon discovery running. /rqsweep stop cancels it.");
    }
}

public sealed class PoolCommand : ModCommand
{
    public override string Command => "rqpool";
    public override CommandType Type => CommandType.World;
    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (!Stage.Enabled) throw new UsageException("Pool creation requires the isolated safe QA server.");
        int left = (int)caller.Player.Center.X / 16 - 5, top = (int)caller.Player.Bottom.Y / 16;
        if (args.Length == 1 && args[0] == "drain") {
            if (!WorldGen.InWorld(left-2,top-12,1) || !WorldGen.InWorld(left+12,top+11,1))
                throw new UsageException("Too close to world boundary.");
            for (int x = left-2; x <= left+12; x++)
                for (int y = top-12; y <= top+11; y++) Main.tile[x,y].LiquidAmount = 0;
            NetMessage.SendTileSquare(-1,left-2,top-12,15,24);
            caller.Reply("QA pool drained."); return;
        }
        if (!WorldGen.InWorld(left,top,10) || !WorldGen.InWorld(left+10,top+7,10))
            throw new UsageException("Too close to world boundary.");
        for (int x = left; x <= left+10; x++)
            for (int y = top; y <= top+7; y++) {
                var tile = Main.tile[x,y];
                tile.ClearEverything();
                if (x == left || x == left+10 || y == top+7) {
                    tile.HasTile = true; tile.TileType = TileID.Stone;
                }
                else { tile.LiquidType = LiquidID.Water; tile.LiquidAmount = 255; }
                WorldGen.SquareTileFrame(x,y);
            }
        NetMessage.SendTileSquare(-1,left,top,11,8);
        caller.Reply("Small native-water pool created below the isolated QA player.");
    }
}
