using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Riakawa;

// Explicit development export; runs after the pinned game has set all defaults.
public sealed class CatalogExporter : ModSystem
{
    public override void PostSetupContent()
    {
        string? output = Environment.GetEnvironmentVariable("RIAKAWA_EXPORT_CATALOG");
        if (string.IsNullOrEmpty(output)) return;
        var weapons = ContentSamples.ItemsByType.Values.Where(item => item.type > ItemID.None && item.type < ItemID.Count &&
            item.damage > 0 && item.useStyle > ItemUseStyleID.None && item.pick == 0 && item.axe == 0 && item.hammer == 0 &&
            item.createTile == -1 && !item.accessory).OrderBy(item => item.type).Select(item => new {
                id = item.type, name = ItemID.Search.GetName(item.type), item.damage, item.knockBack,
                item.width, item.height, item.useStyle, item.useTime, item.useAnimation, item.shoot,
                item.shootSpeed, item.useAmmo, item.mana, item.channel, item.noMelee, item.noUseGraphic,
                item.scale, item.consumable, damageClass = item.DamageType.Name,
                sound = item.UseSound is { } sound ? CosmeticAssets.SoundKey(sound) : null
            }).ToArray();
        var projectiles = ContentSamples.ProjectilesByType.Values.Where(p => p.type > ProjectileID.None && p.type < ProjectileID.Count)
            .OrderBy(p => p.type).Select(p => new {
                id = p.type, name = ProjectileID.Search.GetName(p.type), p.width, p.height, p.aiStyle,
                p.friendly, p.hostile, p.minion, p.sentry, frames = Main.projFrames[p.type],
                whip = ProjectileID.Sets.IsAWhip[p.type]
            }).ToArray();
        var ammunition = ContentSamples.ItemsByType.Values
            .Where(item => item.type > ItemID.None && item.type < ItemID.Count && item.ammo > 0)
            .OrderBy(item => item.type).Select(item => new {
                id = item.type, name = ItemID.Search.GetName(item.type), item.ammo, item.shoot
            }).ToArray();
        var ammoProjectiles = new List<object>();
        var probe = new Player();
        foreach (var weapon in weapons.Where(w => w.useAmmo > 0)) {
            probe.inventory[0] = ContentSamples.ItemsByType[weapon.id].Clone();
            probe.selectedItem = 0;
            foreach (var ammo in ammunition.Where(a => a.ammo == weapon.useAmmo)) {
                probe.inventory[54] = ContentSamples.ItemsByType[ammo.id].Clone();
                probe.inventory[54].stack = 999;
                if (!probe.PickAmmo(probe.inventory[0], out int projectile, out _, out _, out _, out int used,
                        dontConsume: true) || used != ammo.id || projectile <= 0 || projectile >= ProjectileID.Count ||
                        probe.inventory[54].stack != 999)
                    throw new InvalidOperationException($"Ammo catalog mismatch: {weapon.id}/{ammo.id}");
                ammoProjectiles.Add(new { weaponId = weapon.id, ammoId = ammo.id, projectileId = projectile });
            }
        }
        var music = typeof(MusicID).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(short) && f.Name != "Count")
            .Select(f => new { id = (short)f.GetRawConstantValue()!, name = f.Name }).OrderBy(x => x.id).ToArray();
        File.WriteAllText(output, JsonSerializer.Serialize(new {
            tModLoader = "2026.08.3.0", terraria = "1.4.4.9", scopeReviewed = false,
            scopeRule = "damage > 0; directly usable (including throwable ammunition); not mining tools, placeable traps or accessories",
            weapons, projectiles, ammunition, ammoProjectiles, music
        }, CosmeticAssets.JsonOptions));
        Mod.Logger.Info($"RIAKAWA_CATALOG_EXPORTED weapons={weapons.Length} projectiles={projectiles.Length}");
        if (!Main.dedServ) {
            var sizes = new Dictionary<string, int[]>();
            foreach (var item in weapons) {
                Main.instance.LoadItem(item.id);
                var texture = TextureAssets.Item[item.id].Value;
                sizes.Add($"Item/{item.id}", [texture.Width, texture.Height]);
            }
            foreach (var projectile in projectiles) {
                Main.instance.LoadProjectile(projectile.id);
                var texture = TextureAssets.Projectile[projectile.id].Value;
                sizes.Add($"Projectile/{projectile.id}", [texture.Width, texture.Height]);
            }
            File.WriteAllText(Path.ChangeExtension(output, ".textures.json"),
                JsonSerializer.Serialize(sizes, CosmeticAssets.JsonOptions));
            Mod.Logger.Info($"RIAKAWA_TEXTURE_SIZES_EXPORTED count={sizes.Count}");
        }
    }
}
