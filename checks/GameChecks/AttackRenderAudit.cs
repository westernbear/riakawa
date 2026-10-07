using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Riakawa;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace GameChecks;

internal static class AttackRenderAudit
{
    internal static bool Enabled => Environment.GetEnvironmentVariable("RIAKAWA_QA_VISUAL_AUDIT") == "1";
    private static readonly Dictionary<string, HashSet<string>> Rows = [];
    private static readonly HashSet<string> Failures = [];
    private static readonly Dictionary<Texture2D,string> Names = [];
    private static readonly FieldInfo[] Slots = typeof(TextureAssets).GetFields(BindingFlags.Public|BindingFlags.Static);
    private static readonly FieldInfo ManifestField = typeof(CosmeticAssets).GetField("Manifest",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static (string Key, Dictionary<Texture2D,string> Originals, Dictionary<Texture2D,string> Replacements)? current;
    internal static object? Begin(Projectile projectile)
    {
        var previous=current;
        current=null;
        if (!Enabled || Snapshot.Output == null || projectile.type>=ProjectileID.Count || projectile.owner<0 || projectile.owner>=Main.maxPlayers) return previous;
        var character=Main.player[projectile.owner].GetModPlayer<RiakawaPlayer>().Character;
        bool nativeOnly=character==Riakawa.Core.Character.Original || !RiakawaConfig.Current.ReplaceWeapons;
        int weapon=projectile.GetGlobalProjectile<AttackOrigin>().WeaponId;
        var manifest=(AssetManifest)ManifestField.GetValue(null)!;
        var art=manifest.Weapons.Find(w=>w.ItemId==weapon && w.Character==(nativeOnly?Riakawa.Core.Character.Chiikawa:character))?.Attacks.Find(a=>a.ProjectileId==projectile.type);
        if (weapon==0) return previous;
        string key=$"{weapon}/{projectile.type}/{(nativeOnly?"Original":character.ToString())}";
        if (art==null) { Failures.Add("unmapped attack "+key); return previous; }
        var originals=new Dictionary<Texture2D,string>();var replacements=new Dictionary<Texture2D,string>();
        foreach (var binding in art.Bindings.Prepend(new TextureBinding { Slot="Projectile",Id=projectile.type,Texture=art.Texture??"" })) {
            if (string.IsNullOrEmpty(binding.Texture)) continue;
            if (binding.Slot=="Projectile") Main.instance.LoadProjectile(binding.Id);
            if (binding.Slot=="Item") Main.instance.LoadItem(binding.Id);
            var slot=Slots.First(s=>s.Name==binding.Slot).GetValue(null);
            var native=slot is Asset<Texture2D>[] array?array[binding.Id]:(Asset<Texture2D>)slot!;
            var custom=ModContent.Request<Texture2D>("Riakawa/"+binding.Texture,AssetRequestMode.ImmediateLoad);
            originals[native.Value]=binding.Slot+"/"+binding.Id;
            replacements[custom.Value]=custom.Name;
            if (native.Value.Bounds!=custom.Value.Bounds) Failures.Add("dimensions "+key+" "+binding.Texture);
        }
        Rows.TryAdd(key,[]);current=(key,originals,replacements);return previous;
    }
    internal static void End(object? previous) => current=previous is null?null:
        ((string Key, Dictionary<Texture2D,string> Originals, Dictionary<Texture2D,string> Replacements))previous;
    internal static void Observe(Texture2D texture, Rectangle? source)
    {
        if (current is not { } context) return;
        string name;
        if (context.Replacements.TryGetValue(texture,out var custom)) name=custom;
        else if (context.Originals.TryGetValue(texture,out var native)) {
            name=(context.Key.EndsWith("/Original")?"native:":"UNREPLACED:")+native;
            if (!context.Key.EndsWith("/Original")) Failures.Add(context.Key+" "+name);
        }
        else {
            if (!Names.TryGetValue(texture,out name!)) {
                foreach (var field in Slots) {
                    if (field.GetValue(null) is Asset<Texture2D>[] array)
                        for (int i=0;i<array.Length;i++) {
                            if (array[i]?.IsLoaded==true) Names.TryAdd(array[i].Value,field.Name+"/"+i);
                        }
                    else if (field.GetValue(null) is Asset<Texture2D> asset && asset.IsLoaded) Names.TryAdd(asset.Value,field.Name+"/0");
                }
                name=Names.GetValueOrDefault(texture,"unknown:"+texture.Width+"x"+texture.Height);
            }
            name="native:"+name;
        }
        var rect=source??texture.Bounds;
        if (!texture.Bounds.Contains(rect)) Failures.Add(context.Key+" source outside "+name+" "+rect);
        Rows[context.Key].Add(name+" "+rect);
    }
    internal static void Save()
    {
        if (!Enabled || Snapshot.Output==null) return;
        File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".render-audit.json"),JsonSerializer.Serialize(new {
            rows=Rows.OrderBy(x=>x.Key).ToDictionary(x=>x.Key,x=>x.Value.Order().ToArray()),failures=Failures.Order().ToArray(),
            scope="Actual native SpriteBatch/shader calls inside scoped projectile rendering; QA character cycling, not combat parity"
        },new JsonSerializerOptions { WriteIndented=true }));
    }
}
