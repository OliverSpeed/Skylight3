﻿using System.Diagnostics.CodeAnalysis;
using Skylight.API.Game.Furniture;
using Skylight.API.Game.Rooms;
using Skylight.API.Game.Rooms.Items;
using Skylight.Server.Game.Furniture;

namespace Skylight.Server.Game.Rooms.Items.Builders;

internal abstract class RoomItemBuilder<TFurniture, TTarget, TBuilder> : FurnitureItemBuilder<TFurniture, TTarget, TBuilder>, IRoomItemBuilder<TFurniture, TTarget, TBuilder>
	where TFurniture : IFurniture
	where TTarget : IRoomItem, IFurnitureItem<TFurniture>
	where TBuilder : RoomItemBuilder<TFurniture, TTarget, TBuilder>
{
	protected IRoom? RoomValue { get; set; }

	public TBuilder Room(IRoom room)
	{
		this.RoomValue = room;

		return (TBuilder)this;
	}

	[MemberNotNull(nameof(this.RoomValue))]
	protected override void CheckValid()
	{
		base.CheckValid();

		ArgumentNullException.ThrowIfNull(this.RoomValue);
	}
}
