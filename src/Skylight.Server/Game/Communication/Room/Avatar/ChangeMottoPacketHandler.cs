﻿using System.Text;
using Microsoft.EntityFrameworkCore;
using Net.Communication.Attributes;
using Skylight.API.Game.Clients;
using Skylight.API.Game.Rooms;
using Skylight.API.Game.Users;
using Skylight.Domain.Users;
using Skylight.Infrastructure;
using Skylight.Protocol.Packets.Incoming.Room.Avatar;
using Skylight.Protocol.Packets.Manager;
using Skylight.Protocol.Packets.Outgoing.Room.Engine;

namespace Skylight.Server.Game.Communication.Room.Avatar;

[PacketManagerRegister(typeof(AbstractGamePacketManager))]
internal sealed class ChangeMottoPacketHandler<T> : UserPacketHandler<T>
	where T : IChangeMottoIncomingPacket
{
	private readonly IDbContextFactory<SkylightContext> dbContextFactory;

	public ChangeMottoPacketHandler(IDbContextFactory<SkylightContext> dbContextFactory)
	{
		this.dbContextFactory = dbContextFactory;
	}

	internal override void Handle(IUser user, in T packet)
	{
		if (user.RoomSession?.Unit is not { } roomUnit)
		{
			return;
		}

		string motto = Encoding.UTF8.GetString(packet.Motto);
		if (motto.Length > 38 || motto == user.Profile.Motto)
		{
			return;
		}

		user.Client.ScheduleTask(new ChangeMottoTask
		{
			DbContextFactory = this.dbContextFactory,

			Motto = motto,

			Room = roomUnit.Room
		});
	}

	private readonly struct ChangeMottoTask : IClientTask
	{
		internal readonly IDbContextFactory<SkylightContext> DbContextFactory { get; init; }

		internal readonly string Motto { get; init; }

		internal readonly IRoom Room { get; init; }

		public async Task ExecuteAsync(IClient client)
		{
			client.User!.Profile.Motto = this.Motto;

			await using SkylightContext dbContext = await this.DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

			UserEntity entity = new()
			{
				Id = client.User.Profile.Id
			};

			dbContext.Users.Attach(entity);

			entity.Motto = this.Motto;

			await dbContext.SaveChangesAsync().ConfigureAwait(false);

			_ = ((Rooms.Room)this.Room).SendAsync(new UserChangeOutgoingPacket(client.User.Profile.Id, client.User.Profile.Figure, client.User.Profile.Gender, client.User.Profile.Motto, 666));
		}
	}
}
