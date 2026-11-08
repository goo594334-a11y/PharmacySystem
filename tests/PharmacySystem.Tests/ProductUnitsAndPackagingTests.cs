using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Exceptions;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.Packaging;
using PharmacySystem.Models.ViewModels.Sales;
using PharmacySystem.Services;
using PharmacySystem.Services.Packaging;
using Xunit;

namespace PharmacySystem.Tests;

public class ProductUnitsAndPackagingTests
{
    private PharmacyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new PharmacyDbContext(options);
    }

    [Fact]
    public void PackagingService_HierarchyAndPermittedUnits_CalculatesCorrectly()
    {
        // Arrange
        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 1,
            Name = "Panadol Extra",
            SellingPrice = 90m,
            Barcode = "123456789"
        };

        var units = new List<ProductUnitDto>
        {
            new ProductUnitDto
            {
                UnitName = "شريط / Strip",
                ConversionFactor = 10,
                SellingPrice = 32m,
                AllowPurchase = true,
                AllowSale = true,
                ContainsQuantity = 10,
                ContainsUnitName = "قرص / Tablet"
            },
            new ProductUnitDto
            {
                UnitName = "علبة / Box",
                ConversionFactor = 30,
                SellingPrice = 90m,
                AllowPurchase = true,
                AllowSale = true,
                ContainsQuantity = 3,
                ContainsUnitName = "شريط / Strip"
            }
        };

        // Act
        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 3.5m, "123456789", units);
        product.Description = packagingService.EmbedPackaging("Analgesic tablets", config);
        var retrievedConfig = packagingService.GetPackaging(product);

        // Assert
        Assert.Equal("قرص / Tablet", retrievedConfig.BaseUnitName);
        Assert.Equal(3, retrievedConfig.PermittedUnits.Count);

        var baseUnit = retrievedConfig.PermittedUnits.First(u => u.IsBaseUnit);
        Assert.Equal(1, baseUnit.ConversionFactor);
        Assert.Equal(3.5m, baseUnit.SellingPrice);

        var stripUnit = retrievedConfig.PermittedUnits.First(u => u.UnitName == "شريط / Strip");
        Assert.Equal(10, stripUnit.ConversionFactor);
        Assert.Equal(32m, stripUnit.SellingPrice);

        var boxUnit = retrievedConfig.PermittedUnits.First(u => u.UnitName == "علبة / Box");
        Assert.Equal(30, boxUnit.ConversionFactor);
        Assert.Equal(90m, boxUnit.SellingPrice);
    }

    [Fact]
    public async Task PurchasingWithPackagingUnits_ConvertsToBaseUnitsAndAccurateUnitCost()
    {
        // Arrange
        using var context = CreateDbContext();
        var supplier = new Supplier { Id = 1, Name = "Pharma Distribution Co.", IsActive = true };
        var user = new User { Id = 1, Email = "admin@test.com", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin, IsActive = true };
        var category = new Category { Id = 1, Name = "Medicines" };
        var location = new Location { Id = 1, Code = "LOC-1", Name = "Main Storage", IsActive = true };
        var product = new Product { Id = 1, Name = "Panadol", CategoryId = 1, SellingPrice = 90m, IsActive = true };

        context.Suppliers.Add(supplier);
        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var purchaseService = new PurchaseTransactionService(context);

        // Act: Purchase 100 Boxes at 60 EGP/box, conversion factor 30 tablets/box
        var request = new CreatePurchaseRequest(
            SupplierId: 1,
            UserId: 1,
            InvoiceNumber: "PUR-2026-001",
            Notes: "100 Boxes purchased",
            Items: new List<PurchaseItemInput>
            {
                new PurchaseItemInput(
                    ProductId: 1,
                    Quantity: 100,
                    UnitPurchasePrice: 60.00m,
                    BatchNumber: "LOT-PANADOL-01",
                    ExpiryDate: DateOnly.FromDateTime(DateTime.Today.AddYears(2)),
                    LocationId: 1,
                    UnitName: "علبة / Box",
                    ConversionFactor: 30
                )
            }
        );

        var draft = await purchaseService.CreateDraftPurchaseAsync(request);
        Assert.Equal(6000.00m, draft.TotalAmount); // 100 * 60 = 6000

        var confirmed = await purchaseService.ConfirmPurchaseAsync(draft.Id);

        // Assert
        Assert.Equal(PurchaseStatus.Confirmed, confirmed.Status);
        var createdBatch = await context.Batches.FirstOrDefaultAsync(b => b.ProductId == 1 && b.BatchNumber == "LOT-PANADOL-01");
        Assert.NotNull(createdBatch);

        // Stock must be 100 * 30 = 3,000 base units (tablets)
        Assert.Equal(3000, createdBatch.QuantityOnHand);

        // Cost per base unit must be 60.00 / 30 = 2.00 EGP
        Assert.Equal(2.00m, createdBatch.PurchasePrice);
    }

    [Fact]
    public async Task PosSaleWithMultipleUnits_DeductsCorrectBaseQty()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = 1, Email = "cashier@test.com", FullName = "Cashier", PasswordHash = "x", Role = UserRole.Pharmacist, IsActive = true };
        var category = new Category { Id = 1, Name = "Pain Relief" };
        var location = new Location { Id = 1, Code = "SHELF-1", Name = "Front Shelf", IsActive = true };

        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 1,
            Name = "Panadol",
            CategoryId = 1,
            SellingPrice = 90m,
            IsActive = true
        };

        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 3.50m, null, new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "شريط / Strip", ConversionFactor = 10, SellingPrice = 32m, AllowSale = true },
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 30, SellingPrice = 90m, AllowSale = true }
        });
        product.Description = packagingService.EmbedPackaging("Panadol 500mg", config);

        var batch = new Batch
        {
            Id = 1,
            ProductId = 1,
            LocationId = 1,
            BatchNumber = "LOT-100",
            AddedDate = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityOnHand = 300, // 300 tablets
            PurchasePrice = 2.00m
        };

        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Products.Add(product);
        context.Batches.Add(batch);
        await context.SaveChangesAsync();

        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(context, fefoService, packagingService);

        // Act: Sell 1 Box (30 tablets) + 2 Strips (20 tablets) = 50 tablets deducted
        var saleRequest = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-TEST-001",
            PaymentMethod: PaymentMethod.Cash,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(ProductId: 1, Quantity: 1, UnitName: "علبة / Box", UnitPrice: 90m),
                new SaleItemRequest(ProductId: 1, Quantity: 2, UnitName: "شريط / Strip", UnitPrice: 32m)
            }
        );

        var sale = await saleService.ProcessSaleAsync(saleRequest);

        // Assert: Total = 1*90 + 2*32 = 154 EGP
        Assert.Equal(154.00m, sale.TotalAmount);

        // Remaining stock = 300 - 50 = 250 tablets
        var updatedBatch = await context.Batches.FindAsync(1);
        Assert.NotNull(updatedBatch);
        Assert.Equal(250, updatedBatch.QuantityOnHand);
    }

    [Fact]
    public async Task SalesReturn_RestoresCorrectBaseQuantity()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = 1, Email = "cashier@test.com", FullName = "Cashier", PasswordHash = "x", Role = UserRole.Pharmacist, IsActive = true };
        var category = new Category { Id = 1, Name = "Pain Relief" };
        var location = new Location { Id = 1, Code = "SHELF-1", Name = "Front Shelf", IsActive = true };

        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 1,
            Name = "Panadol",
            CategoryId = 1,
            SellingPrice = 90m,
            IsActive = true
        };

        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 3.50m, null, new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "شريط / Strip", ConversionFactor = 10, SellingPrice = 32m, AllowSale = true },
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 30, SellingPrice = 90m, AllowSale = true }
        });
        product.Description = packagingService.EmbedPackaging("Panadol 500mg", config);

        var batch = new Batch
        {
            Id = 1,
            ProductId = 1,
            LocationId = 1,
            BatchNumber = "LOT-100",
            AddedDate = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityOnHand = 300,
            PurchasePrice = 2.00m
        };

        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Products.Add(product);
        context.Batches.Add(batch);
        await context.SaveChangesAsync();

        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(context, fefoService, packagingService);
        var returnService = new ReturnTransactionService(context);

        // Sell 1 Box (30 tablets)
        var saleRequest = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-RET-001",
            PaymentMethod: PaymentMethod.Cash,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(ProductId: 1, Quantity: 1, UnitName: "علبة / Box", UnitPrice: 90m)
            }
        );
        var sale = await saleService.ProcessSaleAsync(saleRequest);

        // Stock after sale: 300 - 30 = 270
        var batchAfterSale = await context.Batches.FindAsync(1);
        Assert.NotNull(batchAfterSale);
        Assert.Equal(270, batchAfterSale.QuantityOnHand);

        // Act: Return 1 Box
        var boxSaleItem = sale.SaleItems.First();
        var returnRequest = new CreateSalesReturnRequest(
            SaleId: sale.Id,
            UserId: 1,
            Reason: "Wrong medicine",
            Items: new List<SalesReturnItemInput>
            {
                new SalesReturnItemInput(
                    SaleItemId: boxSaleItem.Id,
                    BatchId: 1,
                    Quantity: 1,
                    ReturnToStock: true
                )
            }
        );

        var salesReturn = await returnService.ProcessSalesReturnAsync(returnRequest);

        // Assert: Refund = 1 * 90 = 90 EGP
        Assert.Equal(90.00m, salesReturn.TotalAmount);

        // Stock restored by 30 tablets (1 box): 270 + 30 = 300
        await context.Entry(batchAfterSale).ReloadAsync();
        Assert.Equal(300, batchAfterSale.QuantityOnHand);
    }

    [Fact]
    public void FormatStockBreakdown_DisplaysReadablePackagingBreakdown()
    {
        // Arrange
        var packagingService = new ProductPackagingService();
        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 3.50m, null, new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "شريط / Strip", ConversionFactor = 10, SellingPrice = 32m },
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 30, SellingPrice = 90m }
        });

        // Act: 95 tablets = 3 boxes + 0 strips + 5 tablets
        var formatted = packagingService.FormatStockBreakdown(95, config);

        // Assert
        Assert.Contains("3", formatted); // 3 boxes
        Assert.Contains("5", formatted); // 5 tablets remainder
        Assert.Contains("علبة / Box", formatted);
        Assert.Contains("قرص / Tablet", formatted);
    }

    [Fact]
    public async Task Overselling_WhenQuantityExceedsStock_ThrowsInsufficientStockException()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = 1, Email = "cashier@test.com", FullName = "Cashier", PasswordHash = "x", Role = UserRole.Pharmacist, IsActive = true };
        var category = new Category { Id = 1, Name = "Medicines" };
        var location = new Location { Id = 1, Code = "SHELF-1", Name = "Front Shelf", IsActive = true };

        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 1,
            Name = "Panadol",
            CategoryId = 1,
            SellingPrice = 90m,
            IsActive = true
        };

        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 3.50m, null, new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 30, SellingPrice = 90m, AllowSale = true }
        });
        product.Description = packagingService.EmbedPackaging("Panadol 500mg", config);

        var batch = new Batch
        {
            Id = 1, ProductId = 1, LocationId = 1,
            BatchNumber = "LOT-LOW", AddedDate = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityOnHand = 20, // Only 20 tablets available
            PurchasePrice = 2.00m
        };

        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Products.Add(product);
        context.Batches.Add(batch);
        await context.SaveChangesAsync();

        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(context, fefoService, packagingService);

        // Act & Assert: Trying to sell 1 Box (30 tablets) but only 20 available
        var saleRequest = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-OVER-001",
            PaymentMethod: PaymentMethod.Cash,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(ProductId: 1, Quantity: 1, UnitName: "علبة / Box", UnitPrice: 90m)
            }
        );

        await Assert.ThrowsAsync<InsufficientStockException>(() => saleService.ProcessSaleAsync(saleRequest));

        // Verify stock was NOT modified
        var batchAfter = await context.Batches.FindAsync(1);
        Assert.Equal(20, batchAfter!.QuantityOnHand);
    }

    [Fact]
    public async Task FefoMultiBatch_AllocatesFromOldestExpiryFirst()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = 1, Email = "cashier@test.com", FullName = "Cashier", PasswordHash = "x", Role = UserRole.Pharmacist, IsActive = true };
        var category = new Category { Id = 1, Name = "Medicines" };
        var location = new Location { Id = 1, Code = "SHELF-1", Name = "Front Shelf", IsActive = true };

        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 1,
            Name = "Amoxicillin",
            CategoryId = 1,
            SellingPrice = 5.00m,
            IsActive = true
        };

        var config = packagingService.BuildPackagingConfig("Capsule", 5.00m, null, new List<ProductUnitDto>());
        product.Description = packagingService.EmbedPackaging("Amoxicillin 500mg", config);

        // Batch A: expires sooner (6 months), 15 capsules
        var batchA = new Batch
        {
            Id = 1, ProductId = 1, LocationId = 1,
            BatchNumber = "BATCH-A-SOON",
            AddedDate = DateTime.UtcNow.AddDays(-30),
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
            QuantityOnHand = 15,
            PurchasePrice = 3.00m
        };

        // Batch B: expires later (18 months), 25 capsules
        var batchB = new Batch
        {
            Id = 2, ProductId = 1, LocationId = 1,
            BatchNumber = "BATCH-B-LATER",
            AddedDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(18)),
            QuantityOnHand = 25,
            PurchasePrice = 3.50m
        };

        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Products.Add(product);
        context.Batches.AddRange(batchA, batchB);
        await context.SaveChangesAsync();

        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(context, fefoService, packagingService);

        // Act: Sell 20 capsules — should take all 15 from BATCH-A + 5 from BATCH-B (FEFO)
        var saleRequest = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-FEFO-001",
            PaymentMethod: PaymentMethod.Cash,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(ProductId: 1, Quantity: 20, UnitName: "Capsule", UnitPrice: 5.00m)
            }
        );

        var sale = await saleService.ProcessSaleAsync(saleRequest);

        // Assert
        Assert.Equal(100.00m, sale.TotalAmount); // 20 * 5.00

        var batchAAfter = await context.Batches.FindAsync(1);
        var batchBAfter = await context.Batches.FindAsync(2);

        // FEFO: Batch A (sooner expiry) fully consumed first
        Assert.Equal(0, batchAAfter!.QuantityOnHand);
        // Remaining 5 from Batch B
        Assert.Equal(20, batchBAfter!.QuantityOnHand);
    }

    [Fact]
    public async Task PurchaseReturn_ReducesBatchStockCorrectly()
    {
        // Arrange
        using var context = CreateDbContext();
        var supplier = new Supplier { Id = 1, Name = "Supplier X", IsActive = true };
        var user = new User { Id = 1, Email = "admin@test.com", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin, IsActive = true };
        var category = new Category { Id = 1, Name = "Medicines" };
        var location = new Location { Id = 1, Code = "LOC-1", Name = "Main Storage", IsActive = true };
        var product = new Product { Id = 1, Name = "Panadol", CategoryId = 1, SellingPrice = 90m, IsActive = true };

        context.Suppliers.Add(supplier);
        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var purchaseService = new PurchaseTransactionService(context);

        // Create and confirm a purchase of 500 tablets
        var purchaseRequest = new CreatePurchaseRequest(
            SupplierId: 1, UserId: 1, InvoiceNumber: "PUR-RET-001",
            Notes: null,
            Items: new List<PurchaseItemInput>
            {
                new PurchaseItemInput(ProductId: 1, Quantity: 500, UnitPurchasePrice: 2.00m,
                    BatchNumber: "LOT-RET", ExpiryDate: DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
                    LocationId: 1)
            }
        );

        var draft = await purchaseService.CreateDraftPurchaseAsync(purchaseRequest);
        var purchase = await purchaseService.ConfirmPurchaseAsync(draft.Id);

        var batch = await context.Batches.FirstAsync(b => b.BatchNumber == "LOT-RET");
        Assert.Equal(500, batch.QuantityOnHand);

        var purchaseItem = purchase.PurchaseItems.First();

        // Act: Return 200 tablets to supplier
        var returnService = new ReturnTransactionService(context);
        var returnRequest = new CreatePurchaseReturnRequest(
            PurchaseId: purchase.Id,
            UserId: 1,
            Reason: "Damaged packaging",
            Items: new List<PurchaseReturnItemInput>
            {
                new PurchaseReturnItemInput(
                    PurchaseItemId: purchaseItem.Id,
                    BatchId: batch.Id,
                    Quantity: 200
                )
            }
        );

        var purchaseReturn = await returnService.ProcessPurchaseReturnAsync(returnRequest);

        // Assert
        Assert.Equal(400.00m, purchaseReturn.TotalAmount); // 200 * 2.00
        await context.Entry(batch).ReloadAsync();
        Assert.Equal(300, batch.QuantityOnHand); // 500 - 200

        // Verify cannot return more than remaining
        var overReturnRequest = new CreatePurchaseReturnRequest(
            PurchaseId: purchase.Id,
            UserId: 1,
            Reason: "Recall",
            Items: new List<PurchaseReturnItemInput>
            {
                new PurchaseReturnItemInput(PurchaseItemId: purchaseItem.Id, BatchId: batch.Id, Quantity: 400)
            }
        );

        await Assert.ThrowsAsync<InvalidBusinessOperationException>(
            () => returnService.ProcessPurchaseReturnAsync(overReturnRequest));
    }

    [Fact]
    public async Task SplitPaymentMismatch_ThrowsException()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = 1, Email = "cashier@test.com", FullName = "Cashier", PasswordHash = "x", Role = UserRole.Pharmacist, IsActive = true };
        var category = new Category { Id = 1, Name = "Medicines" };
        var location = new Location { Id = 1, Code = "SHELF-1", Name = "Front Shelf", IsActive = true };

        var packagingService = new ProductPackagingService();
        var product = new Product { Id = 1, Name = "Aspirin", CategoryId = 1, SellingPrice = 50m, IsActive = true };
        product.Description = packagingService.EmbedPackaging("Aspirin",
            packagingService.BuildPackagingConfig("Tablet", 50m, null, new List<ProductUnitDto>()));

        var batch = new Batch
        {
            Id = 1, ProductId = 1, LocationId = 1,
            BatchNumber = "LOT-SPL", AddedDate = DateTime.UtcNow,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityOnHand = 100, PurchasePrice = 30m
        };

        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);
        context.Products.Add(product);
        context.Batches.Add(batch);
        await context.SaveChangesAsync();

        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(context, fefoService, packagingService);

        // Act & Assert: Split payment that doesn't add up
        var saleRequest = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-SPL-001",
            PaymentMethod: PaymentMethod.Split,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(ProductId: 1, Quantity: 2, UnitName: "Tablet", UnitPrice: 50m)
            },
            CashAmount: 50m,
            CardAmount: 20m  // Total split = 70, but sale total = 100
        );

        await Assert.ThrowsAsync<InvalidBusinessOperationException>(
            () => saleService.ProcessSaleAsync(saleRequest));
    }

    [Fact]
    public void TabletProduct_3StripsPerBox10TabletsPerStrip_Calculates30TabletsPerBox()
    {
        // Arrange
        var packagingService = new ProductPackagingService();
        var product = new Product { Id = 10, Name = "Cataflam 50mg", SellingPrice = 45m };

        var units = new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "شريط / Strip", ConversionFactor = 10, ContainsQuantity = 10, ContainsUnitName = "قرص / Tablet", SellingPrice = 15m, AllowSale = true },
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 30, ContainsQuantity = 3, ContainsUnitName = "شريط / Strip", SellingPrice = 45m, AllowSale = true }
        };

        // Act
        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 1.50m, null, units, "أقراص / Tablets", false);
        product.Description = packagingService.EmbedPackaging("Analgesic", config);
        var loaded = packagingService.GetPackaging(product);

        // Assert
        Assert.Equal("أقراص / Tablets", loaded.DosageForm);
        Assert.False(loaded.SellFullPackageOnly);
        Assert.Equal("قرص / Tablet", loaded.BaseUnitName);

        var strip = loaded.PermittedUnits.FirstOrDefault(u => u.UnitName == "شريط / Strip");
        Assert.NotNull(strip);
        Assert.Equal(10, strip.ConversionFactor);

        var box = loaded.PermittedUnits.FirstOrDefault(u => u.UnitName == "علبة / Box");
        Assert.NotNull(box);
        Assert.Equal(30, box.ConversionFactor);
        Assert.Equal(45m, box.SellingPrice);
    }

    [Fact]
    public void FullPackageOnlyProduct_HidesPartialUnitsFromPermittedUnits()
    {
        // Arrange
        var packagingService = new ProductPackagingService();
        var product = new Product { Id = 11, Name = "Antibiotic Suspension", SellingPrice = 120m };

        var units = new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "شريط / Strip", ConversionFactor = 10, SellingPrice = 40m, AllowSale = true },
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 30, SellingPrice = 120m, AllowSale = true }
        };

        // Act: configure as SellFullPackageOnly = true
        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 4.0m, null, units, "أقراص / Tablets", sellFullPackageOnly: true);
        product.Description = packagingService.EmbedPackaging("Full Box Only Antibiotic", config);

        var permittedForCashier = packagingService.GetPermittedUnits(product);

        // Assert: Only 1 unit (the Box) should be permitted for cashier sale
        Assert.Single(permittedForCashier);
        Assert.Equal("علبة / Box", permittedForCashier[0].UnitName);
        Assert.Equal(30, permittedForCashier[0].ConversionFactor);
        Assert.True(permittedForCashier[0].AllowSale);
    }

    [Fact]
    public async Task MultiProductInvoice_TabletsStrips_InjectionsAmpoules_Boxes_DeductsCorrectStockInOneInvoice()
    {
        // Arrange
        using var context = CreateDbContext();
        var user = new User { Id = 1, Email = "cashier@test.com", FullName = "Pharmacist Ahmed", PasswordHash = "x", Role = UserRole.Pharmacist, IsActive = true };
        var category = new Category { Id = 1, Name = "General Medicines" };
        var location = new Location { Id = 1, Code = "SHELF-A", Name = "Main Dispensing Shelf", IsActive = true };

        context.Users.Add(user);
        context.Categories.Add(category);
        context.Locations.Add(location);

        var packagingService = new ProductPackagingService();

        // Product 1: Tablet Medicine (Panadol) - Base unit = Tablet, 1 Strip = 10 tablets, 1 Box = 30 tablets
        var product1 = new Product { Id = 1, Name = "Panadol Extra", CategoryId = 1, SellingPrice = 90m, IsActive = true };
        var config1 = packagingService.BuildPackagingConfig("قرص / Tablet", 3.0m, null, new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "شريط / Strip", ConversionFactor = 10, SellingPrice = 30m, AllowSale = true },
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 30, SellingPrice = 90m, AllowSale = true }
        }, "أقراص / Tablets", false);
        product1.Description = packagingService.EmbedPackaging("Pain relief", config1);

        var batch1 = new Batch
        {
            Id = 1, ProductId = 1, LocationId = 1, BatchNumber = "LOT-PAN-01",
            AddedDate = DateTime.UtcNow, ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityOnHand = 100, // 100 tablets
            PurchasePrice = 1.50m
        };

        // Product 2: Injection Medicine (Voltaren Ampoules) - Base unit = Ampoule, 1 Box = 5 ampoules
        var product2 = new Product { Id = 2, Name = "Voltaren 75mg Ampoules", CategoryId = 1, SellingPrice = 75m, IsActive = true };
        var config2 = packagingService.BuildPackagingConfig("أمبول / Ampoule", 15.0m, null, new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "علبة / Box", ConversionFactor = 5, SellingPrice = 75m, AllowSale = true }
        }, "حقن / Injection", false);
        product2.Description = packagingService.EmbedPackaging("Anti-inflammatory injection", config2);

        var batch2 = new Batch
        {
            Id = 2, ProductId = 2, LocationId = 1, BatchNumber = "LOT-VOL-01",
            AddedDate = DateTime.UtcNow, ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(18)),
            QuantityOnHand = 25, // 25 ampoules
            PurchasePrice = 8.00m
        };

        // Product 3: Syrup Medicine (Cough Syrup) - Base unit = Bottle (Box = 1 Bottle)
        var product3 = new Product { Id = 3, Name = "Bronchicum Syrup", CategoryId = 1, SellingPrice = 45m, IsActive = true };
        var config3 = packagingService.BuildPackagingConfig("زجاجة / Bottle", 45.0m, null, new List<ProductUnitDto>(), "شراب / Syrup", true);
        product3.Description = packagingService.EmbedPackaging("Cough syrup bottle", config3);

        var batch3 = new Batch
        {
            Id = 3, ProductId = 3, LocationId = 1, BatchNumber = "LOT-BRO-01",
            AddedDate = DateTime.UtcNow, ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
            QuantityOnHand = 10, // 10 bottles
            PurchasePrice = 28.00m
        };

        context.Products.AddRange(product1, product2, product3);
        context.Batches.AddRange(batch1, batch2, batch3);
        await context.SaveChangesAsync();

        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(context, fefoService, packagingService);

        // Act: Create ONE sales invoice containing:
        // - Item 1: Tablet medicine -> 1 strip (10 tablets) @ 30 EGP
        // - Item 2: Injection medicine -> 2 ampoules @ 15 EGP = 30 EGP
        // - Item 3: Syrup medicine -> 1 bottle (box) @ 45 EGP
        var invoiceRequest = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-MULTI-001",
            PaymentMethod: PaymentMethod.Cash,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(ProductId: 1, Quantity: 1, UnitName: "شريط / Strip", UnitPrice: 30m),
                new SaleItemRequest(ProductId: 2, Quantity: 2, UnitName: "أمبول / Ampoule", UnitPrice: 15m),
                new SaleItemRequest(ProductId: 3, Quantity: 1, UnitName: "زجاجة / Bottle", UnitPrice: 45m)
            }
        );

        var sale = await saleService.ProcessSaleAsync(invoiceRequest);

        // Assert 1: Belongs to ONE invoice
        Assert.NotNull(sale);
        Assert.Equal("INV-MULTI-001", sale.InvoiceNumber);
        Assert.Equal(3, sale.SaleItems.Count);

        // Assert 2: Total amount = 30 + (2*15=30) + 45 = 105.00 EGP
        Assert.Equal(105.00m, sale.TotalAmount);

        // Assert 3: Independent deduction per product base units
        var updatedBatch1 = await context.Batches.FindAsync(1);
        Assert.NotNull(updatedBatch1);
        // Panadol: Started with 100 tablets, sold 1 strip (10 tablets) -> 90 tablets remaining
        Assert.Equal(90, updatedBatch1.QuantityOnHand);

        var updatedBatch2 = await context.Batches.FindAsync(2);
        Assert.NotNull(updatedBatch2);
        // Voltaren: Started with 25 ampoules, sold 2 ampoules -> 23 ampoules remaining
        Assert.Equal(23, updatedBatch2.QuantityOnHand);

        var updatedBatch3 = await context.Batches.FindAsync(3);
        Assert.NotNull(updatedBatch3);
        // Syrup: Started with 10 bottles, sold 1 bottle -> 9 bottles remaining
        Assert.Equal(9, updatedBatch3.QuantityOnHand);

        // Assert 4: Verify customer receipt format excludes internal confidential data
        var receiptItems = new List<CustomerReceiptItemViewModel>();
        foreach (var si in sale.SaleItems)
        {
            var allocatedBase = si.BatchAllocations.Sum(ba => ba.Quantity);
            var factor = si.Quantity > 0 ? (allocatedBase / si.Quantity) : 1;
            var packaging = packagingService.GetPackaging(si.Product);
            var unit = packaging.PermittedUnits.FirstOrDefault(u => u.ConversionFactor == factor);
            receiptItems.Add(new CustomerReceiptItemViewModel
            {
                ProductName = si.Product.Name,
                SellingUnit = unit?.UnitName ?? packaging.BaseUnitName,
                Quantity = si.Quantity,
                UnitPrice = si.UnitPrice
            });
        }

        var customerReceipt = new CustomerReceiptViewModel
        {
            SaleId = sale.Id,
            InvoiceNumber = sale.InvoiceNumber,
            PharmacyName = "Al-Shifa Pharmacy",
            SaleDate = sale.SaleDate,
            CashierName = user.FullName,
            CustomerName = "Walk-in Customer",
            PaymentMethod = sale.PaymentMethod,
            TotalAmount = sale.TotalAmount,
            Items = receiptItems
        };

        Assert.Equal(3, customerReceipt.Items.Count);
        Assert.Equal("شريط / Strip", customerReceipt.Items[0].SellingUnit);
        Assert.Equal("أمبول / Ampoule", customerReceipt.Items[1].SellingUnit);
        Assert.Equal("زجاجة / Bottle", customerReceipt.Items[2].SellingUnit);
        Assert.Equal(105.00m, customerReceipt.TotalAmount);
    }

    [Fact]
    public void NonSplittableProduct_GeneratesFullPackageOnlyConfig_WithSingleUnit()
    {
        // Arrange
        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 10,
            Name = "Cough Syrup 120ml",
            SellingPrice = 45.00m,
            Barcode = "9876543210"
        };

        // Act: Non-splittable configuration
        var config = packagingService.BuildPackagingConfig(
            baseUnitName: "علبة / Box",
            basePrice: 45.00m,
            baseBarcode: "9876543210",
            units: new List<ProductUnitDto>(),
            dosageForm: "شراب / Syrup",
            sellFullPackageOnly: true
        );
        product.Description = packagingService.EmbedPackaging("Soothing cough syrup", config);

        var retrieved = packagingService.GetPackaging(product);
        var permitted = packagingService.GetPermittedUnits(product);

        // Assert
        Assert.True(retrieved.SellFullPackageOnly);
        Assert.Single(permitted);
        Assert.Equal("علبة / Box", permitted[0].UnitName);
        Assert.Equal(1, permitted[0].ConversionFactor);
        Assert.Equal(45.00m, permitted[0].SellingPrice);
        Assert.True(permitted[0].AllowSale);

        var breakdown = packagingService.FormatStockBreakdown(25, retrieved);
        Assert.Contains("25", breakdown);
        Assert.Contains("علبة / Box", breakdown);
    }

    [Fact]
    public void SplittableTablets_GeneratesMultiLevelPackaging_WithAutoCalculatedFactors()
    {
        // Arrange
        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 11,
            Name = "Augmentin 1g",
            SellingPrice = 120.00m,
            Barcode = "5551234567"
        };

        // Act: 1 Box = 2 Strips = 14 Tablets (7 tablets per strip)
        var units = new List<ProductUnitDto>
        {
            new ProductUnitDto
            {
                UnitName = "قرص / Tablet",
                ConversionFactor = 1,
                SellingPrice = 9.00m, // Custom adjusted price from suggested (120/14 = 8.57)
                IsBaseUnit = true,
                AllowSale = true,
                AllowPurchase = true
            },
            new ProductUnitDto
            {
                UnitName = "شريط / Strip",
                ConversionFactor = 7,
                ContainsQuantity = 7,
                ContainsUnitName = "قرص / Tablet",
                SellingPrice = 60.00m,
                IsBaseUnit = false,
                AllowSale = true,
                AllowPurchase = true
            },
            new ProductUnitDto
            {
                UnitName = "علبة / Box",
                ConversionFactor = 14,
                ContainsQuantity = 2,
                ContainsUnitName = "شريط / Strip",
                SellingPrice = 120.00m,
                IsBaseUnit = false,
                AllowSale = true,
                AllowPurchase = true
            }
        };

        var config = packagingService.BuildPackagingConfig(
            baseUnitName: "قرص / Tablet",
            basePrice: 120.00m,
            baseBarcode: "5551234567",
            units: units,
            dosageForm: "أقراص / Tablets",
            sellFullPackageOnly: false
        );
        product.Description = packagingService.EmbedPackaging("Broad spectrum antibiotic", config);

        var retrieved = packagingService.GetPackaging(product);
        var permitted = packagingService.GetPermittedUnits(product);

        // Assert
        Assert.False(retrieved.SellFullPackageOnly);
        Assert.Equal(3, permitted.Count);

        var tabletUnit = permitted.First(u => u.UnitName == "قرص / Tablet");
        Assert.Equal(1, tabletUnit.ConversionFactor);
        Assert.Equal(9.00m, tabletUnit.SellingPrice);
        Assert.True(tabletUnit.AllowSale);

        var stripUnit = permitted.First(u => u.UnitName == "شريط / Strip");
        Assert.Equal(7, stripUnit.ConversionFactor);
        Assert.Equal(60.00m, stripUnit.SellingPrice);
        Assert.True(stripUnit.AllowSale);

        var boxUnit = permitted.First(u => u.UnitName == "علبة / Box");
        Assert.Equal(14, boxUnit.ConversionFactor);
        Assert.Equal(120.00m, boxUnit.SellingPrice);
        Assert.True(boxUnit.AllowSale);
    }

    [Fact]
    public void SplittableTablets_StripsOnlySale_LocksTabletSale()
    {
        // Arrange
        var packagingService = new ProductPackagingService();
        var product = new Product
        {
            Id = 12,
            Name = "Cataflam 50mg",
            SellingPrice = 75.00m
        };

        // Act: Strip is permitted for sale, but individual tablets are not permitted for retail sale
        var units = new List<ProductUnitDto>
        {
            new ProductUnitDto
            {
                UnitName = "قرص / Tablet",
                ConversionFactor = 1,
                SellingPrice = 3.75m,
                IsBaseUnit = true,
                AllowSale = false, // Disabled at POS
                AllowPurchase = true
            },
            new ProductUnitDto
            {
                UnitName = "شريط / Strip",
                ConversionFactor = 10,
                ContainsQuantity = 10,
                ContainsUnitName = "قرص / Tablet",
                SellingPrice = 37.50m,
                IsBaseUnit = false,
                AllowSale = true,
                AllowPurchase = true
            },
            new ProductUnitDto
            {
                UnitName = "علبة / Box",
                ConversionFactor = 20,
                ContainsQuantity = 2,
                ContainsUnitName = "شريط / Strip",
                SellingPrice = 75.00m,
                IsBaseUnit = false,
                AllowSale = true,
                AllowPurchase = true
            }
        };

        var config = packagingService.BuildPackagingConfig("قرص / Tablet", 75.00m, null, units, "أقراص / Tablets", false);
        product.Description = packagingService.EmbedPackaging("Analgesic", config);

        var permitted = packagingService.GetPermittedUnits(product);

        // Assert: Only Box and Strip should be available for cashier sale, Tablet is excluded
        Assert.Equal(2, permitted.Count);
        Assert.Contains(permitted, u => u.UnitName == "علبة / Box");
        Assert.Contains(permitted, u => u.UnitName == "شريط / Strip");
        Assert.DoesNotContain(permitted, u => u.UnitName == "قرص / Tablet");
    }

    [Fact]
    public void SplittableAmpoulesAndSachets_ConfiguresSingleLevelSplitting()
    {
        // Arrange
        var packagingService = new ProductPackagingService();

        // 1. Ampoules: Box of 5 ampoules
        var ampouleUnits = new List<ProductUnitDto>
        {
            new ProductUnitDto
            {
                UnitName = "أمبول / Ampoule",
                ConversionFactor = 1,
                SellingPrice = 15.00m,
                IsBaseUnit = true,
                AllowSale = true
            },
            new ProductUnitDto
            {
                UnitName = "علبة / Box",
                ConversionFactor = 5,
                ContainsQuantity = 5,
                ContainsUnitName = "أمبول / Ampoule",
                SellingPrice = 75.00m,
                IsBaseUnit = false,
                AllowSale = true
            }
        };

        var ampouleConfig = packagingService.BuildPackagingConfig("أمبول / Ampoule", 75.00m, null, ampouleUnits, "حقن / Injection", false);
        Assert.Equal(2, ampouleConfig.PermittedUnits.Count);
        var ampoule = ampouleConfig.PermittedUnits.First(u => u.UnitName == "أمبول / Ampoule");
        Assert.Equal(1, ampoule.ConversionFactor);
        Assert.Equal(15.00m, ampoule.SellingPrice);

        // 2. Sachets: Box of 10 sachets
        var sachetUnits = new List<ProductUnitDto>
        {
            new ProductUnitDto
            {
                UnitName = "كيس / Sachet",
                ConversionFactor = 1,
                SellingPrice = 6.00m,
                IsBaseUnit = true,
                AllowSale = true
            },
            new ProductUnitDto
            {
                UnitName = "علبة / Box",
                ConversionFactor = 10,
                ContainsQuantity = 10,
                ContainsUnitName = "كيس / Sachet",
                SellingPrice = 60.00m,
                IsBaseUnit = false,
                AllowSale = true
            }
        };

        var sachetConfig = packagingService.BuildPackagingConfig("كيس / Sachet", 60.00m, null, sachetUnits, "أكياس وفوار / Sachets", false);
        Assert.Equal(2, sachetConfig.PermittedUnits.Count);
        var sachet = sachetConfig.PermittedUnits.First(u => u.UnitName == "كيس / Sachet");
        Assert.Equal(1, sachet.ConversionFactor);
        Assert.Equal(6.00m, sachet.SellingPrice);
    }

    [Fact]
    public async Task PosSale_MultiProductMultiUnit_ProcessesCorrectlyAndReducesStock()
    {
        // Arrange
        using var context = CreateDbContext();
        var packagingService = new ProductPackagingService();
        var fefoService = new FefoAllocationService();
        var saleService = new SaleTransactionService(context, fefoService, packagingService);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Product 1: Splittable Tablets (Box = 20 tablets, Strip = 10 tablets, Tablet = 1 tablet)
        var tabletUnits = new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "قرص", ConversionFactor = 1, SellingPrice = 3.00m, IsBaseUnit = true, AllowSale = true },
            new ProductUnitDto { UnitName = "شريط", ConversionFactor = 10, SellingPrice = 30.00m, IsBaseUnit = false, AllowSale = true },
            new ProductUnitDto { UnitName = "علبة", ConversionFactor = 20, SellingPrice = 60.00m, IsBaseUnit = false, AllowSale = true }
        };
        var p1Config = packagingService.BuildPackagingConfig("قرص", 60.00m, "111", tabletUnits, "أقراص", false);
        var p1Desc = packagingService.EmbedPackaging("Panadol Advance 500mg", p1Config);

        var product1 = new Product
        {
            Id = 101,
            Name = "Panadol Advance",
            Barcode = "111",
            SellingPrice = 60.00m,
            Description = p1Desc,
            IsActive = true,
            PharmacyId = 1
        };

        // Product 2: Single Unit Syrup (Bottle only, FullPackageOnly = true)
        var syrupUnits = new List<ProductUnitDto>
        {
            new ProductUnitDto { UnitName = "زجاجة", ConversionFactor = 1, SellingPrice = 45.00m, IsBaseUnit = true, AllowSale = true }
        };
        var p2Config = packagingService.BuildPackagingConfig("زجاجة", 45.00m, "222", syrupUnits, "شراب", true);
        var p2Desc = packagingService.EmbedPackaging("Bronchicum Syrup 100ml", p2Config);

        var product2 = new Product
        {
            Id = 102,
            Name = "Bronchicum Syrup",
            Barcode = "222",
            SellingPrice = 45.00m,
            Description = p2Desc,
            IsActive = true,
            PharmacyId = 1
        };

        var location = new Location { Id = 1, Code = "LOC-A", Name = "Main Shelf", PharmacyId = 1 };
        var user = new User { Id = 1, FullName = "Pharmacist", Email = "rx@pharmacy.com", Role = UserRole.Pharmacist, PharmacyId = 1, IsActive = true };

        context.Locations.Add(location);
        context.Users.Add(user);
        context.Products.AddRange(product1, product2);

        // Seed stock: 100 tablets of Panadol, 10 bottles of Syrup
        var batch1 = new Batch
        {
            Id = 201,
            ProductId = 101,
            BatchNumber = "B-PAN-01",
            QuantityOnHand = 100,
            PurchasePrice = 1.50m,
            ExpiryDate = today.AddYears(1),
            LocationId = 1
        };

        var batch2 = new Batch
        {
            Id = 202,
            ProductId = 102,
            BatchNumber = "B-BRO-01",
            QuantityOnHand = 10,
            PurchasePrice = 30.00m,
            ExpiryDate = today.AddYears(1),
            LocationId = 1
        };

        context.Batches.AddRange(batch1, batch2);
        await context.SaveChangesAsync();

        // Act: Sell 1 Box of Panadol (20 tablets) + 2 Strips of Panadol (20 tablets) + 1 Bottle of Syrup
        // Total Panadol base deducted = 20 + 20 = 40 tablets -> remaining 60 tablets
        // Total Syrup base deducted = 1 bottle -> remaining 9 bottles
        var saleRequest = new CreateSaleRequest(
            CustomerId: null,
            UserId: 1,
            InvoiceNumber: "INV-POS-TEST-001",
            PaymentMethod: PaymentMethod.Cash,
            Items: new List<SaleItemRequest>
            {
                new SaleItemRequest(ProductId: 101, Quantity: 1, UnitName: "علبة", ConversionFactor: 20, UnitPrice: 60.00m),
                new SaleItemRequest(ProductId: 101, Quantity: 2, UnitName: "شريط", ConversionFactor: 10, UnitPrice: 30.00m),
                new SaleItemRequest(ProductId: 102, Quantity: 1, UnitName: "زجاجة", ConversionFactor: 1, UnitPrice: 45.00m)
            }
        );

        var sale = await saleService.ProcessSaleAsync(saleRequest);

        // Assert
        Assert.NotNull(sale);
        Assert.Equal(3, sale.SaleItems.Count);
        // Total: 1*60 + 2*30 + 1*45 = 60 + 60 + 45 = 165.00
        Assert.Equal(165.00m, sale.TotalAmount);

        var updatedBatch1 = await context.Batches.FindAsync(201);
        Assert.NotNull(updatedBatch1);
        Assert.Equal(60, updatedBatch1.QuantityOnHand);

        var updatedBatch2 = await context.Batches.FindAsync(202);
        Assert.NotNull(updatedBatch2);
        Assert.Equal(9, updatedBatch2.QuantityOnHand);
    }
}
