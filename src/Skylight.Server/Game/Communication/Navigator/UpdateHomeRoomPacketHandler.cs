﻿using Microsoft.EntityFrameworkCore;
using Net.Communication.Attributes;
using Skylight.API.Game.Clients;
using Skylight.API.Game.Users;
using Skylight.Domain.Users;
using Skylight.Infrastructure;
using Skylight.Protocol.Packets.Incoming.Navigator;
using Skylight.Protocol.Packets.Manager;
using Skylight.Protocol.Packets.Outgoing.Navigator;

namespace Skylight.Server.Game.Communication.Navigator;

[PacketManagerRegister(typeof(AbstractGamePacketManager))]
internal sealed class UpdateHomeRoomPacketHandler<T> : UserPacketHandler<T>
	where T : IUpdateHomeRoomIncomingPacket
{
	private readonly IDbContextFactory<SkylightContext> dbContextFactory;

	public UpdateHomeRoomPacketHandler(IDbContextFactory<SkylightContext> dbContextFactory)
	{
		this.dbContextFactory = dbContextFactory;
	}

	internal override void Handle(IUser user, in T packet)
	{
		if (user.RoomSession?.Unit is not { } roomUnit)
		{
			return;
		}

		if (packet.RoomId < 0 || packet.RoomId == user.Settings.HomeRoomId)
		{
			return;
		}

		user.Client.ScheduleTask(new UpdateHomeRoomTask
		{
			DbContextFactory = this.dbContextFactory,

			HomeRoomId = packet.RoomId
		});
	}

	private readonly struct UpdateHomeRoomTask : IClientTask
	{
		internal readonly IDbContextFactory<SkylightContext> DbContextFactory { get; init; }

		internal readonly int HomeRoomId { get; init; }

		public async Task ExecuteAsync(IClient client)
		{
			await using SkylightContext dbContext = await this.DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

			await dbContext.UserSettings.Upsert(new UserSettingsEntity
			{
				UserId = client.User!.Profile.Id,
				HomeRoomId = this.HomeRoomId,
			})
			.On(c => c.UserId)
			.WhenMatched((_, c) => new UserSettingsEntity
			{
				HomeRoomId = c.HomeRoomId,
			}).RunAsync().ConfigureAwait(false);

			client.User.Settings.HomeRoomId = this.HomeRoomId;

			client.SendAsync(new NavigatorSettingsOutgoingPacket(client.User.Settings.HomeRoomId, client.User.Settings.HomeRoomId));

			await dbContext.SaveChangesAsync().ConfigureAwait(false);
		}
	}
}
