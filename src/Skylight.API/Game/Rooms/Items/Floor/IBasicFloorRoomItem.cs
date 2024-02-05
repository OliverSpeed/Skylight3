﻿using Skylight.API.Game.Furniture;
using Skylight.API.Game.Furniture.Floor;

namespace Skylight.API.Game.Rooms.Items.Floor;

public interface IBasicFloorRoomItem : IFloorRoomItem, IFurnitureItem<IBasicFloorFurniture>
{
	public new IBasicFloorFurniture Furniture { get; }

	IFloorFurniture IFloorRoomItem.Furniture => this.Furniture;
	IBasicFloorFurniture IFurnitureItem<IBasicFloorFurniture>.Furniture => this.Furniture;
}
