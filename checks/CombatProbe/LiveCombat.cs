using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CombatProbe;

// Opt-in replay in a disposable single-player world. Uses native updates, AI,
// damage and resource payments. No infinite mana, invulnerability or frozen NPCs.
public sealed class LiveCombat : ModSystem
{
    internal static string? Output => Environment.GetEnvironmentVariable("RIAKAWA_LIVE_COMBAT");
    internal static bool Running;
    internal static int Tick;
    internal static readonly List<object> Hits = [];
    private static readonly JsonSerializerOptions Json = new() {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private static readonly FieldInfo Counter = typeof(Main).GetField("_gameUpdateCount", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static int[] weapons = [];
    private static int index;
    private static bool opened;
    private static StreamWriter? trace;
    private static readonly List<object> completed = [];
    private static readonly HashSet<int> spawned = [];
    private static int totalHits, ammo;
    private static int damageRoll;
    private static readonly List<object> Rolls = [];
    private const int Duration = 360;
    private const int UpdateBatchSize = 256;
    private static Vector2 Start => new(Main.spawnTileX * 16, (Main.spawnTileY - 4) * 16 - 42);

    public override void Load()
    {
        if (Output != null && !Main.dedServ) {
            On_Main.DoUpdateInWorld += Update;
            On_Main.DamageVar_float_int_float += Damage;
            On_Player.ItemCheck += ItemCheck;
            On_Projectile.Update += ProjectileUpdate;
        }
    }
    public override void Unload() {
        On_Main.DoUpdateInWorld -= Update; On_Main.DamageVar_float_int_float -= Damage;
        On_Player.ItemCheck -= ItemCheck; On_Projectile.Update -= ProjectileUpdate;
        trace?.Dispose(); trace = null; Running = false;
    }
    private static void ItemCheck(On_Player.orig_ItemCheck orig, Player player)
    {
        if (Running) Main.rand = new UnifiedRandom(81000 + weapons[index] * 400 + Tick);
        orig(player);
    }
    private static void ProjectileUpdate(On_Projectile.orig_Update orig, Projectile projectile, int i)
    {
        if (Running) Main.rand = new UnifiedRandom(101000 + weapons[index] * 400 + Tick * 1031 + i);
        orig(projectile, i);
    }
    private static int Damage(On_Main.orig_DamageVar_float_int_float orig, float damage, int percent, float luck)
    {
        if (!Running) return orig(damage, percent, luck);
        var random = Main.rand;
        try {
            // Give native damage variance identical rolls despite frame-timed
            // cosmetic RNG. Retain and compare every native input and result.
            Main.rand = new UnifiedRandom(71000 + weapons[index] * 400 + Tick * 31 + damageRoll++);
            int result = orig(damage, percent, luck);
            Rolls.Add(new { damage, percent, luck, result });
            return result;
        }
        finally { Main.rand = random; }
    }
    private static void Update(On_Main.orig_DoUpdateInWorld orig, Main self, Stopwatch timer)
    {
        // Render between batches; retain every native simulation update and trace row.
        int speed = Running ? UpdateBatchSize : 1;
        for (int i = 0; i < speed; i++) {
            if (i > 0 && Running) ModContent.GetInstance<LiveCombat>().PreUpdateEntities();
            orig(self, timer);
        }
    }
    public override void PostUpdateInput()
    {
        if (Output == null || opened || Main.dedServ || !Main.gameMenu || Main.menuMode != 0) return;
        opened = true;
        Player.LoadPlayer(Environment.GetEnvironmentVariable("RIAKAWA_LIVE_PLAYER")!, false).SetAsActive();
        WorldFile.GetAllMetadata(Environment.GetEnvironmentVariable("RIAKAWA_LIVE_WORLD")!, false).SetAsActive();
        Main.autoPause = false; Main.netMode = NetmodeID.SinglePlayer;
        WorldGen.playWorld();
    }
    public override void OnWorldLoad()
    {
        if (Output == null || Main.dedServ) return;
        if (Main.netMode != NetmodeID.SinglePlayer) throw new InvalidOperationException("Live replay needs a disposable single-player world.");
        using var catalog = JsonDocument.Parse(File.ReadAllBytes(Environment.GetEnvironmentVariable("RIAKAWA_QA_CATALOG")!));
        weapons = catalog.RootElement.GetProperty("weapons").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).Order().ToArray();
        string? selected = Environment.GetEnvironmentVariable("RIAKAWA_LIVE_ITEMS");
        if (!string.IsNullOrEmpty(selected)) weapons = selected.Split(',').Select(int.Parse).ToArray();
        if (ModLoader.TryGetMod("Riakawa", out var mod)) {
            var type = mod.Code.GetType("Riakawa.RiakawaConfig")!;
            var config = type.GetProperty("Current")!.GetValue(null)!;
            var field = type.GetField("Character")!;
            field.SetValue(config, Enum.Parse(field.FieldType, Environment.GetEnvironmentVariable("RIAKAWA_LIVE_CHARACTER") ?? "Chiikawa"));
        }
        index = 0; completed.Clear(); Running = true; Tick = -1;
        Directory.CreateDirectory(Output);
    }
    private static void Begin()
    {
        Main.rand = new UnifiedRandom(91000 + weapons[index]);
        for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i] = new Projectile { whoAmI = i };
        for (int i = 0; i < Main.maxNPCs; i++) Main.npc[i] = new NPC { whoAmI = i };
        for (int i = 0; i < Main.maxItems; i++) Main.item[i] = new Item();
        foreach (var dust in Main.dust) dust.active = false;
        foreach (var gore in Main.gore) gore.active = false;
        foreach (var text in Main.combatText) text.active = false;
        // Identical floor and wall exercise gravity, tile collision and bounces.
        for (int x = Main.spawnTileX - 85; x <= Main.spawnTileX + 85; x++)
            for (int y = Main.spawnTileY - 65; y <= Main.spawnTileY + 10; y++) {
                var tile = Main.tile[x, y];
                tile.ClearEverything();
                if (y >= Main.spawnTileY - 4 || x == Main.spawnTileX + 45) {
                    tile.HasTile = true; tile.TileType = TileID.Stone;
                }
            }
        var player = new Player { whoAmI = Main.myPlayer, active = true, name = "Combat replay",
            position = Start, oldPosition = Start, fallStart = (int)Start.Y / 16, fallStart2 = (int)Start.Y / 16,
            direction = 1, ConsumedLifeCrystals = 15, ConsumedManaCrystals = 9,
            statLife = 400, statMana = 200 };
        Main.player[Main.myPlayer] = player;
        player.downedDD2EventAnyDifficulty = true;
        player.inventory[0].SetDefaults(weapons[index]);
        player.inventory[0].stack = player.inventory[0].maxStack;
        ammo = 0;
        if (player.HeldItem.useAmmo > 0) {
            ammo = ContentSamples.ItemsByType.Values.Where(a => a.type < ItemID.Count && a.ammo == player.HeldItem.useAmmo)
                .OrderBy(a => a.type).First().type;
            player.inventory[54].SetDefaults(ammo); player.inventory[54].stack = 999;
        }
        NPC.NewNPC(new EntitySource_Misc("Combat replay"), (int)Start.X + 65, (int)Start.Y + 42, NPCID.BlueSlime);
        NPC.NewNPC(new EntitySource_Misc("Combat replay"), (int)Start.X + 220, (int)Start.Y + 42, NPCID.Zombie);
        NPC.NewNPC(new EntitySource_Misc("Combat replay"), (int)Start.X + 300, (int)Start.Y - 90, NPCID.CaveBat);
        Tick = 0; totalHits = 0; Hits.Clear(); spawned.Clear();
        trace = new StreamWriter(new GZipStream(File.Create(Path.Combine(Output!, $"{weapons[index]}.jsonl.gz")), CompressionLevel.Fastest));
        Save();
    }
    public override void PreUpdateEntities()
    {
        if (!Running) return;
        if (Tick < 0) Begin();
        damageRoll = 0; Rolls.Clear();
        // Rendering and OS timing are nondeterministic. Seed each simulation
        // tick equally; this compares native simulation, not RNG use by draws.
        Main.rand = new UnifiedRandom(91000 + weapons[index] * 400 + Tick);
        Counter.SetValue(null, (uint)(600 + Tick));
        Main.timeForVisualEffects = 600 + Tick;
        Main.GlobalTimeWrappedHourly = (600 + Tick) / 60f;
        Main.gfxQuality = 1f; Main.maxDustToDraw = Main.maxDust;
        Main.screenPosition = Start - new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);
        // Refresh the native biome/tile-buff cache at simulation time, rather than
        // letting asynchronous lighting frames decide when nearby buffs change.
        Main.LocalPlayer.ForceUpdateBiomes();
        Main.dayTime = true; Main.time = 27000; Main.hardMode = true;
        Main.raining = false; Main.rainTime = 0; Main.maxRaining = 0;
        Main.windSpeedCurrent = Main.windSpeedTarget = 0;
        Main.bloodMoon = Main.pumpkinMoon = Main.snowMoon = Main.eclipse = false;
        Main.playerInventory = Main.drawingPlayerChat = false;
    }
    public override void PostUpdateEverything()
    {
        if (!Running || Tick < 0) return;
        var p = Main.LocalPlayer;
        var projectiles = Main.projectile.Where(x => x.active).ToArray();
        foreach (var projectile in projectiles) spawned.Add(projectile.type);
        totalHits += Hits.Count;
        trace!.WriteLine(JsonSerializer.Serialize(new {
            tick = Tick,
            player = new { p.dead, p.statLife, p.statMana, p.statDefense, p.itemAnimation, p.itemAnimationMax,
                p.itemTime, p.itemTimeMax, p.reuseDelay, p.channel, p.direction, p.gravDir,
                pos = V(p.position), velocity = V(p.velocity), p.width, p.height,
                stack = p.HeldItem.stack, ammo = p.inventory[54].stack, p.buffType, p.buffTime },
            npcs = Main.npc.Where(x => x.active).Select(x => new { x.whoAmI, x.type, x.life, x.damage, x.defense,
                pos = V(x.position), velocity = V(x.velocity), x.width, x.height, x.ai, x.localAI,
                x.direction, x.collideX, x.collideY, x.buffType, x.buffTime }).ToArray(),
            projectiles = projectiles.Select(x => new { x.whoAmI, x.type, x.owner, x.damage, x.knockBack,
                pos = V(x.position), velocity = V(x.velocity), x.width, x.height, x.ai, x.localAI,
                x.rotation, x.scale, x.timeLeft, x.penetrate, x.extraUpdates, x.tileCollide, x.friendly,
                x.hostile, x.minion, x.sentry, x.usesLocalNPCImmunity, x.localNPCHitCooldown }).ToArray(), hits = Hits, damageRolls = Rolls
        }, Json));
        Hits.Clear();
        if (++Tick < Duration) return;
        trace.Dispose(); trace = null;
        completed.Add(new { itemId = weapons[index], ammoId = ammo, ticks = Tick, hits = totalHits,
            observedProjectiles = spawned.Order().ToArray(), life = p.statLife, mana = p.statMana,
            ammoRemaining = p.inventory[54].stack, stack = p.HeldItem.stack });
        if (++index < weapons.Length) { Tick = -1; Save(); return; }
        Running = false; Save();
        Mod.Logger.Info("PASS: live combat replay finished: " + completed.Count);
        Environment.Exit(0); // Disposable process: deliberately do not save its modified world/player.
    }
    internal static float[] V(Vector2 v) => [v.X, v.Y];
    private static void Save() => File.WriteAllText(Path.Combine(Output!, "run.json"), JsonSerializer.Serialize(new {
        running = Running, index, current = index < weapons.Length ? weapons[index] : 0, completed,
        riakawaLoaded = ModLoader.TryGetMod("Riakawa", out _), character = Environment.GetEnvironmentVariable("RIAKAWA_LIVE_CHARACTER"),
        simulationTicksPerCase = Duration, attackStartTick = 1, attackTicks = 180, updateBatchSize = UpdateBatchSize, perTickSeed = true, seededDamageRolls = true,
        seededItemChecksAndProjectileUpdates = true, nativeBiomeRefreshEachTick = true, dayTime = true,
        scope = "Native single-player updates; active slime/zombie/cave-bat AI at clear noon, stone floor/wall; initial 400 life/200 mana, no refill. Not every equipment/terrain/network combination."
    }, new JsonSerializerOptions { WriteIndented = true }));
}

