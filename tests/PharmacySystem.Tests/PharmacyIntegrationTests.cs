using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Exceptions;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.Packaging;
using PharmacySystem.Services;
using PharmacySystem.Services.Packaging;
using Xunit;

namespace PharmacySystem.Tests;

public class PharmacyIntegrationTests
{
    private PharmacyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PharmacyDbContext(options);
    }

    [Fact]
    public async Task PackagingService_ParsesAndCalculatesBaseUnitsCorrectly()
    {
        var packagingService = new ProductPackagingService();

        var units = new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "Strip", ConversionFactor = 10, SellingPrice = 25m, IsBaseUnit = false },
            new ProductUnitDto { UnitName = "Box", ConversionFactor = 30, SellingPrice = 70m, IsBaseUnit = false }
        };

        var config = packagingService.BuildPackagingConfig("Tablet", 3m, "123456", units);
        var serializedDesc = packagingService.EmbedPackaging("Original Description", config);

        Assert.Contains("[PACKAGING:", serializedDesc);

        var retrievedConfig = packagingService.GetPackagingConfig(serializedDesc, 3m, "123456");
        Assert.Equal("Tablet", retrievedConfig.BaseUnitName);
        Assert.Equal(3, retrievedConfig.PermittedUnits.Count);

        // Calculate: 2 strips of factor 10 = 20 base units
        var (baseUnits, unitPrice, factor) = packagingService.CalculateAllocation(retrievedConfig, "Strip", 2, fallbackPrice: 25m);
        Assert.Equal(20, baseUnits);
        Assert.Equal(25m, unitPrice);
        Assert.Equal(10, factor);
    }

    [Fact]
    public async Task SaleTransactionService_FefoAllocatesBaseUnitsForPackagingUnits()
    {
        using var db = CreateDbContext();
        var packagingService = new ProductPackagingService();
        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(db, fefoService, packagingService);

        // Setup Pharmacy & User
        var pharmacy = new Pharmacy { Id = 1, Name = "Main Pharmacy", IsActive = true, CreatedAt = DateTime.UtcNow };
        var user = new User { Id = 1, FullName = "Dr. Pharmacist", Email = "rx@pharmacy.com", PasswordHash = "hash", PharmacyId = 1, Role = UserRole.Pharmacist, IsActive = true, CreatedAt = DateTime.UtcNow };
        var category = new Category { Id = 1, Name = "Tablets", IsActive = true };
        var location = new Location { Id = 1, Code = "LOC-A", Name = "Shelf A", IsActive = true };

        db.Pharmacies.Add(pharmacy);
        db.Users.Add(user);
        db.Categories.Add(category);
        db.Locations.Add(location);

        // Product with 2 strips per box or 10 tablets per strip
        var units = new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "Strip", ConversionFactor = 10, SellingPrice = 20m, IsBaseUnit = false }
        };
        var packagingConfig = packagingService.BuildPackagingConfig("Tablet", 2.5m, "BAR-100", units);

        var product = new Product
        {
            Id = 1,
            Name = "Aspirin 100mg",
            CategoryId = 1,
            SellingPrice = 2.5m,
            MinimumStockLevel = 10,
            IsActive = true,
            Description = packagingService.EmbedPackaging("Pain relief", packagingConfig)
        };
        db.Products.Add(product);

        // Two batches: Batch 1 has 15 tablets expiring in 3 months; Batch 2 has 50 tablets expiring in 6 months
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var batch1 = new Batch
        {
            Id = 1,
            ProductId = 1,
            LocationId = 1,
            BatchNumber = "LOT-EARLY",
            QuantityOnHand = 15, // 15 base tablets
            PurchasePrice = 1.0m,
            ExpiryDate = today.AddMonths(3),
            AddedDate = DateTime.UtcNow
        };
        var batch2 = new Batch
        {
            Id = 2,
            ProductId = 1,
            LocationId = 1,
            BatchNumber = "LOT-LATER",
            QuantityOnHand = 50, // 50 base tablets
            PurchasePrice = 1.0m,
            ExpiryDate = today.AddMonths(6),
            AddedDate = DateTime.UtcNow
        };
        db.Batches.AddRange(batch1, batch2);
        await db.SaveChangesAsync();

        // Act: Sell 2 Strips (factor = 10 -> total 20 base tablets)
        // Batch 1 has 15 -> completely depleted (0 left)
        // Batch 2 has 50 -> 5 depleted (45 left)
        var request = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-TEST-001",
            PaymentMethod: PaymentMethod.Cash,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(
                    ProductId: 1,
                    Quantity: 2,
                    UnitName: "Strip",
                    ConversionFactor: 10,
                    UnitPrice: 20m
                )
            }
        );

        var sale = await saleService.ProcessSaleAsync(request);

        // Assert
        Assert.NotNull(sale);
        Assert.Equal(40m, sale.TotalAmount); // 2 strips * 20 EGP = 40 EGP

        var updatedBatch1 = await db.Batches.FindAsync(1);
        var updatedBatch2 = await db.Batches.FindAsync(2);

        Assert.Equal(0, updatedBatch1!.QuantityOnHand);
        Assert.Equal(45, updatedBatch2!.QuantityOnHand);
    }

    [Fact]
    public async Task SaleTransactionService_SplitPayment_ValidatesSumEquality()
    {
        using var db = CreateDbContext();
        var packagingService = new ProductPackagingService();
        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(db, fefoService, packagingService);

        var pharmacy = new Pharmacy { Id = 1, Name = "Main", IsActive = true, CreatedAt = DateTime.UtcNow };
        var user = new User { Id = 1, FullName = "Admin", Email = "admin@pharmacy.com", PasswordHash = "hash", PharmacyId = 1, Role = UserRole.Admin, IsActive = true, CreatedAt = DateTime.UtcNow };
        var category = new Category { Id = 1, Name = "General", IsActive = true };
        var location = new Location { Id = 1, Code = "LOC-1", Name = "Loc 1", IsActive = true };
        var product = new Product { Id = 1, Name = "Vitamins", CategoryId = 1, SellingPrice = 100m, MinimumStockLevel = 5, IsActive = true };
        var batch = new Batch { Id = 1, ProductId = 1, LocationId = 1, BatchNumber = "B-01", QuantityOnHand = 10, PurchasePrice = 50m, ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(12)), AddedDate = DateTime.UtcNow };

        db.AddRange(pharmacy, user, category, location, product, batch);
        await db.SaveChangesAsync();

        // 1. Mismatched Split: Total is 100, but Cash=50, Card=30 (Sum = 80 != 100) -> Throws exception
        var invalidSplit = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-SPLIT-FAIL",
            PaymentMethod: PaymentMethod.Split,
            Items: new List<SaleItemRequest> { new SaleItemRequest(ProductId: 1, Quantity: 1, UnitPrice: 100m) },
            CashAmount: 50m,
            CardAmount: 30m,
            VodafoneCashAmount: 0m
        );

        await Assert.ThrowsAsync<InvalidBusinessOperationException>(() => saleService.ProcessSaleAsync(invalidSplit));

        // 2. Valid Split: Total is 100, Cash=40, Card=40, VodafoneCash=20 (Sum = 100) -> Succeeds
        var validSplit = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-SPLIT-OK",
            PaymentMethod: PaymentMethod.Split,
            Items: new List<SaleItemRequest> { new SaleItemRequest(ProductId: 1, Quantity: 1, UnitPrice: 100m) },
            CashAmount: 40m,
            CardAmount: 40m,
            VodafoneCashAmount: 20m
        );

        var successfulSale = await saleService.ProcessSaleAsync(validSplit);
        Assert.NotNull(successfulSale);
        Assert.Equal(100m, successfulSale.TotalAmount);
        Assert.Equal(PaymentMethod.Split, successfulSale.PaymentMethod);
    }

    [Fact]
    public async Task ReturnTransactionService_RestocksSaleableNonExpiredItemsOnly()
    {
        using var db = CreateDbContext();
        var returnService = new ReturnTransactionService(db);

        var pharmacy = new Pharmacy { Id = 1, Name = "Main", IsActive = true, CreatedAt = DateTime.UtcNow };
        var user = new User { Id = 1, FullName = "Admin", Email = "admin@pharmacy.com", PasswordHash = "hash", PharmacyId = 1, Role = UserRole.Admin, IsActive = true, CreatedAt = DateTime.UtcNow };
        var category = new Category { Id = 1, Name = "General", IsActive = true };
        var location = new Location { Id = 1, Code = "LOC-1", Name = "Loc 1", IsActive = true };
        var product = new Product { Id = 1, Name = "Antibiotic", CategoryId = 1, SellingPrice = 50m, MinimumStockLevel = 5, IsActive = true };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var validBatch = new Batch { Id = 1, ProductId = 1, LocationId = 1, BatchNumber = "B-VALID", QuantityOnHand = 10, PurchasePrice = 30m, ExpiryDate = today.AddMonths(5), AddedDate = DateTime.UtcNow };
        var expiredBatch = new Batch { Id = 2, ProductId = 1, LocationId = 1, BatchNumber = "B-EXPIRED", QuantityOnHand = 5, PurchasePrice = 30m, ExpiryDate = today.AddDays(-5), AddedDate = DateTime.UtcNow };

        var sale = new Sale
        {
            Id = 1,
            InvoiceNumber = "INV-SALE-01",
            UserId = 1,
            PharmacyId = 1,
            SaleDate = DateTime.UtcNow,
            TotalAmount = 150m,
            PaymentMethod = PaymentMethod.Cash,
            SaleItems = new List<SaleItem>
            {
                new SaleItem { Id = 1, ProductId = 1, Quantity = 2, UnitPrice = 50m },
                new SaleItem { Id = 2, ProductId = 1, Quantity = 1, UnitPrice = 50m }
            }
        };

        db.AddRange(pharmacy, user, category, location, product, validBatch, expiredBatch, sale);
        await db.SaveChangesAsync();

        // 1. Return valid item to stock -> restores batch QuantityOnHand (10 + 1 = 11)
        var validReturnRequest = new CreateSalesReturnRequest(
            SaleId: 1,
            UserId: 1,
            Reason: "Customer changed mind",
            Items: new List<SalesReturnItemInput>
            {
                new SalesReturnItemInput(SaleItemId: 1, BatchId: 1, Quantity: 1, ReturnToStock: true)
            }
        );

        var validReturnResult = await returnService.ProcessSalesReturnAsync(validReturnRequest);
        Assert.NotNull(validReturnResult);
        Assert.Equal(50m, validReturnResult.TotalAmount);

        var updatedValidBatch = await db.Batches.FindAsync(1);
        Assert.Equal(11, updatedValidBatch!.QuantityOnHand);

        // 2. Return expired item to saleable stock -> Throws InvalidBusinessOperationException
        var expiredReturnRequest = new CreateSalesReturnRequest(
            SaleId: 1,
            UserId: 1,
            Reason: "Product returned expired",
            Items: new List<SalesReturnItemInput>
            {
                new SalesReturnItemInput(SaleItemId: 2, BatchId: 2, Quantity: 1, ReturnToStock: true)
            }
        );

        await Assert.ThrowsAsync<InvalidBusinessOperationException>(() => returnService.ProcessSalesReturnAsync(expiredReturnRequest));
    }
}
