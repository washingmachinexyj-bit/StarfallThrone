using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Ecology;

public sealed class EcologyPlayer : ModPlayer
{
    public string Identity=Guid.NewGuid().ToString("N");
    private bool acceptedIdentity;
    private ulong lastRequest;
    public override void Initialize(){Identity=Guid.NewGuid().ToString("N");acceptedIdentity=false;lastRequest=0;}
    public override void SaveData(TagCompound tag)=>tag["EcologyIdentity"]=Identity;
    public override void LoadData(TagCompound tag){if(Guid.TryParse(tag.GetString("EcologyIdentity"),out var id))Identity=id.ToString("N");}
    public override void OnEnterWorld()
    {
        if(Player.whoAmI!=Main.myPlayer)return;EcologyRegionUI.Close();
        if(Main.netMode==NetmodeID.MultiplayerClient){var p=Packet(0);p.Write(new Guid(Identity).ToByteArray());p.Send();}
    }
    public override void SyncPlayer(int toWho,int fromWho,bool newPlayer)
    {
        if(Main.netMode!=NetmodeID.Server||!acceptedIdentity)return;
        var p=Packet(6);p.Write((byte)Player.whoAmI);p.Write(new Guid(Identity).ToByteArray());p.Send(toWho,fromWho);
    }
    public static ModPacket Packet(byte action){var p=ModContent.GetInstance<global::StarfallThrone.StarfallThrone>().GetPacket();p.Write((byte)50);p.Write(action);return p;}
    public static bool Reach(Player p,Point q,float pixels)=>p!=null&&p.active&&!p.dead&&!p.ghost&&WorldGen.InWorld(q.X,q.Y,10)&&Vector2.DistanceSquared(p.Center,q.ToWorldCoordinates())<=pixels*pixels;
    /// <summary>Returns Terraria's already-normalized tile target, avoiding a second UI/input-scale conversion.</summary>
    public static Point CursorTile(Player p)
    {
        Point target=new(Player.tileTargetX,Player.tileTargetY);
        return WorldGen.InWorld(target.X,target.Y,10)?target:Main.MouseWorld.ToTileCoordinates();
    }
    public static void Tell(Player player,string key,params object[] args)
    {
        var message=EcologyCatalog.Text(key,args);
        if(Main.netMode==NetmodeID.Server)ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(message),Color.LightCyan,player.whoAmI);else Main.NewText(message,Color.LightCyan);
    }
    public static void Request(byte action,Point point)
    {
        if(Main.netMode==NetmodeID.SinglePlayer){Perform(Main.LocalPlayer,action,point,EcologyRegionUI.Bounds,EcologyRegionUI.Biome,EcologyRegionUI.Ecology);return;}
        if(Main.netMode!=NetmodeID.MultiplayerClient)return;
        var p=Packet(action);p.Write(point.X);p.Write(point.Y);
        if(action==1){var b=EcologyRegionUI.Bounds;p.Write(b.X);p.Write(b.Y);p.Write(b.Width);p.Write(b.Height);p.Write((byte)EcologyRegionUI.Biome);p.Write(EcologyRegionUI.Ecology);}
        p.Send();
    }
    public static void ReceivePacket(BinaryReader r,int who)
    {
        byte action=r.ReadByte();
        if(action==6)
        {
            int target=r.ReadByte();byte[] bytes=r.ReadBytes(16);if(bytes.Length!=16)return;
            if(Main.netMode==NetmodeID.MultiplayerClient&&target<Main.maxPlayers)Main.player[target].GetModPlayer<EcologyPlayer>().Identity=new Guid(bytes).ToString("N");return;
        }
        if(Main.netMode!=NetmodeID.Server||who<0||who>=Main.maxPlayers)return;
        Player player=Main.player[who];if(!player.active)return;var state=player.GetModPlayer<EcologyPlayer>();
        if(action==0)
        {
            byte[] bytes=r.ReadBytes(16);if(bytes.Length!=16||state.acceptedIdentity)return;
            state.Identity=new Guid(bytes).ToString("N");state.acceptedIdentity=true;state.SyncPlayer(-1,-1,false);return;
        }
        Point point=new(r.ReadInt32(),r.ReadInt32());Rectangle bounds=default;int biome=0;bool ecology=true;
        if(action==1){bounds=new(r.ReadInt32(),r.ReadInt32(),r.ReadInt32(),r.ReadInt32());biome=r.ReadByte();ecology=r.ReadBoolean();}
        // Parse the full payload first; malformed/truncated packets never partly perform edits.
        if(action is <1 or >4||!state.acceptedIdentity||Main.GameUpdateCount-state.lastRequest<8)return;
        state.lastRequest=Main.GameUpdateCount;Perform(player,action,point,bounds,biome,ecology);
    }
    internal static void Perform(Player player,byte action,Point point,Rectangle bounds,int biome,bool ecology)
    {
        if(!EcologyWorld.Editable||!Reach(player,point,action==3?160:960))return;
        if(action==1){Tell(player,EcologyWorld.SetRegion(player,point,bounds,biome,ecology)?"RegionSaved":"RegionRejected");return;}
        if(action==2){Tell(player,EcologyWorld.RemoveRegion(player,point)?"RegionRemoved":"RegionRejected");return;}
        if(action==3){EcologyMineralBeds.Interact(player,point);return;}
        if(action==4&&player.HeldItem.ModItem is EcologyPowder powder&&!powder.Solution&&player.HeldItem.stack>0)
        {
            // The held catalyst is authoritative. The old path reused the
            // biome selected in the region panel, which could be stale or
            // default to biome 0 (especially on a multiplayer server).
            int powderBiome=powder.Restore?-1:powder.Index;
            if(EcologyConversion.Convert(player,point,powderBiome,powder.Restore,3)>0){ConsumeHeld(player);EcologyWorld.Sync();}
            else Tell(player,"NoConversion");
        }
    }
    public static void ConsumeHeld(Player p)
    {
        p.HeldItem.stack--;if(p.HeldItem.stack<=0)p.HeldItem.TurnToAir();
        if(Main.netMode==NetmodeID.Server)NetMessage.SendData(MessageID.SyncEquipment,-1,-1,null,p.whoAmI,p.selectedItem,p.HeldItem.prefix);
    }
}
