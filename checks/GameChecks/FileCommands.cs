using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.UI.Chat;
using Terraria.ModLoader;

namespace GameChecks;

// QA-only local command queue avoids dropped virtual-display keystrokes.
internal sealed class FileCommands : CommandCaller
{
    private static readonly Queue<string> Pending = new();
    public CommandType CommandType => CommandType.Chat;
    public Player Player => Main.LocalPlayer;
    public void Reply(string text, Color color = default) => ModLoader.GetMod("GameChecks").Logger.Info(text);
    internal static void Tick()
    {
        string path=Path.ChangeExtension(Snapshot.Output!,".commands.txt");
        if (Pending.Count==0 && File.Exists(path)) {
            foreach (string line in File.ReadAllLines(path).Where(s=>!string.IsNullOrWhiteSpace(s))) Pending.Enqueue(line);
            File.Delete(path);
        }
        if (!Pending.TryDequeue(out string? text)) return;
        Main.drawingPlayerChat=false; Main.chatText="";
        var args=text.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        var caller=new FileCommands();
        caller.Reply("QA command: "+text);
        if (args[0]=="/rq") ModContent.GetInstance<CheckCommand>().Action(caller,text,args[1..]);
        else if (args[0]=="/rqsweep") ModContent.GetInstance<SweepCommand>().Action(caller,text,args[1..]);
        else ChatHelper.SendChatMessageFromClient(ChatManager.Commands.CreateOutgoingMessage(text));
    }
}
