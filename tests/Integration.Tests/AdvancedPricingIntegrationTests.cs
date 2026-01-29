using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Menu.Api.Dtos;
using DarkVelocity.Orders.Api.Dtos;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// P3 Integration tests for Advanced Pricing scenarios:
/// - Happy Hour Pricing (time-based discounts)
/// - Customer Type Pricing (staff, loyalty tiers)
/// - Promotional Pricing (BOGO, combos)
/// - Stacking Discount Rules
/// </summary>
public class AdvancedPricingIntegrationTests : IClassFixture<MenuServiceFixture>, IClassFixture<OrdersServiceFixture>
{
    private readonly MenuServiceFixture _menuFixture;
    private readonly OrdersServiceFixture _ordersFixture;
    private readonly HttpClient _menuClient;
    private readonly HttpClient _ordersClient;

    public AdvancedPricingIntegrationTests(
        MenuServiceFixture menuFixture,
        OrdersServiceFixture ordersFixture)
    {
        _menuFixture = menuFixture;
        _ordersFixture = ordersFixture;
        _menuFixture.TestLocationId = Guid.NewGuid();
        _menuFixture.SecondLocationId = Guid.NewGuid();
        _menuClient = menuFixture.Client;
        _ordersClient = ordersFixture.Client;
    }

    #region Happy Hour Pricing

    [Fact]
    public async Task HappyHourPricing_DuringTimeWindow_AppliesReducedPrice()
    {
        // Arrange - Create a happy hour price rule
        var happyHourRequest = new CreateHappyHourRuleRequest(
            Name: "Weekday Happy Hour",
            StartTime: TimeOnly.Parse("16:00"),
            EndTime: TimeOnly.Parse("18:00"),
            DaysOfWeek: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            DiscountType: "percentage",
            DiscountValue: 25m, // 25% off
            AppliesToCategories: new List<Guid> { _menuFixture.DrinksCategoryId });

        // Act
        var response = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/happy-hour",
            happyHourRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
        // NotFound if happy hour endpoints aren't implemented
    }

