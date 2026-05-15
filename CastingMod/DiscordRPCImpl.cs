using DiscordRPC;
using Assets = DiscordRPC.Assets;

namespace vynscastingmod
{
    public class DiscordRPCImpl
    {
        // Discord RPC library from https://github.com/Lachee/discord-rpc-csharp
        
        public static DiscordRpcClient client;
        public static void InitRPC()
        {
            client = new DiscordRpcClient("1483602116239954011");
            client.Initialize();
            client.SetPresence(new RichPresence()
            {
                Assets = new DiscordRPC.Assets()
                {
                    LargeImageKey = "mango",
                    SmallImageKey = "smallicon"
                },
                
                Buttons = new Button[]
                {
                    new Button() { Label = "Github", Url = "https://github.com/rileyig/vynscastingmod" },
                    new Button() { Label = "Discord", Url = "https://discord.gg/KPhreBySxr" }
                    
                }
            });
        }
    }
}