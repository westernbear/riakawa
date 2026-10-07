using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using ReLogic.Content;
using Riakawa;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace GameChecks;

// Opt-in native rendering audit. Replacements must be disabled so exported
// references cannot accidentally be Riakawa art. Observe every SpriteBatch.Draw
// overload, including direct chain/glow calls that bypass Main.EntitySpriteDraw.
public sealed class BindingTrace : ModSystem
{
    private Projectile? current;
    private readonly Dictionary<Texture2D, (string Slot, int Id)> textures = [];
    private readonly HashSet<(int Weapon, int Projectile, string Slot, int Id)> seen = [];
    private readonly HashSet<string> exported = [];
    private readonly Dictionary<string, int[]> sizes = [];
    private readonly List<ILHook> hooks = [];
    private readonly HashSet<(int Weapon, int Projectile, string Texture)> shaderDraws = [];
    private static readonly FieldInfo[] Fields = typeof(TextureAssets).GetFields(BindingFlags.Public | BindingFlags.Static);
    private static readonly FieldInfo[] ShaderImages = typeof(MiscShaderData).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(f => f.FieldType == typeof(Asset<Texture2D>)).ToArray();

    public override void Load()
    {
        if (Main.dedServ || Environment.GetEnvironmentVariable("RIAKAWA_QA_BINDINGS") != "1" && !AttackRenderAudit.Enabled) return;
        On_Main.DrawProjDirect += Draw;
        On_MiscShaderData.Apply += Shader;
        hooks.Add(new ILHook(typeof(MiscShaderData).GetMethod("Apply",[typeof(DrawData?)])!,
            il => new ILCursor(il).Emit(OpCodes.Ldarg_0).EmitDelegate<Action<MiscShaderData>>(RecordShader)));
        foreach (var method in typeof(SpriteBatch).GetMethods().Where(m => m.Name == "Draw" &&
                     m.GetParameters().FirstOrDefault()?.ParameterType == typeof(Texture2D)))
        {
            hooks.Add(new ILHook(method, il => {
                var cursor = new ILCursor(il);
                cursor.Emit(OpCodes.Ldarg_1).EmitDelegate<Action<Texture2D>>(Observe);
                int rect = Array.FindIndex(method.GetParameters(),p => p.ParameterType == typeof(Rectangle?));
                if (rect >= 0) cursor.Emit(OpCodes.Ldarg_1).EmitLdarg(rect+1)
                    .EmitDelegate<Action<Texture2D,Rectangle?>>(AttackRenderAudit.Observe);
                else cursor.Emit(OpCodes.Ldarg_1).EmitDelegate<Action<Texture2D>>(t => AttackRenderAudit.Observe(t,null));
            }));
        }
    }
    public override void Unload()
    {
        On_Main.DrawProjDirect -= Draw;
        On_MiscShaderData.Apply -= Shader;
        foreach (var hook in hooks) hook.Dispose();
        hooks.Clear();
        textures.Clear(); seen.Clear(); exported.Clear(); sizes.Clear(); shaderDraws.Clear(); current = null;
    }
    private void Shader(On_MiscShaderData.orig_Apply orig, MiscShaderData shader, DrawData? drawData)
    {
        if (current != null)
            foreach (var field in ShaderImages)
                if (field.GetValue(shader) is Asset<Texture2D> asset) Observe(asset.Value);
        orig(shader, drawData);
    }
    private void Draw(On_Main.orig_DrawProjDirect orig, Main main, Projectile projectile)
    {
        var previous = current;
        current = projectile.type < ProjectileID.Count ? projectile : null;
        var audit = AttackRenderAudit.Begin(projectile);
        try { orig(main, projectile); }
        finally { current = previous; AttackRenderAudit.End(audit); }
    }
    // Runs inside native Apply, after the production wrapper installed its
    // temporary assets. Capture the actual textures reaching the shader.
    private void RecordShader(MiscShaderData shader)
    {
        if (current == null || Snapshot.Output == null || !RiakawaConfig.Current.ReplaceWeapons) return;
        int weapon = current.GetGlobalProjectile<AttackOrigin>().WeaponId;
        if (weapon == 0) return;
        bool added = false;
        foreach (var field in ShaderImages)
            if (field.GetValue(shader) is Asset<Texture2D> asset) {
                AttackRenderAudit.Observe(asset.Value,null);
                added |= shaderDraws.Add((weapon,current.type,asset.Name));
            }
        if (added) File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".shader-draws.json"),
            JsonSerializer.Serialize(shaderDraws.OrderBy(x => x.Weapon).ThenBy(x => x.Projectile).ThenBy(x => x.Texture)
                .Select(x => new { weaponId = x.Weapon, projectileId = x.Projectile, texture = x.Texture }),
                new JsonSerializerOptions { WriteIndented = true }));
    }
    private void Observe(Texture2D texture)
    {
        if (current == null || Snapshot.Output == null || RiakawaConfig.Current.ReplaceWeapons) return;
        int weapon = current.GetGlobalProjectile<AttackOrigin>().WeaponId;
        if (weapon == 0) return;
        if (!textures.ContainsKey(texture)) {
            foreach (var field in Fields) {
                if (field.GetValue(null) is Asset<Texture2D>[] array) {
                    for (int i = 0; i < array.Length; i++)
                        if (array[i]?.IsLoaded == true) textures.TryAdd(array[i].Value, (field.Name, i));
                }
                else if (field.GetValue(null) is Asset<Texture2D> asset && asset.IsLoaded)
                    textures.TryAdd(asset.Value, (field.Name, 0));
            }
        }
        if (!textures.TryGetValue(texture, out var slot) || !seen.Add((weapon,current.type,slot.Slot,slot.Id))) return;
        string key = $"{slot.Slot}/{slot.Id}";
        if (exported.Add(key)) {
            string directory = Path.Combine(Path.GetDirectoryName(Snapshot.Output)!, "reference-textures");
            Directory.CreateDirectory(directory);
            using var output = File.Create(Path.Combine(directory, $"{slot.Slot}_{slot.Id}.png"));
            texture.SaveAsPng(output, texture.Width, texture.Height);
            sizes[key] = [texture.Width,texture.Height];
            File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".binding-sizes.json"),JsonSerializer.Serialize(sizes));
        }
        File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".bindings.json"),
            JsonSerializer.Serialize(seen.OrderBy(s => s.Weapon).ThenBy(s => s.Projectile).ThenBy(s => s.Slot).ThenBy(s => s.Id)
                .Select(s => new { weaponId = s.Weapon, projectileId = s.Projectile, slot = s.Slot, id = s.Id }),
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
