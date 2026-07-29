using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Application.Taxation.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Inventory;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.Tests.Carts;

public class CartServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Adding_a_simple_product_creates_a_cart_with_one_line()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 2));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Quantity.Should().Be(2);
        result.Value.TotalItemCount.Should().Be(2);
        result.Value.Subtotal.Should().Be(20);
    }

    [Fact]
    public async Task Adding_the_same_product_again_increments_the_existing_line_instead_of_duplicating()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();

        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 2));
        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 3));

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Adding_a_product_that_requires_a_variant_without_selecting_one_is_rejected()
    {
        var (product, _, _) = await SeedProductWithVariantsAsync();

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, null, 1));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Adding_an_inactive_variant_is_rejected()
    {
        var (product, red, _) = await SeedProductWithVariantsAsync();
        red.IsActive = false;
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, red.Id, 1));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Specifying_a_variant_for_a_product_with_no_variants_is_rejected()
    {
        var product = await SeedProductAsync();

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, 999999, 1));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Adding_an_unpublished_product_is_not_found()
    {
        var product = await SeedProductAsync(isPublished: false);

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, null, 1));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Requesting_more_than_available_stock_is_rejected()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 3, reserved: 0, allowBackorder: false);

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, null, 4));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
        result.FirstError.Message.Should().Contain("3");
    }

    [Fact]
    public async Task Requesting_exactly_the_available_stock_succeeds()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 3, reserved: 0, allowBackorder: false);

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, null, 3));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_product_with_no_inventory_record_allows_any_quantity()
    {
        var product = await SeedProductAsync();

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, null, 500));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Backorder_allowed_inventory_allows_exceeding_available_stock()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 1, reserved: 0, allowBackorder: true);

        var result = await _harness.CartService.AddItemAsync(GuestOwner(), new AddCartItemRequest(product.Id, null, 10));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Adding_combined_quantity_across_two_calls_is_still_checked_against_stock()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 3, reserved: 0, allowBackorder: false);
        var owner = GuestOwner();

        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 2));
        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 2));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Updating_quantity_changes_the_line_and_recomputes_totals()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        var result = await _harness.CartService.UpdateQuantityAsync(owner, new UpdateCartItemQuantityRequest(added.Value.Items[0].Id, 4));

        result.Value.Items[0].Quantity.Should().Be(4);
        result.Value.TotalItemCount.Should().Be(4);
        result.Value.Subtotal.Should().Be(40);
    }

    [Fact]
    public async Task Updating_a_nonexistent_cart_item_is_not_found()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        var result = await _harness.CartService.UpdateQuantityAsync(owner, new UpdateCartItemQuantityRequest(999999, 2));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Updating_quantity_beyond_available_stock_is_rejected()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 3, reserved: 0, allowBackorder: false);
        var owner = GuestOwner();
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 2));

        var result = await _harness.CartService.UpdateQuantityAsync(owner, new UpdateCartItemQuantityRequest(added.Value.Items[0].Id, 4));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Updating_an_item_whose_product_became_unpublished_is_rejected()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        product.IsPublished = false;
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CartService.UpdateQuantityAsync(owner, new UpdateCartItemQuantityRequest(added.Value.Items[0].Id, 2));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Removing_an_item_deletes_it_and_recomputes_totals()
    {
        var first = await SeedProductAsync(name: "First");
        var second = await SeedProductAsync(name: "Second");
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(first.Id, null, 1));
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(second.Id, null, 1));

        var result = await _harness.CartService.RemoveItemAsync(owner, added.Value.Items[1].Id);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ProductId.Should().Be(first.Id);
    }

    [Fact]
    public async Task Removing_a_nonexistent_item_is_not_found()
    {
        var result = await _harness.CartService.RemoveItemAsync(GuestOwner(), 999999);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Clearing_the_cart_removes_every_item()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        var result = await _harness.CartService.ClearCartAsync(owner);

        result.Items.Should().BeEmpty();
        result.TotalItemCount.Should().Be(0);
    }

    [Fact]
    public async Task An_owner_with_no_cart_gets_an_empty_dto_without_creating_a_row()
    {
        var result = await _harness.CartService.GetCartAsync(GuestOwner());

        result.Id.Should().BeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Two_different_guest_owners_have_fully_isolated_carts()
    {
        var product = await SeedProductAsync();
        var ownerA = GuestOwner();
        var ownerB = GuestOwner();

        await _harness.CartService.AddItemAsync(ownerA, new AddCartItemRequest(product.Id, null, 1));
        var cartB = await _harness.CartService.GetCartAsync(ownerB);

        cartB.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task A_soft_deleted_products_line_still_displays_but_is_marked_unavailable_and_excluded_from_totals()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 2));

        product.IsDeleted = true;
        await _harness.DbContext.SaveChangesAsync();

        var cart = await _harness.CartService.GetCartAsync(owner);

        cart.Items.Should().ContainSingle();
        cart.Items[0].ProductName.Should().Be(product.Name);
        cart.Items[0].IsAvailable.Should().BeFalse();
        cart.TotalItemCount.Should().Be(0);
        cart.Subtotal.Should().Be(0);
    }

    [Fact]
    public async Task Subtotal_sums_only_available_lines_across_multiple_items()
    {
        var available = await SeedProductAsync(name: "Available", price: 10m);
        var unavailable = await SeedProductAsync(name: "Unavailable", price: 25m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(available.Id, null, 2));
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(unavailable.Id, null, 1));

        unavailable.IsActive = false;
        await _harness.DbContext.SaveChangesAsync();

        var cart = await _harness.CartService.GetCartAsync(owner);

        cart.TotalItemCount.Should().Be(2);
        cart.Subtotal.Should().Be(20);
    }

    [Fact]
    public async Task Merging_with_no_existing_user_cart_reassigns_the_guest_cart()
    {
        var product = await SeedProductAsync();
        var guestToken = Guid.NewGuid().ToString("N");
        await _harness.CartService.AddItemAsync(CartOwner.ForGuest(guestToken), new AddCartItemRequest(product.Id, null, 2));

        var merged = await _harness.CartService.MergeGuestCartIntoUserCartAsync(guestToken, "user-1");

        merged.Items.Should().ContainSingle();
        merged.Items[0].Quantity.Should().Be(2);
        (await _harness.CartService.GetCartAsync(CartOwner.ForGuest(guestToken))).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Merging_combines_matching_lines_and_moves_non_matching_lines()
    {
        var shared = await SeedProductAsync(name: "Shared");
        var guestOnly = await SeedProductAsync(name: "GuestOnly");
        var guestToken = Guid.NewGuid().ToString("N");
        await _harness.CartService.AddItemAsync(CartOwner.ForGuest(guestToken), new AddCartItemRequest(shared.Id, null, 2));
        await _harness.CartService.AddItemAsync(CartOwner.ForGuest(guestToken), new AddCartItemRequest(guestOnly.Id, null, 1));
        await _harness.CartService.AddItemAsync(CartOwner.ForUser("user-1"), new AddCartItemRequest(shared.Id, null, 3));

        var merged = await _harness.CartService.MergeGuestCartIntoUserCartAsync(guestToken, "user-1");

        merged.Items.Should().HaveCount(2);
        merged.Items.Should().Contain(i => i.ProductId == shared.Id && i.Quantity == 5);
        merged.Items.Should().Contain(i => i.ProductId == guestOnly.Id && i.Quantity == 1);
    }

    [Fact]
    public async Task Merging_caps_a_combined_quantity_to_available_stock()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 4, reserved: 0, allowBackorder: false);
        var guestToken = Guid.NewGuid().ToString("N");
        await _harness.CartService.AddItemAsync(CartOwner.ForGuest(guestToken), new AddCartItemRequest(product.Id, null, 3));
        await _harness.CartService.AddItemAsync(CartOwner.ForUser("user-1"), new AddCartItemRequest(product.Id, null, 1));

        var merged = await _harness.CartService.MergeGuestCartIntoUserCartAsync(guestToken, "user-1");

        merged.Items.Should().ContainSingle();
        merged.Items[0].Quantity.Should().Be(4);
    }

    [Fact]
    public async Task Merging_with_no_guest_cart_is_a_noop_returning_the_users_existing_cart()
    {
        var product = await SeedProductAsync();
        await _harness.CartService.AddItemAsync(CartOwner.ForUser("user-1"), new AddCartItemRequest(product.Id, null, 1));

        var merged = await _harness.CartService.MergeGuestCartIntoUserCartAsync(Guid.NewGuid().ToString("N"), "user-1");

        merged.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task A_price_change_since_adding_is_flagged_without_affecting_the_line_total()
    {
        var product = await SeedProductAsync(price: 10m);
        var owner = GuestOwner();
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 2));
        added.Value.Items[0].PriceChanged.Should().BeFalse();

        product.SellingPrice = 15m;
        await _harness.DbContext.SaveChangesAsync();

        var cart = await _harness.CartService.GetCartAsync(owner);

        cart.Items[0].PriceChanged.Should().BeTrue();
        cart.Items[0].PreviousUnitPrice.Should().Be(10m);
        cart.Items[0].UnitPrice.Should().Be(15m);
        cart.Items[0].LineTotal.Should().Be(30m);
    }

    [Fact]
    public async Task Updating_quantity_re_stamps_the_price_and_clears_the_price_changed_flag()
    {
        var product = await SeedProductAsync(price: 10m);
        var owner = GuestOwner();
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        product.SellingPrice = 15m;
        await _harness.DbContext.SaveChangesAsync();

        var updated = await _harness.CartService.UpdateQuantityAsync(owner, new UpdateCartItemQuantityRequest(added.Value.Items[0].Id, 2));

        updated.Value.Items[0].PriceChanged.Should().BeFalse();
    }

    [Fact]
    public async Task Quantity_exceeding_shrunk_stock_is_flagged_without_being_auto_changed()
    {
        var product = await SeedProductAsync();
        await SeedInventoryAsync(product.Id, null, onHand: 10, reserved: 0, allowBackorder: false);
        var owner = GuestOwner();
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 8));

        var inventoryItem = await _harness.DbContext.InventoryItems.FirstAsync(i => i.ProductId == product.Id);
        inventoryItem.QuantityOnHand = 3;
        await _harness.DbContext.SaveChangesAsync();

        var cart = await _harness.CartService.GetCartAsync(owner);

        cart.Items[0].QuantityExceedsStock.Should().BeTrue();
        cart.Items[0].Quantity.Should().Be(8);
        cart.Items[0].AvailableQuantity.Should().Be(3);
    }

    [Fact]
    public async Task Applying_a_valid_coupon_sets_the_discount_and_total()
    {
        var product = await SeedProductAsync(price: 100m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        await SeedPromotionAsync("SAVE10", 10m);

        var result = await _harness.CartService.ApplyCouponAsync(owner, "SAVE10");

        result.IsSuccess.Should().BeTrue();
        result.Value.AppliedCouponCode.Should().Be("SAVE10");
        result.Value.PromotionDiscount.Should().Be(10m);
        result.Value.Total.Should().Be(90m);
    }

    [Fact]
    public async Task Applying_an_unknown_coupon_code_fails_without_changing_the_cart()
    {
        var product = await SeedProductAsync(price: 100m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        var result = await _harness.CartService.ApplyCouponAsync(owner, "NOPE");

        result.IsFailure.Should().BeTrue();
        var cart = await _harness.CartService.GetCartAsync(owner);
        cart.AppliedCouponCode.Should().BeNull();
    }

    [Fact]
    public async Task Applying_a_coupon_to_an_empty_cart_is_rejected()
    {
        await SeedPromotionAsync("SAVE10", 10m);

        var result = await _harness.CartService.ApplyCouponAsync(GuestOwner(), "SAVE10");

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Removing_a_coupon_clears_the_discount()
    {
        var product = await SeedProductAsync(price: 100m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        await SeedPromotionAsync("SAVE10", 10m);
        await _harness.CartService.ApplyCouponAsync(owner, "SAVE10");

        var cart = await _harness.CartService.RemoveCouponAsync(owner);

        cart.AppliedCouponCode.Should().BeNull();
        cart.PromotionDiscount.Should().Be(0);
        cart.Total.Should().Be(cart.Subtotal);
    }

    [Fact]
    public async Task A_promotion_deactivated_after_being_applied_is_silently_cleared_on_the_next_read()
    {
        var product = await SeedProductAsync(price: 100m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        var promotion = await SeedPromotionAsync("SAVE10", 10m);
        await _harness.CartService.ApplyCouponAsync(owner, "SAVE10");

        await _harness.PromotionService.DeactivateAsync(promotion.Id);
        var cart = await _harness.CartService.GetCartAsync(owner);

        cart.AppliedCouponCode.Should().BeNull();
        cart.PromotionDiscount.Should().Be(0);
        cart.Total.Should().Be(cart.Subtotal);
    }

    [Fact]
    public async Task A_taxable_item_shows_estimated_tax_when_a_rate_is_configured_for_the_store_default_jurisdiction()
    {
        // The harness configures Store:DefaultTaxCountryCode=US, Store:DefaultTaxRegionCode=CA.
        var product = await SeedProductAsync(price: 100m);
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        result.Value.EstimatedTaxRateConfigured.Should().BeTrue();
        result.Value.EstimatedTax.Should().Be(10m);
    }

    [Fact]
    public async Task A_non_taxable_item_is_excluded_from_the_tax_estimate()
    {
        var product = await SeedProductAsync(price: 100m, isTaxable: false);
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        result.Value.EstimatedTaxRateConfigured.Should().BeFalse();
        result.Value.EstimatedTax.Should().Be(0);
    }

    [Fact]
    public async Task Estimated_tax_is_zero_and_unconfigured_when_no_rate_exists_for_the_store_default_jurisdiction()
    {
        var product = await SeedProductAsync(price: 100m);
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        result.Value.EstimatedTaxRateConfigured.Should().BeFalse();
        result.Value.EstimatedTax.Should().Be(0);
    }

    [Fact]
    public async Task Estimated_tax_sums_across_lines_with_different_tax_categories()
    {
        var standard = await SeedProductAsync(name: "Standard item", price: 100m, taxCategory: "Standard");
        var reduced = await SeedProductAsync(name: "Reduced item", price: 50m, taxCategory: "Reduced");
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Reduced", 5m, true));
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(standard.Id, null, 1));

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(reduced.Id, null, 1));

        result.Value.EstimatedTaxRateConfigured.Should().BeTrue();
        result.Value.EstimatedTax.Should().Be(12.5m); // 10 + 2.5
    }

    [Fact]
    public async Task A_cart_shows_estimated_shipping_when_a_method_is_configured_for_the_store_default_jurisdiction()
    {
        // The harness configures Store:DefaultShippingCountryCode=US, Store:DefaultShippingRegionCode=CA.
        var product = await SeedProductAsync(price: 50m, weight: 2m);
        await _harness.ShippingService.CreateAsync(new CreateShippingMethodRequest(
            "Standard", null, "US", "CA", 5m, 1m, null, null, null, 0, true));
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        result.Value.EstimatedShippingRateConfigured.Should().BeTrue();
        result.Value.EstimatedShipping.Should().Be(7m); // 5 + 1*2
    }

    [Fact]
    public async Task A_product_with_no_recorded_weight_contributes_zero_to_the_shipping_estimate()
    {
        var product = await SeedProductAsync(price: 50m, weight: null);
        await _harness.ShippingService.CreateAsync(new CreateShippingMethodRequest(
            "Standard", null, "US", "CA", 5m, 1m, null, null, null, 0, true));
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        result.Value.EstimatedShippingRateConfigured.Should().BeTrue();
        result.Value.EstimatedShipping.Should().Be(5m); // 5 + 1*0
    }

    [Fact]
    public async Task Estimated_shipping_is_zero_and_unconfigured_when_no_method_exists_for_the_store_default_jurisdiction()
    {
        var product = await SeedProductAsync(price: 50m, weight: 2m);
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        result.Value.EstimatedShippingRateConfigured.Should().BeFalse();
        result.Value.EstimatedShipping.Should().Be(0);
    }

    [Fact]
    public async Task Estimated_shipping_sums_weight_across_multiple_lines()
    {
        var heavy = await SeedProductAsync(name: "Heavy item", price: 50m, weight: 3m);
        var light = await SeedProductAsync(name: "Light item", price: 20m, weight: 1m);
        await _harness.ShippingService.CreateAsync(new CreateShippingMethodRequest(
            "Standard", null, "US", "CA", 5m, 2m, null, null, null, 0, true));
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(heavy.Id, null, 1));

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(light.Id, null, 1));

        result.Value.EstimatedShippingRateConfigured.Should().BeTrue();
        result.Value.EstimatedShipping.Should().Be(13m); // 5 + 2*(3+1)
    }

    [Fact]
    public async Task Applying_a_coupon_reduces_the_taxable_amount_the_estimated_tax_is_computed_against()
    {
        var product = await SeedProductAsync(price: 100m);
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        await SeedPromotionAsync("SAVE10", 10m);

        var result = await _harness.CartService.ApplyCouponAsync(owner, "SAVE10");

        result.Value.PromotionDiscount.Should().Be(10m);
        result.Value.EstimatedTax.Should().Be(9m); // 10% of the post-discount 90, not the pre-discount 100
    }

    [Fact]
    public async Task Applying_a_coupon_reduces_the_subtotal_the_free_shipping_threshold_is_checked_against()
    {
        var product = await SeedProductAsync(price: 100m, weight: 2m);
        await _harness.ShippingService.CreateAsync(new CreateShippingMethodRequest(
            "Standard", null, "US", "CA", 5m, 1m, FreeShippingThreshold: 90m, null, null, 0, true));
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        await SeedPromotionAsync("SAVE10", 10m);

        var result = await _harness.CartService.ApplyCouponAsync(owner, "SAVE10");

        // Pre-discount subtotal (100) would clear the 90 threshold too, but the
        // post-discount subtotal (90) exactly meeting it proves it's the
        // post-discount amount driving the free-shipping check.
        result.Value.EstimatedShipping.Should().Be(0m);
    }

    [Fact]
    public async Task EstimatedGrandTotal_is_the_discounted_total_plus_estimated_tax_and_shipping()
    {
        var product = await SeedProductAsync(price: 100m, weight: 2m);
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        await _harness.ShippingService.CreateAsync(new CreateShippingMethodRequest(
            "Standard", null, "US", "CA", 5m, 1m, null, null, null, 0, true));
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        await SeedPromotionAsync("SAVE10", 10m);

        var result = await _harness.CartService.ApplyCouponAsync(owner, "SAVE10");

        // Total 90 + tax 9 (10% of 90) + shipping 7 (5 + 1*2) = 106.
        result.Value.EstimatedTax.Should().Be(9m);
        result.Value.EstimatedShipping.Should().Be(7m);
        result.Value.EstimatedGrandTotal.Should().Be(106m);
    }

    [Fact]
    public async Task EstimatedGrandTotal_with_no_promotion_tax_or_shipping_configured_equals_the_total()
    {
        var product = await SeedProductAsync(price: 100m);
        var owner = GuestOwner();

        var result = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));

        result.Value.EstimatedGrandTotal.Should().Be(result.Value.Total);
    }

    [Fact]
    public async Task GetCheckoutInputAsync_with_no_cart_at_all_is_rejected_as_empty()
    {
        var result = await _harness.CartService.GetCheckoutInputAsync(GuestOwner());

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("cart.empty");
    }

    [Fact]
    public async Task GetCheckoutInputAsync_with_an_empty_cart_is_rejected_as_empty()
    {
        var product = await SeedProductAsync();
        var owner = GuestOwner();
        var added = await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        await _harness.CartService.RemoveItemAsync(owner, added.Value.Items[0].Id);

        var result = await _harness.CartService.GetCheckoutInputAsync(owner);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("cart.empty");
    }

    [Fact]
    public async Task GetCheckoutInputAsync_with_only_unavailable_lines_is_rejected_as_empty()
    {
        var product = await SeedProductAsync(price: 50m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        product.IsActive = false;
        await _harness.DbContext.SaveChangesAsync();

        var result = await _harness.CartService.GetCheckoutInputAsync(owner);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("cart.empty");
    }

    [Fact]
    public async Task GetCheckoutInputAsync_returns_a_checkout_line_per_available_item()
    {
        var product = await SeedProductAsync(name: "Widget", price: 25m, weight: 2m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 3));

        var result = await _harness.CartService.GetCheckoutInputAsync(owner);

        result.IsSuccess.Should().BeTrue();
        result.Value.Lines.Should().ContainSingle();
        result.Value.Lines[0].ProductId.Should().Be(product.Id);
        result.Value.Lines[0].LineTotal.Should().Be(75m);
        result.Value.Lines[0].TotalWeight.Should().Be(6m); // 2kg * 3
        result.Value.AppliedPromotionId.Should().BeNull();
    }

    [Fact]
    public async Task GetCheckoutInputAsync_returns_the_resolved_applied_promotion_id()
    {
        var product = await SeedProductAsync(price: 100m);
        var owner = GuestOwner();
        await _harness.CartService.AddItemAsync(owner, new AddCartItemRequest(product.Id, null, 1));
        var promotion = await SeedPromotionAsync("SAVE10", 10m);
        await _harness.CartService.ApplyCouponAsync(owner, "SAVE10");

        var result = await _harness.CartService.GetCheckoutInputAsync(owner);

        result.IsSuccess.Should().BeTrue();
        result.Value.AppliedPromotionId.Should().Be(promotion.Id);
    }

    private async Task<PromotionDto> SeedPromotionAsync(string couponCode, decimal percentageDiscount)
    {
        var result = await _harness.PromotionService.CreateAsync(new CreatePromotionRequest(
            "Test promotion", null, couponCode, "Percentage", percentageDiscount, "EntireOrder",
            null, null, null, null, null, DateTime.UtcNow.AddDays(-1), null, null, null, true));
        return result.Value;
    }

    private static CartOwner GuestOwner() => CartOwner.ForGuest(Guid.NewGuid().ToString("N"));

    private async Task<Category> SeedCategoryAsync()
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();
        return category;
    }

    private async Task<Product> SeedProductAsync(
        bool isActive = true, bool isPublished = true, string name = "Widget", decimal price = 10m,
        string taxCategory = "Standard", bool isTaxable = true, decimal? weight = null)
    {
        var category = await SeedCategoryAsync();
        var product = new Product
        {
            Name = name,
            Slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = price / 2,
            SellingPrice = price,
            IsActive = isActive,
            IsPublished = isPublished,
            PublishedAtUtc = DateTime.UtcNow,
            TaxCategory = taxCategory,
            IsTaxable = isTaxable,
            Weight = weight,
        };
        _harness.DbContext.Products.Add(product);
        await _harness.DbContext.SaveChangesAsync();
        return product;
    }

    private async Task<(Product Product, ProductVariant Red, ProductVariant Blue)> SeedProductWithVariantsAsync()
    {
        var product = await SeedProductAsync();

        var colorAttribute = new ProductAttribute { Name = "Color" };
        _harness.DbContext.ProductAttributes.Add(colorAttribute);
        await _harness.DbContext.SaveChangesAsync();

        var redValue = new ProductAttributeValue { ProductAttributeId = colorAttribute.Id, Value = "Red" };
        var blueValue = new ProductAttributeValue { ProductAttributeId = colorAttribute.Id, Value = "Blue" };
        _harness.DbContext.ProductAttributeValues.AddRange(redValue, blueValue);
        await _harness.DbContext.SaveChangesAsync();

        var red = new ProductVariant
        {
            ProductId = product.Id,
            SKU = $"SKU-RED-{Guid.NewGuid():N}",
            CombinationKey = ProductVariant.BuildCombinationKey(new[] { redValue.Id }),
            IsActive = true,
        };
        var blue = new ProductVariant
        {
            ProductId = product.Id,
            SKU = $"SKU-BLUE-{Guid.NewGuid():N}",
            CombinationKey = ProductVariant.BuildCombinationKey(new[] { blueValue.Id }),
            IsActive = true,
        };
        _harness.DbContext.ProductVariants.AddRange(red, blue);
        await _harness.DbContext.SaveChangesAsync();

        _harness.DbContext.Set<ProductVariantAttributeValue>().AddRange(
            new ProductVariantAttributeValue { ProductVariantId = red.Id, ProductAttributeValueId = redValue.Id },
            new ProductVariantAttributeValue { ProductVariantId = blue.Id, ProductAttributeValueId = blueValue.Id });
        await _harness.DbContext.SaveChangesAsync();

        return (product, red, blue);
    }

    private async Task SeedInventoryAsync(int productId, int? variantId, int onHand, int reserved, bool allowBackorder)
    {
        var warehouse = new Warehouse { Name = "Main", Code = $"WH-{Guid.NewGuid():N}", IsActive = true };
        _harness.DbContext.Warehouses.Add(warehouse);
        await _harness.DbContext.SaveChangesAsync();

        _harness.DbContext.InventoryItems.Add(new InventoryItem
        {
            WarehouseId = warehouse.Id,
            ProductId = productId,
            ProductVariantId = variantId,
            QuantityOnHand = onHand,
            QuantityReserved = reserved,
            AllowBackorder = allowBackorder,
            LastStockUpdateUtc = DateTime.UtcNow,
        });
        await _harness.DbContext.SaveChangesAsync();
    }
}
