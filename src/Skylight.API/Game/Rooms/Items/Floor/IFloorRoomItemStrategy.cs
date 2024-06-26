﻿using System.Text.Json;
using Skylight.API.Game.Furniture;
using Skylight.API.Game.Furniture.Floor;
using Skylight.API.Game.Inventory.Items.Floor;
using Skylight.API.Game.Users;
using Skylight.API.Numerics;

namespace Skylight.API.Game.Rooms.Items.Floor;

public interface IFloorRoomItemStrategy
{
	public IFloorRoomItem CreateFloorItem(int itemId, IRoom room, IUserInfo owner, IFloorFurniture furniture, Point3D position, int direction, JsonDocument? extraData = null)
		=> this.CreateFloorItem<IFloorFurniture, IFloorRoomItem>(itemId, room, owner, furniture, position, direction, extraData);

	public TRoomItem CreateFloorItem<TFurniture, TRoomItem>(int itemId, IRoom room, IUserInfo owner, TFurniture furniture, Point3D position, int direction, JsonDocument? extraData = null)
		where TFurniture : IFloorFurniture
		where TRoomItem : IFloorRoomItem, IFurnitureItem<TFurniture>;

	public TRoomItem CreateFloorItem<TFurniture, TRoomItem, TBuilder>(int itemId, IRoom room, IUserInfo owner, TFurniture furniture, Point3D position, int direction, Func<TBuilder, IFurnitureItemDataBuilder<TFurniture, TRoomItem, TBuilder>> builder)
		where TFurniture : IFloorFurniture
		where TRoomItem : IFloorRoomItem, IFurnitureItem<TFurniture>
		where TBuilder : IFurnitureItemDataBuilder<TFurniture, TRoomItem, TBuilder>;

	public IFloorRoomItem CreateFloorItem(IFloorInventoryItem item, IRoom room, Point3D position, int direction, JsonDocument? extraData = null)
		=> this.CreateFloorItem<IFloorFurniture, IFloorRoomItem>(item.Id, room, item.Owner, item.Furniture, position, direction, extraData);

	public TRoomItem CreateFloorItem<TInventoryItem, TFurniture, TRoomItem>(TInventoryItem item, IRoom room, Point3D position, int direction, JsonDocument? extraData = null)
		where TInventoryItem : IFloorInventoryItem, IFurnitureItem<TFurniture>
		where TFurniture : IFloorFurniture
		where TRoomItem : IFloorRoomItem, IFurnitureItem<TFurniture> => this.CreateFloorItem<TFurniture, TRoomItem>(item.Id, room, item.Owner, ((IFurnitureItem<TFurniture>)item).Furniture, position, direction, extraData);

	public TRoomItem CreateFloorItem<TInventoryItem, TFurniture, TRoomItem, TBuilder>(TInventoryItem item, IRoom room, Point3D position, int direction, Func<TBuilder, IFurnitureItemDataBuilder<TFurniture, TRoomItem, TBuilder>> builder)
		where TInventoryItem : IFloorInventoryItem, IFurnitureItem<TFurniture>
		where TFurniture : IFloorFurniture
		where TRoomItem : IFloorRoomItem, IFurnitureItem<TFurniture>
		where TBuilder : IFurnitureItemDataBuilder<TFurniture, TRoomItem, TBuilder> => this.CreateFloorItem(item.Id, room, item.Owner, ((IFurnitureItem<TFurniture>)item).Furniture, position, direction, builder);
}
