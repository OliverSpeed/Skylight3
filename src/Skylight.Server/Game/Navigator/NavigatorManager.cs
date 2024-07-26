﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Skylight.API.DependencyInjection;
using Skylight.API.Game.Navigator;
using Skylight.API.Game.Rooms;
using Skylight.API.Game.Rooms.Map;
using Skylight.API.Game.Users;
using Skylight.Domain.Navigator;
using Skylight.Domain.Rooms.Layout;
using Skylight.Domain.Rooms.Private;
using Skylight.Domain.Rooms.Public;
using Skylight.Infrastructure;
using Skylight.Server.Collections.Cache;
using Skylight.Server.DependencyInjection;
using Skylight.Server.Game.Rooms;

namespace Skylight.Server.Game.Navigator;

internal sealed partial class NavigatorManager : VersionedLoadableServiceBase<INavigatorSnapshot, NavigatorSnapshot>, INavigatorManager
{
	private readonly IDbContextFactory<SkylightContext> dbContextFactory;

	private readonly TimeProvider timeProvider;

	private readonly IUserManager userManager;

	private readonly AsyncTypedCache<int, IRoomInfo?> roomData;

	private readonly ConcurrentDictionary<int, RoomActivity> roomActivity;
	private readonly Channel<(int RoomId, int Activity)> roomActivityChannel;

	public NavigatorManager(IDbContextFactory<SkylightContext> dbContextFactory, TimeProvider timeProvider, IUserManager userManager)
		: base(NavigatorSnapshot.CreateBuilder().Build())
	{
		this.dbContextFactory = dbContextFactory;

		this.timeProvider = timeProvider;

		this.userManager = userManager;

		this.roomData = new AsyncTypedCache<int, IRoomInfo?>(this.InternalLoadRoomDataAsync);

		this.roomActivity = [];
		this.roomActivityChannel = Channel.CreateUnbounded<ValueTuple<int, int>>(new UnboundedChannelOptions
		{
			AllowSynchronousContinuations = false,
			SingleReader = true
		});

		_ = this.ProcessRoomActivityAsync();
	}

	internal override async Task<VersionedServiceSnapshot.Transaction<NavigatorSnapshot>> LoadAsyncCore(ILoadableServiceContext context, CancellationToken cancellationToken = default)
	{
		NavigatorSnapshot.Builder builder = NavigatorSnapshot.CreateBuilder();

		await using (SkylightContext dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
		{
			await foreach (RoomLayoutEntity layout in dbContext.RoomLayouts
				.AsNoTracking()
				.AsAsyncEnumerable()
				.WithCancellation(cancellationToken)
				.ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				builder.AddLayout(layout);
			}

			await foreach (PublicRoomEntity publicRoom in dbContext.PublicRooms
				.AsNoTracking()
				.AsAsyncEnumerable()
				.WithCancellation(cancellationToken)
				.ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				builder.AddPublicRoom(publicRoom);
			}

			await foreach (NavigatorNodeEntity node in dbContext.NavigatorNodes
				.Include(e => e.Children)
				.AsNoTrackingWithIdentityResolution()
				.AsAsyncEnumerable()
				.WithCancellation(cancellationToken)
				.ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();

				builder.AddFlatCat(node);
			}
		}

		return builder.BuildAndStartTransaction(this, this.Current);
	}

	public ValueTask<IRoomInfo?> GetRoomDataAsync(int id, CancellationToken cancellationToken)
	{
		return this.roomData.GetAsync(id);
	}

	private async Task<IRoomInfo?> InternalLoadRoomDataAsync(int id)
	{
		await using SkylightContext dbContext = await this.dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

		PrivateRoomEntity? entity = await dbContext.PrivateRooms.FirstOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
		if (entity is null)
		{
			return null;
		}

		if (!this.TryGetLayout(entity.LayoutId, out IRoomLayout? layout))
		{
			throw new InvalidOperationException($"Missing room layout data for {entity.LayoutId}");
		}

		IUserInfo? owner = await this.userManager.GetUserInfoAsync(entity.OwnerId).ConfigureAwait(false);

		return new RoomData(entity, owner!, layout);
	}

	public void PushRoomActivity(int roomId, int activity)
	{
		this.roomActivityChannel.Writer.WriteAsync((roomId, activity));
	}

	private async Task ProcessRoomActivityAsync()
	{
		PeriodicTimer timer = new(TimeSpan.FromSeconds(30));

		List<PrivateRoomActivityEntity> entities = [];
		while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
		{
			DateTimeOffset time = this.timeProvider.GetUtcNow();

			(int week, int day) = RoomActivity.GetDateParts(time);

			while (this.roomActivityChannel.Reader.TryRead(out (int RoomId, int Activity) value))
			{
				PrivateRoomActivityEntity entity = new()
				{
					RoomId = value.RoomId,
					Week = week
				};

				switch (day)
				{
					case 0:
						entity.Monday = value.Activity;
						break;
					case 1:
						entity.Tuesday = value.Activity;
						break;
					case 2:
						entity.Wednesday = value.Activity;
						break;
					case 3:
						entity.Thursday = value.Activity;
						break;
					case 4:
						entity.Friday = value.Activity;
						break;
					case 5:
						entity.Saturday = value.Activity;
						break;
					case 6:
						entity.Sunday = value.Activity;
						break;
				}

				entities.Add(entity);
			}

			if (entities.Count > 0)
			{
				await using SkylightContext skylightContext = await this.dbContextFactory.CreateDbContextAsync().ConfigureAwait(false);

				await skylightContext.PrivateRoomActivity
					.UpsertRange(entities)
					.On(e => new { e.RoomId, e.Week })
					.WhenMatched((oldValue, newValue) => new PrivateRoomActivityEntity
					{
						Monday = oldValue.Monday + newValue.Monday,
						Tuesday = oldValue.Tuesday + newValue.Tuesday,
						Wednesday = oldValue.Wednesday + newValue.Wednesday,
						Thursday = oldValue.Thursday + newValue.Thursday,
						Friday = oldValue.Friday + newValue.Friday,
						Saturday = oldValue.Saturday + newValue.Saturday,
						Sunday = oldValue.Sunday + newValue.Sunday
					}).RunAsync().ConfigureAwait(false);

				foreach (PrivateRoomActivityEntity entity in entities)
				{
					if (this.roomActivity.TryGetValue(entity.RoomId, out RoomActivity? roomActivity))
					{
						int activity = day switch
						{
							0 => entity.Monday,
							1 => entity.Tuesday,
							2 => entity.Wednesday,
							3 => entity.Thursday,
							4 => entity.Friday,
							5 => entity.Saturday,
							6 => entity.Sunday,

							_ => throw new UnreachableException()
						};

						roomActivity.Update(week, day, activity);
					}
					else
					{
						IAsyncEnumerable<PrivateRoomActivityEntity> query = skylightContext.PrivateRoomActivity
							.Where(e => e.RoomId == entity.RoomId)
							.OrderBy(e => e.Week)
							.AsAsyncEnumerable();

						this.roomActivity[entity.RoomId] = await RoomActivity.LoadAsync(week, day, query).ConfigureAwait(false);
					}
				}

				entities.Clear();
			}
		}
	}
}