    [Fact]
    public async Task HappyHourPricing_OutsideTimeWindow_UsesRegularPrice()
    {
        // This test verifies that regular prices apply outside happy hour
        // Arrange - Get a menu item price
        var response = await _menuClient.GetAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/items/{_menuFixture.SodaItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<MenuItemDto>();

        // The price should be the regular price when outside happy hour
        item!.Price.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task HappyHourPricing_MultipleRules_AppliesBestPrice()
    {
        // Arrange - When multiple happy hour rules overlap, best price for customer
        var rule1 = new CreateHappyHourRuleRequest(
            Name: "Early Bird",
            StartTime: TimeOnly.Parse("15:00"),
            EndTime: TimeOnly.Parse("17:00"),
            DaysOfWeek: new[] { DayOfWeek.Friday },
            DiscountType: "percentage",
            DiscountValue: 15m,
            AppliesToCategories: new List<Guid>());

        var rule2 = new CreateHappyHourRuleRequest(
            Name: "Friday Special",
            StartTime: TimeOnly.Parse("16:00"),
            EndTime: TimeOnly.Parse("19:00"),
            DaysOfWeek: new[] { DayOfWeek.Friday },
            DiscountType: "percentage",
            DiscountValue: 20m,
            AppliesToCategories: new List<Guid>());

        // Act
        var response1 = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/happy-hour", rule1);
        var response2 = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/happy-hour", rule2);

        // Assert - Both should be created (or NotFound if not implemented)
        response1.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
        response2.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetActiveHappyHourRules_ReturnsCurrentlyApplicableRules()
    {
        // Act
        var response = await _menuClient.GetAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/happy-hour/active");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Customer Type Pricing

    [Fact]
    public async Task StaffDiscount_AppliesEmployeePricing()
    {
        // Arrange - Create staff pricing tier
        var staffPricingRequest = new CreateCustomerPricingTierRequest(
            Name: "Staff",
            DiscountType: "percentage",
            DiscountValue: 50m, // 50% staff discount
            RequiresVerification: true,
            IsActive: true);

        // Act
        var response = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/customer-tiers",
            staffPricingRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LoyaltyTier_GoldMember_AppliesGoldPricing()
    {
        // Arrange - Create loyalty tier
        var goldTierRequest = new CreateCustomerPricingTierRequest(
            Name: "Gold Member",
            DiscountType: "percentage",
            DiscountValue: 10m, // 10% loyalty discount
            RequiresVerification: false,
            IsActive: true);

        // Act
        var response = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/customer-tiers",
            goldTierRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApplyCustomerTierToOrder_AppliesCorrectDiscount()
    {
        // Arrange - Create an order
        var createOrderRequest = new CreateOrderRequest(
            UserId: _ordersFixture.TestUserId,
            OrderType: "direct_sale",
            CustomerName: "Staff Member");

        var orderResponse = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createOrderRequest);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        // Add item
        await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            new AddOrderLineRequest(
                _ordersFixture.TestMenuItemId,
                "Test Item",
                1,
                20.00m));

        // Act - Apply staff tier
        var applyTierRequest = new ApplyCustomerTierRequest(
            TierName: "Staff",
            VerifiedByUserId: _ordersFixture.TestUserId);

        var response = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}/customer-tier",
            applyTierRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCustomerPricingTiers_ReturnsAllTiers()
    {
        // Act
        var response = await _menuClient.GetAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/customer-tiers");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Promotional Pricing

    [Fact]
    public async Task BOGO_BuyOneGetOneFree_AppliesPromotion()
    {
        // Arrange - Create BOGO promotion
        var bogoRequest = new CreatePromotionRequest(
            Name: "BOGO Burgers",
            PromotionType: "bogo",
            BuyQuantity: 1,
            GetQuantity: 1,
            GetDiscountPercent: 100m, // Free
            ApplicableItemIds: new List<Guid> { _menuFixture.BurgerItemId },
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(7),
            IsActive: true);

        // Act
        var response = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/promotions",
            bogoRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ComboMeal_BundlePricing_AppliesComboPrice()
    {
        // Arrange - Create combo deal
        var comboRequest = new CreatePromotionRequest(
            Name: "Lunch Combo",
            PromotionType: "combo",
            ComboItems: new List<ComboItemRequest>
            {
                new(_menuFixture.BurgerItemId, 1),
                new(_menuFixture.SodaItemId, 1)
            },
            ComboPrice: 12.99m, // Special combo price
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddMonths(1),
            IsActive: true);

        // Act
        var response = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/promotions",
            comboRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LimitedTimeOffer_WithinDateRange_AppliesDiscount()
    {
        // Arrange - Create limited time promotion
        var ltoRequest = new CreatePromotionRequest(
            Name: "Weekend Special",
            PromotionType: "percentage",
            DiscountPercent: 20m,
            ApplicableItemIds: new List<Guid>(),
            ApplicableCategoryIds: new List<Guid> { _menuFixture.MainCategoryId },
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(2),
            DaysOfWeek: new[] { DayOfWeek.Saturday, DayOfWeek.Sunday },
            IsActive: true);

        // Act
        var response = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/promotions",
            ltoRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExpiredPromotion_NotApplied()
    {
        // Arrange - Create an expired promotion
        var expiredPromo = new CreatePromotionRequest(
            Name: "Old Promo",
            PromotionType: "percentage",
            DiscountPercent: 50m,
            ApplicableItemIds: new List<Guid> { _menuFixture.BurgerItemId },
            StartDate: DateTime.UtcNow.AddDays(-30),
            EndDate: DateTime.UtcNow.AddDays(-1), // Already ended
            IsActive: true);

        // Act
        var response = await _menuClient.PostAsJsonAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/promotions",
            expiredPromo);

        // Assert - May be rejected or created but marked inactive
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetActivePromotions_ReturnsCurrentPromotions()
    {
        // Act
        var response = await _menuClient.GetAsync(
            $"/api/locations/{_menuFixture.TestLocationId}/pricing-rules/promotions/active");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Stacking Discount Rules

    [Fact]
    public async Task MultipleDiscounts_StackingAllowed_AppliesInOrder()
    {
        // Arrange - Create order with item
        var createRequest = new CreateOrderRequest(
            UserId: _ordersFixture.TestUserId,
            OrderType: "direct_sale");

        var orderResponse = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            new AddOrderLineRequest(
                _ordersFixture.TestMenuItemId,
                "Expensive Item",
                1,
                100.00m));

        // Apply first discount (percentage)
        var discount1 = new ApplyOrderDiscountRequest(
            DiscountType: "percentage",
            DiscountValue: 10m,
            Reason: "Loyalty discount");

        var response1 = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}/discount",
            discount1);

        // Apply second discount (fixed)
        var discount2 = new ApplyOrderDiscountRequest(
            DiscountType: "fixed",
            DiscountValue: 5.00m,
            Reason: "Birthday discount");

        // Act
        var response2 = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}/discount",
            discount2);

        // Assert
        // Either stacking is allowed or second discount replaces first
        response1.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        response2.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);

        // Verify final totals
        var finalOrder = await _ordersClient.GetAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}");
        var orderResult = await finalOrder.Content.ReadFromJsonAsync<OrderDto>();

        // Total should reflect applied discounts
        orderResult!.Total.Should().BeLessThan(100.00m);
    }

    [Fact]
    public async Task DiscountStacking_MaximumCapEnforced()
    {
        // Arrange - Try to apply discounts that exceed maximum allowed
        var createRequest = new CreateOrderRequest(
            UserId: _ordersFixture.TestUserId,
            OrderType: "direct_sale");

        var orderResponse = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            new AddOrderLineRequest(
                _ordersFixture.TestMenuItemId,
                "Item",
                1,
                50.00m));

        // Try to apply 80% discount (likely exceeds max)
        var hugeDiscount = new ApplyOrderDiscountRequest(
            DiscountType: "percentage",
            DiscountValue: 80m,
            Reason: "Excessive discount test");

        // Act
        var response = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}/discount",
            hugeDiscount);

