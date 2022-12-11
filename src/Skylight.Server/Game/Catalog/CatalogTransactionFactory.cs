﻿using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Skylight.API.Game.Catalog;
using Skylight.API.Game.Furniture;
using Skylight.API.Game.Inventory.Items;
using Skylight.API.Game.Users;
using Skylight.Infrastructure;

namespace Skylight.Server.Game.Catalog;

internal sealed class CatalogTransactionFactory : ICatalogTransactionFactory
{
	private readonly IDbContextFactory<SkylightContext> dbContextFactory;

	private readonly IFurnitureInventoryItemStrategy furnitureInventoryItemStrategy;

	public CatalogTransactionFactory(IDbContextFactory<SkylightContext> dbContextFactory, IFurnitureInventoryItemStrategy furnitureInventoryItemStrategy)
	{
		this.dbContextFactory = dbContextFactory;

		this.furnitureInventoryItemStrategy = furnitureInventoryItemStrategy;
	}

	public async Task<ICatalogTransaction> CreateTransactionAsync(IFurnitureSnapshot furniture, IUser user, string extraData, CancellationToken cancellationToken)
	{
		SkylightContext dbContext = await this.dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return await this.CreateTransactionAsync(furniture, dbContext, user, extraData, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await dbContext.DisposeAsync().ConfigureAwait(false);

			throw;
		}
	}

	public async Task<ICatalogTransaction> CreateTransactionAsync(IFurnitureSnapshot furniture, DbConnection connection, IUser user, string extraData, CancellationToken cancellationToken)
	{
		SkylightContext dbContext = new(new DbContextOptionsBuilder<SkylightContext>().UseNpgsql(connection).Options);

		try
		{
			return await this.CreateTransactionAsync(furniture, dbContext, user, extraData, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await dbContext.DisposeAsync().ConfigureAwait(false);

			throw;
		}
	}

	private async Task<ICatalogTransaction> CreateTransactionAsync(IFurnitureSnapshot furniture, SkylightContext dbContext, IUser user, string extraData, CancellationToken cancellationToken)
	{
		IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			return new CatalogTransaction(furniture, this.furnitureInventoryItemStrategy, dbContext, transaction, user, extraData);
		}
		catch
		{
			await transaction.DisposeAsync().ConfigureAwait(false);

			throw;
		}
	}
}