public sealed class CombatControls : ModPlayer
{
    public override void SetControls()
    {
        if (!LiveCombat.Running || Player.whoAmI != Main.myPlayer) return;
        var target = Main.npc.Where(n => n.active && !n.friendly).OrderBy(n => Vector2.DistanceSquared(n.Center, Player.Center)).FirstOrDefault();
        Vector2 aim = target?.Center ?? Player.Center + new Vector2(600, -20);
        Main.mouseX = (int)(aim.X - Main.screenPosition.X); Main.mouseY = (int)(aim.Y - Main.screenPosition.Y);
        Player.controlLeft = Player.controlRight = Player.controlJump = Player.controlDown = Player.controlUp = Player.controlUseTile = false;
        Player.controlUseItem = LiveCombat.Tick > 0 && LiveCombat.Tick <= 180 && (Player.HeldItem.channel || LiveCombat.Tick % 40 != 39);
        if (target != null) Player.MinionAttackTargetNPC = target.whoAmI;
    }
    public override void OnHurt(Player.HurtInfo info)
    {
        if (LiveCombat.Running) LiveCombat.Hits.Add(new { kind = "player", info.Damage, info.Knockback, info.HitDirection });
    }
}

public sealed class CombatHits : GlobalNPC
{
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (LiveCombat.Running) { spawnRate = int.MaxValue; maxSpawns = 0; }
    }
    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        if (LiveCombat.Running) LiveCombat.Hits.Add(new { kind = "item", source = item.type, target = npc.whoAmI, damageDone, hit.Crit, hit.Knockback, hit.HitDirection });
    }
    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (LiveCombat.Running) LiveCombat.Hits.Add(new { kind = "projectile", source = projectile.type, target = npc.whoAmI, damageDone, hit.Crit, hit.Knockback, hit.HitDirection });
    }
}
