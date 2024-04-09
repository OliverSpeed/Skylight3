﻿using System.Text;
using Net.Communication.Attributes;
using Skylight.API.Game.Rooms.Items.Floor;
using Skylight.API.Game.Rooms.Items.Floor.Wired.Triggers;
using Skylight.API.Game.Users;
using Skylight.Protocol.Packets.Incoming.UserDefinedRoomEvents;
using Skylight.Protocol.Packets.Manager;
using Skylight.Protocol.Packets.Outgoing.UserDefinedRoomEvents;

namespace Skylight.Server.Game.Communication.UserDefinedRoomEvents;

[PacketManagerRegister(typeof(AbstractGamePacketManager))]
internal sealed class UpdateTriggerPacketHandler<T> : UserPacketHandler<T>
	where T : IUpdateTriggerIncomingPacket
{
	internal override void Handle(IUser user, in T packet)
	{
		if (user.RoomSession?.Unit is not { } roomUnit)
		{
			return;
		}

		int itemId = packet.ItemId;

		IList<int> selectedItemIds = packet.SelectedItems;
		IList<int> integerParameters = packet.IntegerParameters;
		string stringParameter = Encoding.UTF8.GetString(packet.StringParameter);

		roomUnit.Room.PostTask(room =>
		{
			if (!roomUnit.InRoom || !room.ItemManager.TryGetFloorItem(itemId, out IFloorRoomItem? item) || item is not IWiredTriggerRoomItem trigger)
			{
				return;
			}

			List<IFloorRoomItem> selectedItems = [];
			foreach (int selectedItemId in selectedItemIds)
			{
				if (!room.ItemManager.TryGetFloorItem(selectedItemId, out IFloorRoomItem? selectedItem))
				{
					continue;
				}

				selectedItems.Add(selectedItem);
			}

			if (trigger is IUserSayTriggerRoomItem userSay)
			{
				userSay.Message = stringParameter;
			}

			user.SendAsync(new WiredSaveSuccessOutgoingPacket());
		});
	}
}
