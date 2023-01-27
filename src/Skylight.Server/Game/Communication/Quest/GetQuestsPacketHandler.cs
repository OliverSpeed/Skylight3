using Net.Communication.Attributes;
using Skylight.API.Game.Users;
using Skylight.Protocol.Packets.Data.Quest;
using Skylight.Protocol.Packets.Incoming.Quest;
using Skylight.Protocol.Packets.Manager;
using Skylight.Protocol.Packets.Outgoing.Quest;

namespace Skylight.Server.Game.Communication.Quest;

[PacketManagerRegister(typeof(AbstractGamePacketManager))]
internal sealed class GetQuestsPacketHandler<T> : UserPacketHandler<T>
	where T : IGetQuestsIncomingPacket
{
	internal override void Handle(IUser user, in T packet)
	{
		//user.SendAsync(new QuestCompletedOutgoingPacket(new List<QuestMessageData>(), true));
	}
}
