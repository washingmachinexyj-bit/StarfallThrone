using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace StarfallThrone.Content.Ascendant.Equipment;

// Metadata only. NPC-fired hostile shots often use Main.myPlayer as owner in single player;
// excluding all owner==player shots would incorrectly disable challenge rewards against bosses.
public sealed class AscendantHostileOrigin:GlobalProjectile
{
    public override bool InstancePerEntity=>true;
    public bool FromEnemy;
    public override void OnSpawn(Projectile projectile,IEntitySource source)
    {
        FromEnemy=source is EntitySource_Parent{Entity:NPC npc}&&!npc.friendly;
        if(source is EntitySource_Parent{Entity:Projectile parent})FromEnemy=parent.GetGlobalProjectile<AscendantHostileOrigin>().FromEnemy;
    }
    public override void SendExtraAI(Projectile projectile,BitWriter bitWriter,BinaryWriter binaryWriter)=>bitWriter.WriteBit(FromEnemy);
    public override void ReceiveExtraAI(Projectile projectile,BitReader bitReader,BinaryReader binaryReader)=>FromEnemy=bitReader.ReadBit();
}
