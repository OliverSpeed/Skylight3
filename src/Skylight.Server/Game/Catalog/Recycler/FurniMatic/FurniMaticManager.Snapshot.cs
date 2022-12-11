﻿using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Skylight.API.Game.Catalog;
using Skylight.API.Game.Furniture;
using Skylight.API.Game.Furniture.Floor;
using Skylight.API.Game.Furniture.Wall;
using Skylight.API.Game.Inventory.Items;
using Skylight.API.Game.Inventory.Items.Floor;
using Skylight.API.Game.Inventory.Items.Wall;
using Skylight.API.Game.Recycler.FurniMatic;
using Skylight.API.Game.Rooms.Items.Floor;
using Skylight.API.Game.Users;
using Skylight.Domain.Items;
using Skylight.Domain.Recycler.FurniMatic;
using Skylight.Infrastructure;

namespace Skylight.Server.Game.Catalog.Recycler.FurniMatic;

internal partial class FurniMaticManager
{
	public IFurniMaticPrizes Prizes => this.Current.Prizes;

	public int ItemsRequiredToRecycle => this.Current.ItemsRequiredToRecycle;

	public Task<IFurniMaticPrize?> RecycleAsync(IUser user, IEnumerable<IFurnitureInventoryItem> items, CancellationToken cancellationToken) => this.Current.RecycleAsync(user, items, cancellationToken);
	public Task<IFurniMaticPrize?> OpenGiftAsync(IUser user, IFurniMaticGiftRoomItem gift, CancellationToken cancellationToken) => this.Current.OpenGiftAsync(user, gift, cancellationToken);

	private sealed class Snapshot : IFurniMaticSnapshot
	{
		private readonly FurniMaticManager manager;
		private readonly Cache cache;

		internal Snapshot(FurniMaticManager manager, Cache cache)
		{
			this.manager = manager;
			this.cache = cache;
		}

		public IFurniMaticPrizes Prizes => this.cache.Prizes;

		public int ItemsRequiredToRecycle => this.manager.settings.ItemsRequired;

		internal (IFloorFurniture? GiftFurniture, IFurniMaticPrize? Prize) RollRandomPrice() => (this.cache.GiftFurniture, this.cache.Prizes.RollRandomPrice());

		public async Task<IFurniMaticPrize?> RecycleAsync(IUser user, IEnumerable<IFurnitureInventoryItem> items, CancellationToken cancellationToken)
		{
			(IFloorFurniture? giftFurniture, IFurniMaticPrize? prize) = this.RollRandomPrice();
			if (giftFurniture is null || prize is null)
			{
				return null;
			}

			//TODO: Catalog transaction?
			FloorItemEntity giftItemEntity = new()
			{
				UserId = user.Profile.Id,
				FurnitureId = giftFurniture.Id,
				ExtraData = JsonSerializer.SerializeToDocument(DateTimeOffset.Now)
			};

			try
			{
				await using SkylightContext dbContext = await this.manager.dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

				foreach (IFurnitureInventoryItem item in items)
				{
					if (!user.Inventory.TryRemoveFurniture(item))
					{
						user.Inventory.RefreshFurniture();

						return null;
					}

					if (item is IFloorInventoryItem floorItem)
					{
						dbContext.FloorItems.Remove(new FloorItemEntity
						{
							Id = floorItem.Id,
							UserId = user.Profile.Id
						});
					}
					else if (item is IWallInventoryItem wallItem)
					{
						dbContext.WallItems.Remove(new WallItemEntity
						{
							Id = wallItem.Id,
							UserId = user.Profile.Id
						});
					}
					else
					{
						throw new NotSupportedException($"Unknown furniture type {item.GetType()}");
					}
				}

				dbContext.FurniMaticGifts.Add(new FurniMaticGiftEntity
				{
					Item = giftItemEntity,
					PrizeId = prize.Id
				});

				await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (DbUpdateConcurrencyException)
			{
				user.Inventory.RefreshFurniture();

				return null;
			}

			user.Inventory.AddUnseenFloorItem(this.manager.furnitureInventoryItemStrategy.CreateFurnitureItem(giftItemEntity.Id, user.Profile, giftFurniture, giftItemEntity.ExtraData));

			return prize;
		}

		public async Task<IFurniMaticPrize?> OpenGiftAsync(IUser user, IFurniMaticGiftRoomItem gift, CancellationToken cancellationToken)
		{
			await using SkylightContext dbContext = await this.manager.dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

			FurniMaticGiftEntity? giftEntity = await dbContext.FurniMaticGifts.FirstOrDefaultAsync(i => i.ItemId == gift.Id, cancellationToken).ConfigureAwait(false);
			if (giftEntity is null || !this.cache.Prizes.TryGetPrize(giftEntity.PrizeId, out IFurniMaticPrize? prize))
			{
				return null;
			}

			await using ICatalogTransaction transaction = await this.manager.catalogTransactionFactory.CreateTransactionAsync(this.manager.furnitureManager, dbContext.Database.GetDbConnection(), user, string.Empty, cancellationToken).ConfigureAwait(false);

			await using (await dbContext.Database.UseTransactionAsync(transaction.Transaction, cancellationToken).ConfigureAwait(false))
			{
				dbContext.FloorItems.Remove(new FloorItemEntity
				{
					Id = gift.Id,
					UserId = gift.Owner.Id,
					RoomId = gift.Room.Info.Id
				});

				await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
			}

			foreach (IFurniture prizeFurniture in prize.Furnitures)
			{
				//TODO: Maybe make more general "ICatalogProduct" or just use it as impl detail
				if (prizeFurniture is IBasicFloorFurniture floorFurniture)
				{
					transaction.AddFloorItem(floorFurniture, null);
				}
				else if (prizeFurniture is IBasicWallFurniture wallFurniture)
				{
					transaction.AddWallItem(wallFurniture, null);
				}
				else
				{
					throw new NotSupportedException();
				}
			}

			await transaction.CompleteAsync(cancellationToken).ConfigureAwait(false);

			return prize;
		}
	}
}
