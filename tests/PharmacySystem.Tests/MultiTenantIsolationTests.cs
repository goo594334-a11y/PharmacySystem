using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Models.Entities;
using PharmacySystem.Services.Auth;
using Xunit;

namespace PharmacySystem.Tests;

public class MultiTenantIsolationTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(int? pharmacyId)
        {
            PharmacyId = pharmacyId;
        }

        public int? UserId => 1;
        public int? PharmacyId { get; set; }
        public string? PharmacyName => "Test Pharmacy";
        public string? Email => "admin@test.com";
        public string? DisplayName => "Test Admin";
        public UserRole? Role => UserRole.Admin;
        public bool IsAuthenticated => true;
        public bool IsDeveloper { get; set; } = false;
        public bool IsAdmin => true;
        public bool IsPharmacist => false;
        public bool MustChangePassword => false;
    }

    [Fact]
    public async Task NewPharmacy_ShouldHaveZeroProductsAndZeroSales()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // 1. Seed existing data for Pharmacy 1
        var user1Service = new FakeCurrentUserService(pharmacyId: 1);
        using (var dbContext1 = new PharmacyDbContext(options, user1Service))
        {
            var p1 = new Pharmacy { Id = 1, Name = "Existing Pharmacy 1" };
            var cat1 = new Category { Id = 1, PharmacyId = 1, Name = "Painkillers" };
            var prod1 = new Product { Id = 1, PharmacyId = 1, CategoryId = 1, Name = "Aspirin", SellingPrice = 10 };
            var sale1 = new Sale { Id = 1, PharmacyId = 1, InvoiceNumber = "INV-001", TotalAmount = 50, SaleDate = DateTime.UtcNow };

            dbContext1.Pharmacies.Add(p1);
            dbContext1.Categories.Add(cat1);
            dbContext1.Products.Add(prod1);
            dbContext1.Sales.Add(sale1);
            await dbContext1.SaveChangesAsync();
        }

        // 2. User registers for Pharmacy 2 (brand new account)
        var user2Service = new FakeCurrentUserService(pharmacyId: 2);
        using (var dbContext2 = new PharmacyDbContext(options, user2Service))
        {
            var p2 = new Pharmacy { Id = 2, Name = "Brand New Pharmacy 2" };
            dbContext2.Pharmacies.Add(p2);
            await dbContext2.SaveChangesAsync();

            // Act: Query data as Pharmacy 2 user
            var productsCount = await dbContext2.Products.CountAsync();
            var salesCount = await dbContext2.Sales.CountAsync();
            var categoriesCount = await dbContext2.Categories.CountAsync();

            // Assert: Absolutely clean slate, 0 data leaked from Pharmacy 1!
            Assert.Equal(0, productsCount);
            Assert.Equal(0, salesCount);
            Assert.Equal(0, categoriesCount);
        }
    }

    [Fact]
    public async Task NullPharmacyId_OrdinaryUser_ShouldFailClosed_AndReturnZeroRecords()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // Seed data for Pharmacy 1
        using (var seedCtx = new PharmacyDbContext(options, new FakeCurrentUserService(1) { IsDeveloper = true }))
        {
            seedCtx.Pharmacies.Add(new Pharmacy { Id = 1, Name = "Pharm 1" });
            seedCtx.Products.Add(new Product { Id = 10, PharmacyId = 1, Name = "Secret Med", SellingPrice = 20 });
            await seedCtx.SaveChangesAsync();
        }

        // Ordinary user with NULL PharmacyId
        var unassignedUser = new FakeCurrentUserService(null) { IsDeveloper = false };
        using (var testCtx = new PharmacyDbContext(options, unassignedUser))
        {
            var visibleProducts = await testCtx.Products.CountAsync();
            // MUST fail closed: zero products visible
            Assert.Equal(0, visibleProducts);
        }
    }

    [Fact]
    public async Task DeveloperUser_CanViewDataAcrossAllPharmacies()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using (var seedCtx = new PharmacyDbContext(options, new FakeCurrentUserService(1) { IsDeveloper = true }))
        {
            seedCtx.Pharmacies.Add(new Pharmacy { Id = 1, Name = "Pharm 1" });
            seedCtx.Pharmacies.Add(new Pharmacy { Id = 2, Name = "Pharm 2" });
            seedCtx.Products.Add(new Product { Id = 101, PharmacyId = 1, Name = "P1 Med", SellingPrice = 20 });
            seedCtx.Products.Add(new Product { Id = 102, PharmacyId = 2, Name = "P2 Med", SellingPrice = 30 });
            await seedCtx.SaveChangesAsync();
        }

        var devUser = new FakeCurrentUserService(null) { IsDeveloper = true };
        using (var devCtx = new PharmacyDbContext(options, devUser))
        {
            var visibleProducts = await devCtx.Products.CountAsync();
            Assert.Equal(2, visibleProducts);
        }
    }
}
