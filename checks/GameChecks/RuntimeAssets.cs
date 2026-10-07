using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Riakawa;
using Riakawa.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace GameChecks;

// Explicit client-side release audit of the packaged assets and real draw hooks.
// No claim about live projectile trajectories, native AI or combat timing.
internal static class RuntimeAssets
{
    internal static void Check()
    {
        if (Main.dedServ || Snapshot.Output == null) throw new InvalidOperationException("Client QA only");
        using var doc = JsonDocument.Parse(ModLoader.GetMod("Riakawa").GetFileBytes("Assets/manifest.json"));
        var textures = new HashSet<string>();
        var sounds = new HashSet<string>();
        foreach (var p in doc.RootElement.GetProperty("players").EnumerateArray()) {
            textures.Add(p.GetProperty("texture").GetString()!);
            foreach (var v in p.GetProperty("voices").EnumerateObject()) sounds.Add(v.Value.GetString()!);
        }
        foreach (var w in doc.RootElement.GetProperty("weapons").EnumerateArray()) {
            textures.Add(w.GetProperty("texture").GetString()!);
            if (w.TryGetProperty("heldBindings",out var heldBindings))
                foreach (var b in heldBindings.EnumerateArray()) textures.Add(b.GetProperty("texture").GetString()!);
            if (w.TryGetProperty("useSound",out var s) && s.ValueKind == JsonValueKind.String) sounds.Add(s.GetString()!);
            foreach (var cue in w.GetProperty("sounds").EnumerateObject()) sounds.Add(cue.Value.GetString()!);
            foreach (var a in w.GetProperty("attacks").EnumerateArray()) {
                if (a.TryGetProperty("texture",out var t) && t.ValueKind == JsonValueKind.String) textures.Add(t.GetString()!);
                foreach (var b in a.GetProperty("bindings").EnumerateArray()) textures.Add(b.GetProperty("texture").GetString()!);
                foreach (var cue in a.GetProperty("sounds").EnumerateObject()) sounds.Add(cue.Value.GetString()!);
            }
        }
        textures.Add(doc.RootElement.GetProperty("particleAtlas").GetString()!);
        var loaded = textures.Order().Select(path => {
            var texture = ModContent.Request<Texture2D>("Riakawa/"+path,AssetRequestMode.ImmediateLoad).Value;
            return new { path, texture.Width, texture.Height };
        }).ToArray();
        var audio = sounds.Order().Select(path => {
            var effect = new SoundStyle("Riakawa/"+path).GetSoundEffect();
            return new { path, seconds = effect.Duration.TotalSeconds };
        }).ToArray();
        var observer = Main.LocalPlayer.GetModPlayer<RiakawaPlayer>();
        var old = observer.Character;
        bool enabled = RiakawaConfig.Current.ReplaceWeapons;
        var graphics = Main.instance.GraphicsDevice;
        var targets = graphics.GetRenderTargets();
        var viewport = graphics.Viewport;
        var blend = graphics.BlendState;
        var depth = graphics.DepthStencilState;
        var rasterizer = graphics.RasterizerState;
        var sampler = graphics.SamplerStates[0];
        using var target = new RenderTarget2D(graphics,256,256);
        using var batch = new SpriteBatch(graphics);
        var hook = new WeaponVisuals();
        var owner = new Player();
        var cases = new List<object>();
        bool begun = false;
        try {
            RiakawaConfig.Current.ReplaceWeapons = true;
            graphics.SetRenderTarget(target);
            batch.Begin(); begun = true;
            foreach (var w in doc.RootElement.GetProperty("weapons").EnumerateArray()) {
                int id = w.GetProperty("itemId").GetInt32();
                var character = Enum.Parse<Character>(w.GetProperty("character").GetString()!);
                var item = new Item(id);
                Main.instance.LoadItem(id);
                Main.GetItemDrawFrame(id,out _,out var frame);
                var texture = ModContent.Request<Texture2D>("Riakawa/"+w.GetProperty("texture").GetString(),AssetRequestMode.ImmediateLoad).Value;
                owner.GetModPlayer<RiakawaPlayer>().Character = character;
                var drawInfo = new PlayerDrawSet { drawPlayer = owner };
                observer.Character = Character.Original;
                var held = new DrawData(TextureAssets.Item[id].Value,Vector2.Zero,Color.White);
                DrawData? colored = null, glow = held;
                hook.PreModifyItemDraw(item,ref drawInfo,ref held,ref colored,ref glow);
                if (!ReferenceEquals(held.texture,texture) || !ReferenceEquals(glow?.texture,TextureAssets.Item[id].Value))
                    throw new InvalidOperationException($"Held art did not follow owner: {id}/{character}");
                observer.Character = character;
                owner.GetModPlayer<RiakawaPlayer>().Character = Character.Original;
                float rotation = 0, scale = 1;
                bool inv = hook.PreDrawInInventory(item,batch,new Vector2(128),frame,Color.White,Color.White,Vector2.Zero,1);
                bool drop = hook.PreDrawInWorld(item,batch,Color.White,Color.White,ref rotation,ref scale,0);
                if (inv || drop || rotation != 0 || scale != 1)
                    throw new InvalidOperationException($"Observer draw/transform mismatch: {id}/{character}");
                observer.Character = Character.Original;
                if (!hook.PreDrawInInventory(item,batch,Vector2.Zero,frame,Color.White,Color.White,Vector2.Zero,1) ||
                    !hook.PreDrawInWorld(item,batch,Color.White,Color.White,ref rotation,ref scale,0))
                    throw new InvalidOperationException($"Original fallback failed: {id}");
                observer.Character = character;
                RiakawaConfig.Current.ReplaceWeapons = false;
                if (!hook.PreDrawInInventory(item,batch,Vector2.Zero,frame,Color.White,Color.White,Vector2.Zero,1))
                    throw new InvalidOperationException($"Weapon toggle failed: {id}");
                RiakawaConfig.Current.ReplaceWeapons = true;
                cases.Add(new { id, character = character.ToString(), heldOwner = true, inventoryObserver = true,
                    droppedObserver = true, originalFallback = true, disabledFallback = true });
            }
            batch.End(); begun = false;
        }
        finally {
            if (begun) batch.End();
            graphics.SetRenderTargets(targets); graphics.Viewport = viewport;
            graphics.BlendState = blend; graphics.DepthStencilState = depth;
            graphics.RasterizerState = rasterizer; graphics.SamplerStates[0] = sampler;
            observer.Character = old; RiakawaConfig.Current.ReplaceWeapons = enabled;
        }
        File.WriteAllText(Path.ChangeExtension(Snapshot.Output,".assets.json"),JsonSerializer.Serialize(new {
            utc = DateTime.UtcNow, loaded, audio, cases,
            scope = "Packaged assets loaded through tModLoader; real inventory/world/held hooks called through the FNA graphics device. Static render contract only, not live alignment or combat parity."
        }));
    }
}
