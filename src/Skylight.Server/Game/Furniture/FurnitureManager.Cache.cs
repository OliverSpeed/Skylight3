﻿using System.Collections.Immutable;
using Skylight.API.Game.Furniture.Floor;
using Skylight.API.Game.Furniture.Wall;
using Skylight.Domain.Furniture;
using Skylight.Server.Game.Furniture.Floor;
using Skylight.Server.Game.Furniture.Wall;

namespace Skylight.Server.Game.Furniture;

internal partial class FurnitureManager
{
	private sealed class Cache
	{
		internal Dictionary<int, IFloorFurniture> FloorFurnitures { get; }
		internal Dictionary<int, IWallFurniture> WallFurnitures { get; }

		internal Cache(Dictionary<int, IFloorFurniture> floorFurnitures, Dictionary<int, IWallFurniture> wallFurnitures)
		{
			this.FloorFurnitures = floorFurnitures;
			this.WallFurnitures = wallFurnitures;
		}

		internal static Builder CreateBuilder() => new();

		internal sealed class Builder
		{
			private readonly Dictionary<int, FloorFurnitureEntity> floorFurnitures;
			private readonly Dictionary<int, WallFurnitureEntity> wallFurnitures;

			internal Builder()
			{
				this.floorFurnitures = new Dictionary<int, FloorFurnitureEntity>();
				this.wallFurnitures = new Dictionary<int, WallFurnitureEntity>();
			}

			internal void AddFloorItem(FloorFurnitureEntity floorItem)
			{
				this.floorFurnitures.Add(floorItem.Id, floorItem);
			}

			internal void AddWallItem(WallFurnitureEntity wallItem)
			{
				this.wallFurnitures.Add(wallItem.Id, wallItem);
			}

			internal Cache ToImmutable()
			{
				Dictionary<int, IFloorFurniture> floorFurnitures = new();
				Dictionary<int, IWallFurniture> wallFurnitures = new();

				foreach (FloorFurnitureEntity entity in this.floorFurnitures.Values)
				{
					FloorFurniture item = entity.InteractionType switch
					{
						//Todo: Factory
						"sticky_note_pole" => new StickyNotePoleFurniture(entity.Id, entity.Width, entity.Length, entity.Height[0]),
						"furnimatic_gift" => new FurniMaticGiftFurniture(entity.Id, entity.Width, entity.Length, entity.Height[0]),
						"sound_machine" => new SoundMachineFurniture(entity.Id, entity.Width, entity.Length, entity.Height[0]),
						"sound_set" => CreateSoundSet(entity),
						"roller" => new RollerFurniture(entity.Id, entity.Width, entity.Length, entity.Height[0]),

						_ => new BasicFloorFurniture(entity.Id, entity.Width, entity.Length, entity.Height[0])
					};

					floorFurnitures.Add(item.Id, item);

					static SoundSetFurniture CreateSoundSet(FloorFurnitureEntity entity)
					{
						int soundSetId = int.Parse(entity.ClassName.AsSpan(entity.ClassName.LastIndexOf('_') + 1));

						return new SoundSetFurniture(entity.Id, entity.Width, entity.Length, entity.Height[0], soundSetId, Enumerable.Range((soundSetId * 9) - 8, 9).ToImmutableHashSet());
					}
				}

				foreach (WallFurnitureEntity entity in this.wallFurnitures.Values)
				{
					WallFurniture item = entity.InteractionType switch
					{
						//Todo: Factory
						"sticky_note" => new StickyNoteFurniture(entity.Id),

						_ => new BasicWallFurniture(entity.Id)
					};

					wallFurnitures.Add(item.Id, item);
				}

				return new Cache(floorFurnitures, wallFurnitures);
			}
		}
	}
}
