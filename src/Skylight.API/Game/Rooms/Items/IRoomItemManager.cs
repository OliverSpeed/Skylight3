﻿using System.Diagnostics.CodeAnalysis;
using Skylight.API.Game.Furniture.Floor;
using Skylight.API.Game.Furniture.Wall;
using Skylight.API.Game.Rooms.Items.Floor;
using Skylight.API.Game.Rooms.Items.Interactions;
using Skylight.API.Game.Rooms.Items.Wall;
using Skylight.API.Game.Users;
using Skylight.API.Numerics;

namespace Skylight.API.Game.Rooms.Items;

public interface IRoomItemManager
{
	public IEnumerable<IFloorRoomItem> FloorItems { get; }
	public IEnumerable<IWallRoomItem> WallItems { get; }

	public Task LoadAsync(CancellationToken cancellationToken = default);

	public bool CanPlaceItem(IFloorFurniture floorFurniture, Point3D position, IUser? source = null);
	public bool CanPlaceItem(IWallFurniture wallFurniture, Point2D location, Point2D position, IUser? source = null);

	public void AddItem(IFloorRoomItem floorItem);
	public void AddItem(IWallRoomItem wallItem);

	public void MoveItem(IFloorRoomItem floorItem, Point3D position) => this.MoveItem(floorItem, position, floorItem.Direction);
	public void MoveItem(IFloorRoomItem floorItem, Point3D position, int direction);

	public void RemoveItem(IFloorRoomItem floorItem);
	public void RemoveItem(IWallRoomItem wallItem);

	public double GetPlacementHeight(IFloorFurniture furniture, Point2D location);

	public bool TryGetFloorItem(int itemId, [NotNullWhen(true)] out IFloorRoomItem? item);
	public bool TryGetWallItem(int itemId, [NotNullWhen(true)] out IWallRoomItem? item);

	public bool TryGetInteractionHandler<T>([NotNullWhen(true)] out T? handler)
		where T : IRoomItemInteractionHandler;
}