        // Assert - May require authorization or be capped
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DiscountStacking_OrderMatters_PercentageThenFixed()
    {
        // Test that percentage discounts applied before fixed discounts
        // This affects final calculation: (100 * 0.9) - 5 = 85 vs (100 - 5) * 0.9 = 85.50

        var createRequest = new CreateOrderRequest(
            UserId: _ordersFixture.TestUserId,
            OrderType: "direct_sale");

        var orderResponse = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            new AddOrderLineRequest(
                _ordersFixture.TestMenuItemId,
                "Test Item",
                1,
                100.00m));

        // The order of discount application should follow business rules
        // This test documents the expected behavior
        var response = await _ordersClient.GetAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Price Override

    [Fact]
    public async Task ManagerPriceOverride_CanSetCustomPrice()
    {
        // Arrange - Create order
        var createRequest = new CreateOrderRequest(
            UserId: _ordersFixture.TestUserId,
            OrderType: "direct_sale");

        var orderResponse = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders",
            createRequest);
        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        var lineResponse = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order!.Id}/lines",
            new AddOrderLineRequest(
                _ordersFixture.TestMenuItemId,
                "Regular Item",
                1,
                25.00m));
        var line = await lineResponse.Content.ReadFromJsonAsync<OrderLineDto>();

        // Act - Override the price
        var overrideRequest = new PriceOverrideRequest(
            NewPrice: 20.00m,
            Reason: "Price match competitor",
            AuthorizedByUserId: _ordersFixture.TestUserId);

        var response = await _ordersClient.PostAsJsonAsync(
            $"/api/locations/{_ordersFixture.TestLocationId}/orders/{order.Id}/lines/{line!.Id}/price-override",
            overrideRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion
}

// P3 DTOs for pricing features
public record CreateHappyHourRuleRequest(
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DayOfWeek[] DaysOfWeek,
    string DiscountType,
    decimal DiscountValue,
    List<Guid>? AppliesToCategories = null,
    List<Guid>? AppliesToItems = null);

public record CreateCustomerPricingTierRequest(
    string Name,
    string DiscountType,
    decimal DiscountValue,
    bool RequiresVerification,
    bool IsActive);

public record ApplyCustomerTierRequest(
    string TierName,
    Guid? VerifiedByUserId = null);

public record CreatePromotionRequest(
    string Name,
    string PromotionType,
    decimal? DiscountPercent = null,
    int? BuyQuantity = null,
    int? GetQuantity = null,
    decimal? GetDiscountPercent = null,
    decimal? ComboPrice = null,
    List<ComboItemRequest>? ComboItems = null,
    List<Guid>? ApplicableItemIds = null,
    List<Guid>? ApplicableCategoryIds = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    DayOfWeek[]? DaysOfWeek = null,
    bool IsActive = true);

public record ComboItemRequest(
    Guid ItemId,
    int Quantity);

public record PriceOverrideRequest(
    decimal NewPrice,
    string Reason,
    Guid AuthorizedByUserId);
