using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CombatProbe;

// QA-only: no reference to Riakawa, so the same probe runs with and without it.
public sealed class CombatProbe : Mod { }
public sealed class Export : ModSystem
{
    public override void PostSetupContent()
    {
        string? output = Environment.GetEnvironmentVariable("RIAKAWA_COMBAT_PROBE");
        if (!Main.dedServ || string.IsNullOrEmpty(output)) return;
        object Fields(object entity) => entity.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.FieldType.IsPrimitive || f.FieldType.IsEnum)
            .OrderBy(f => f.Name).ToDictionary(f => f.Name,f => f.GetValue(entity));
        // Native flames randomize scale/hitbox in SetDefaults; use identical
        // per-type seeds instead of comparing unrelated startup RNG streams.
        var items = Enumerable.Range(1,ItemID.Count-1).Select(id => {
            Main.rand = new UnifiedRandom(7300+id);
            var value = new Item(); value.SetDefaults(id);
            return new System.Collections.Generic.KeyValuePair<int,Item>(id,value);
        })
            .OrderBy(p => p.Key).ToDictionary(p => p.Key,p => new { fields = Fields(p.Value), damageClass = p.Value.DamageType.Name });
        var projectiles = Enumerable.Range(1,ProjectileID.Count-1).Select(id => {
            Main.rand = new UnifiedRandom(7300+id);
            var value = new Projectile(); value.SetDefaults(id);
            return new System.Collections.Generic.KeyValuePair<int,Projectile>(id,value);
        })
            .OrderBy(p => p.Key).ToDictionary(p => p.Key,p => new { fields = Fields(p.Value), damageClass = p.Value.DamageType.Name,
                frames = Main.projFrames[p.Key], whip = ProjectileID.Sets.IsAWhip[p.Key] });
        File.WriteAllText(output,JsonSerializer.Serialize(new { utc = DateTime.UtcNow,
            riakawaLoaded = ModLoader.TryGetMod("Riakawa",out _), seedBase = 7300, items, projectiles }, new JsonSerializerOptions {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
        Mod.Logger.Info($"PASS: native defaults exported: {items.Count} items, {projectiles.Count} projectiles; no world entered");
        Environment.Exit(0);
    }
}
