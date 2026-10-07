using System.ComponentModel;
using Riakawa.Core;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace Riakawa;

public enum InterfaceLanguage { English, Korean }

public sealed class RiakawaConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;
    [DefaultValue(InterfaceLanguage.English)] public InterfaceLanguage InterfaceLanguage;
    [DefaultValue(Character.Chiikawa)] public Character Character = Character.Chiikawa;
    public override void OnChanged() => RiakawaLocalization.Apply();
    public static RiakawaConfig Current => ModContent.GetInstance<RiakawaConfig>();
}
