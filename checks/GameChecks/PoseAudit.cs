using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Riakawa;
using Riakawa.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace GameChecks;

// QA only: native use-style/frame/layer/transform code, neutral-light contact
// sheets. No substitute for observing projectile AI or equipment shaders live.
internal static class PoseAudit
{
    private static readonly List<object> BodyChecks = [];
    internal static void CheckCurrentBody(string expected)
    {
        var player=Main.LocalPlayer;
        string character=player.GetModPlayer<RiakawaPlayer>().Character.ToString();
        using var manifest=JsonDocument.Parse(ModLoader.GetMod("Riakawa").GetFileBytes("Assets/manifest.json"));
        var art=manifest.RootElement.GetProperty("players").EnumerateArray().Single(p=>p.GetProperty("character").GetString()==character);
        var texture=ModContent.Request<Texture2D>("Riakawa/"+art.GetProperty("texture").GetString(),ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        var data=new List<DrawData>();var draw=new PlayerDrawSet();
        draw.BoringSetup(player,data,[],[],player.position,0,0,Vector2.Zero);
        PlayerLoader.ModifyDrawInfo(ref draw);
        foreach (var layer in PlayerDrawLayerLoader.GetDrawLayers(draw)) layer.DrawWithTransformationAndChildren(ref draw);
        var body=data.Where(d=>ReferenceEquals(d.texture,texture)).ToArray();
        int row=expected=="invisible"?-1:art.GetProperty("animations").GetProperty(expected).GetProperty("row").GetInt32();
        if (row<0 ? body.Length!=0 : body.Length!=1 || body[0].sourceRect?.Y!=row*art.GetProperty("frameHeight").GetInt32())
            throw new InvalidOperationException($"Body state mismatch: {character}/{expected}");
        BodyChecks.Add(new { character,expected,player.itemAnimation,velocity=new[]{player.velocity.X,player.velocity.Y},
            player.wet,player.invis,player.gravDir,mount=player.mount.Active,frame=body.Length==0?null:body[0].sourceRect?.ToString() });
        File.WriteAllText(Path.ChangeExtension(Snapshot.Output!,".body-checks.json"),JsonSerializer.Serialize(BodyChecks,new JsonSerializerOptions { WriteIndented=true }));
    }
    private static int next;
    internal static void Tick()
    {
        if (Environment.GetEnvironmentVariable("RIAKAWA_QA_POSES") != "1" || next>=426 || Main.GameUpdateCount%120!=0) return;
        int start=next; next+=16;
        Run(16,start); // Yield to native networking between small render batches.
    }

    internal static void Run(int limit, int start=0)
    {
        string directory = Path.Combine(Path.GetDirectoryName(Snapshot.Output!)!,"poses");
        Directory.CreateDirectory(directory);
        using var manifest = JsonDocument.Parse(ModLoader.GetMod("Riakawa").GetFileBytes("Assets/manifest.json"));
        var ids = manifest.RootElement.GetProperty("weapons").EnumerateArray()
            .Select(w => w.GetProperty("itemId").GetInt32()).Distinct().Order().Skip(start).Take(limit).ToArray();
        var graphics = Main.instance.GraphicsDevice;
        var targets = graphics.GetRenderTargets(); var viewport = graphics.Viewport;
        var blend = graphics.BlendState; var depth = graphics.DepthStencilState;
        var rasterizer = graphics.RasterizerState; var sampler = graphics.SamplerStates[0];
        var oldRandom = Main.rand; var oldConfig = RiakawaConfig.Current.ReplaceWeapons;
        int mouseX = Main.mouseX, mouseY = Main.mouseY;
        const int cell = 256;
        using var target = new RenderTarget2D(graphics,12*cell,4*cell);
        using var batch = new SpriteBatch(graphics);
        var results = new List<object>();
        bool begun = false;
        try {
            RiakawaConfig.Current.ReplaceWeapons = true;
            foreach (int id in ids) {
                Main.instance.LoadItem(id);
                Main.GetItemDrawFrame(id,out var native,out var heldFrame);
                graphics.SetRenderTarget(target); graphics.Clear(new Color(38,42,50));
                batch.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.PointClamp,
                    DepthStencilState.None,RasterizerState.CullNone); begun = true;
                var poses = new List<object>();
                foreach (Character character in Enum.GetValues<Character>())
                    for (int orientation = 0; orientation < 4; orientation++)
                        for (int phase = 0; phase < 3; phase++) {
                            var player = new Player { whoAmI = 254, active = true, position = Main.LocalPlayer.position,
                                direction = orientation % 2 == 0 ? 1 : -1, gravDir = orientation < 2 ? 1 : -1 };
                            player.inventory[0].SetDefaults(id); player.selectedItem = 0;
                            player.lastVisualizedSelectedItem = player.HeldItem.Clone();
                            player.GetModPlayer<RiakawaPlayer>().Character = character;
                            player.itemAnimationMax = Math.Max(1,player.HeldItem.useAnimation);
                            player.itemAnimation = Math.Max(1,player.itemAnimationMax*(3-phase)/4);
                            player.itemTime = player.itemAnimation;
                            player.itemRotation = player.direction == 1 ? 0 : MathHelper.Pi;
                            Main.mouseX = (int)(player.Center.X-Main.screenPosition.X)+200*player.direction;
                            Main.mouseY = (int)(player.Center.Y-Main.screenPosition.Y);
                            player.ItemCheck_ApplyUseStyle(0,player.HeldItem,heldFrame);
                            player.PlayerFrame();
                            var data = new List<DrawData>();
                            var draw = new PlayerDrawSet();
                            var position = Main.screenPosition + new Vector2((orientation*3+phase)*cell+118,(int)character*cell+104);
                            draw.BoringSetup(player,data,[],[],position,0,0,Vector2.Zero);
                            draw.colorArmorBody = draw.colorArmorHead = draw.colorArmorLegs = Color.White;
                            PlayerLoader.ModifyDrawInfo(ref draw);
                            foreach (var layer in PlayerDrawLayerLoader.GetDrawLayers(draw))
                                layer.DrawWithTransformationAndChildren(ref draw);
                            PlayerDrawLayers.DrawPlayer_TransformDrawData(ref draw);
                            PlayerLoader.TransformDrawData(ref draw);
                            var paths = new List<object>();
                            var entry = manifest.RootElement.GetProperty("weapons").EnumerateArray()
                                .First(w => w.GetProperty("itemId").GetInt32()==id &&
                                    w.GetProperty("character").GetString()==(character==Character.Original?"Chiikawa":character.ToString()));
                            var heldTexture = character==Character.Original?native:
                                ModContent.Request<Texture2D>("Riakawa/"+entry.GetProperty("texture").GetString(),ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
                            if (entry.TryGetProperty("heldBindings",out var heldBindings) && heldBindings.GetArrayLength()>0 && heldBindings[0].GetProperty("slot").GetString()=="Extra")
                                heldTexture=character==Character.Original?TextureAssets.Extra[heldBindings[0].GetProperty("id").GetInt32()].Value:
                                    ModContent.Request<Texture2D>("Riakawa/"+heldBindings[0].GetProperty("texture").GetString(),ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
                            if (!player.HeldItem.noUseGraphic && !data.Any(d=>ReferenceEquals(d.texture,heldTexture)))
                                throw new InvalidOperationException($"Native held layer missing art: {id}/{character}/{orientation}/{phase}");
                            var bindings = new Dictionary<Texture2D,string>();
                            if (entry.TryGetProperty("heldBindings",out var heldLayers)) foreach (var binding in heldLayers.EnumerateArray()) {
                                string slot=binding.GetProperty("slot").GetString()!; int index=binding.GetProperty("id").GetInt32();
                                var textures=slot switch { "Item"=>TextureAssets.Item,"Extra"=>TextureAssets.Extra,
                                    "GlowMask"=>TextureAssets.GlowMask,"ItemFlame"=>TextureAssets.ItemFlame,
                                    _=>throw new InvalidOperationException(slot) };
                                var original=textures[index].Value;
                                var replacement=ModContent.Request<Texture2D>("Riakawa/"+binding.GetProperty("texture").GetString(),ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
                                if (character!=Character.Original && data.Any(d=>ReferenceEquals(d.texture,original)))
                                    throw new InvalidOperationException($"Unreplaced held binding: {id}/{character}/{slot}/{index}");
                                bindings[character==Character.Original?original:replacement]=$"{slot}/{index}";
                            }
                            foreach (var datum in data) {
                                if (datum.texture == null) continue;
                                if (ReferenceEquals(datum.texture,TextureAssets.MagicPixel.Value) && datum.sourceRect != new Rectangle(0,0,1,1))
                                    throw new InvalidOperationException("Paw primitives must sample exactly one pixel");
                                if (datum.sourceRect is { } rect && !datum.texture.Bounds.Contains(rect))
                                    throw new InvalidOperationException($"Source rectangle outside sheet: {id}/{character}/{rect}");
                                var neutral = datum; neutral.color=datum.texture==TextureAssets.MagicPixel.Value?datum.color:Color.White; neutral.Draw(batch);
                                paths.Add(new { texture = datum.texture.Name, binding = bindings.GetValueOrDefault(datum.texture), held = ReferenceEquals(datum.texture,heldTexture), source = datum.sourceRect?.ToString(),
                                    x = datum.position.X, y = datum.position.Y, datum.rotation, effects = datum.effect.ToString() });
                            }
                            poses.Add(new { character = character.ToString(), orientation, phase,
                                player.direction, player.gravDir, player.HeldItem.noUseGraphic, layers = paths });
                        }
                batch.End(); begun = false;
                using (var file = File.Create(Path.Combine(directory,$"item-{id}.png"))) target.SaveAsPng(file,target.Width,target.Height);
                results.Add(new { itemId = id, poses });
            }
            File.WriteAllText(Path.Combine(directory,$"part-{start}.json"),JsonSerializer.Serialize(results));
        }
        finally {
            if (begun) batch.End();
            graphics.SetRenderTargets(targets);graphics.Viewport=viewport;
            graphics.BlendState=blend;graphics.DepthStencilState=depth;graphics.RasterizerState=rasterizer;graphics.SamplerStates[0]=sampler;
            Main.rand=oldRandom;Main.mouseX=mouseX;Main.mouseY=mouseY;RiakawaConfig.Current.ReplaceWeapons=oldConfig;
        }
        ModLoader.GetMod("GameChecks").Logger.Info($"PASS: native layer pose sheets: {results.Count} weapons x 4 characters x 4 orientations x 3 use phases");
    }
}
