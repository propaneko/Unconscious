using ProtoBuf;
using Vintagestory.API.Common;

namespace Unconscious.src.Packets
{

    [ProtoContract]
    public class ShowUnconciousScreen
    {
        [ProtoMember(1)]
        public bool shouldShow { get; set; }
        [ProtoMember(2)]
        public int unconsciousTime { get; set; }
    }

    [ProtoContract]
    public class SendUnconsciousPacket
    {
        [ProtoMember(1)]
        public bool isUnconscious { get; set; }
    }

    [ProtoContract]
    public class ShowPlayerFinishOffScreenPacket
    {
        [ProtoMember(1)]
        public bool shouldShow { get; set; }
        [ProtoMember(2)]
        public string attackerPlayerUUID { get; set; }
        [ProtoMember(3)]
        public string victimPlayerUUID { get; set; }
        [ProtoMember(4)]
        public EnumDamageType damageType { get; set; }
        [ProtoMember(5)]
        public int finishTimer { get; set; }
    }

    [ProtoContract]
    public class PlayerKill
    {
        [ProtoMember(1)]
        public string attackerPlayerUUID { get; set; }
        [ProtoMember(2)]
        public string victimPlayerUUID { get; set; }
        [ProtoMember(3)]
        public EnumDamageType damageType { get; set; }
    }

    [ProtoContract]
    public class PlayerDeath
    {

    }
}
