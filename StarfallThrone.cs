using Terraria.ModLoader;
using System.IO;
using Terraria;
using Terraria.ID;
using StarfallThrone.Content.Items;
using StarfallThrone.Content.NPCs;

namespace StarfallThrone;

public sealed class StarfallThrone : Mod
{
    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        if (reader.BaseStream.Position >= reader.BaseStream.Length) return;
        try
        {
        byte message = reader.ReadByte();
        if (message == 1)
        {
            int index = reader.ReadByte();
            if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers)
                MiniSummon.TrySummon(Main.player[whoAmI], index);
        }
        else if (message == 2)
        {
            int npcIndex = reader.ReadInt16();
            if (Main.netMode == NetmodeID.Server && whoAmI >= 0 && whoAmI < Main.maxPlayers)
                MiniBossNPC.TryWake(Main.player[whoAmI], npcIndex);
        }
        else if (message == 20)
            Content.Voyage.VoyageSummonBase.ReceivePacket(reader, whoAmI);
        else if (message == 21)
            Content.Voyage.Equipment.VoyageEquipmentPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 22)
            Content.Voyage.Utility.VoyageUtilityPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 30)
            Content.Mining.MiningWorld.ReceivePacket(reader, whoAmI);
        else if (message == 31)
            Content.Mining.MiningPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 40)
            Content.Divine.DivineChallengePlayer.ReceivePacket(reader, whoAmI);
        else if (message == 41)
            Content.Divine.Equipment.DivineEquipmentPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 50)
            Content.Ecology.EcologyPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 51)
            Content.Ecology.Combat.EcologySummonBase.ReceivePacket(reader, whoAmI);
        else if (message == 60)
            Content.Ascendant.AscendantChallengePlayer.ReceivePacket(reader, whoAmI);
        else if (message == 61)
            Content.Ascendant.Equipment.AscendantEquipmentPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 70)
            Content.Pantheon.PantheonSummonBase.ReceivePacket(reader, whoAmI);
        else if (message == 71)
            Content.Pantheon.Equipment.PantheonEquipmentPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 80)
            Content.Fable.FableWorld.ReceivePacket(reader, whoAmI);
        else if (message == 90)
            Content.Oaths.OathWorld.ReceivePacket(reader, whoAmI);
        else if (message == 92)
            Content.Oaths.Support.SupremeSummonBase.ReceivePacket(reader, whoAmI);
        else if (message == 93)
            Content.Oaths.Equipment.OathEquipmentPlayer.ReceivePacket(reader, whoAmI);
        else if (message == 94)
            Content.Fable.Trials.FourTrialWorld.ReceivePacket(reader, whoAmI);
        }
        catch (EndOfStreamException)
        {
            // Truncated network input is not an instruction to partially perform an action.
        }
    }
}
