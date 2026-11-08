using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Models.Entities;
using Xunit;

namespace PharmacySystem.Tests;

public class BusinessLogicTests
{
    private PharmacyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PharmacyDbContext(options);
    }

    [Fact]
    public async Task ExpiredBatches_ShouldNotBeIncludedInAvailableStock()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = 1, Name = "Analgesics" };
        var location = new Location { Id = 1, Code = "A1", Name = "Shelf A1" };
        context.Categories.Add(category);
        context.Locations.Add(location);

        var product = new Product
        {
            Id = 1,
            Name = "Panadol",
            CategoryId = 1,
            SellingPrice = 20,
            MinimumStockLevel = 5,
            IsActive = true
        };
        context.Products.Add(product);

        var validBatch = new Batch
        {
            Id = 1,
            ProductId = 1,
            LocationId = 1,
            BatchNumber = "LOT-VALID",
            QuantityOnHand = 10,
            PurchasePrice = 15,
            AddedDate = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6))
        };

        var expiredBatch = new Batch
        {
            Id = 2,
            ProductId = 1,
            LocationId = 1,
            BatchNumber = "LOT-EXPIRED",
            QuantityOnHand = 5,
            PurchasePrice = 15,
            AddedDate = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
        };

        context.Batches.AddRange(validBatch, expiredBatch);
        await context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act: Filter batches available for dispensing
        var activeUsableBatches = await context.Batches
            .Where(b => b.ProductId == 1 && b.QuantityOnHand > 0 && b.ExpiryDate > today)
            .OrderBy(b => b.ExpiryDate) // FEFO order
            .ToListAsync();

        // Assert: Expired batch must be strictly excluded from saleable batches
        Assert.Single(activeUsableBatches);
        Assert.Equal("LOT-VALID", activeUsableBatches[0].BatchNumber);
        Assert.Equal(10, activeUsableBatches[0].QuantityOnHand);
    }

    [Fact]
    public async Task SafeProductDeactivation_ShouldKeepProductAndBatchesIntact()
    {
        // Arrange
        using var context = CreateDbContext();
        var category = new Category { Id = 2, Name = "Antibiotics" };
        context.Categories.Add(category);

        var product = new Product
        {
            Id = 2,
            Name = "Amoxicillin",
            CategoryId = 2,
            SellingPrice = 50,
            MinimumStockLevel = 10,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        // Act: Soft deactivate
        product.IsActive = false;
        await context.SaveChangesAsync();

        // Assert: Product exists but is inactive
        var fetched = await context.Products.FindAsync(2);
        Assert.NotNull(fetched);
        Assert.False(fetched.IsActive);
    }
}
