﻿using Net.Communication.Attributes;
using Skylight.API.Game.Users;
using Skylight.Protocol.Packets.Incoming.Catalog;
using Skylight.Protocol.Packets.Manager;

namespace Skylight.Server.Game.Communication.Catalog;

[PacketManagerRegister(typeof(AbstractGamePacketManager))]
internal sealed class GetBonusRareInfoPacketHandler<T> : UserPacketHandler<T>
	where T : IGetBonusRareInfoIncomingPacket
{
	internal override void Handle(IUser user, in T packet)
	{
	}
}
