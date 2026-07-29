using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class ShippingServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_method_succeeds_with_valid_data()
    {
        var result = await _harness.ShippingService.CreateAsync(StandardRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Standard Shipping");
    }

    [Fact]
    public async Task Creating_a_duplicate_name_for_the_same_jurisdiction_is_rejected()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest());

        var result = await _harness.ShippingService.CreateAsync(StandardRequest() with { BaseRate = 10m });

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task The_same_name_can_be_used_in_a_different_country()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest());

        var result = await _harness.ShippingService.CreateAsync(StandardRequest() with { CountryCode = "CA" });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Updating_a_method_persists_changes()
    {
        var created = await _harness.ShippingService.CreateAsync(StandardRequest());

        var result = await _harness.ShippingService.UpdateAsync(new UpdateShippingMethodRequest(
            created.Value.Id, "Standard Shipping", null, "US", null, 8m, 2m, null, null, null, 0, false));

        result.IsSuccess.Should().BeTrue();
        result.Value.BaseRate.Should().Be(8m);
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_method_soft_deletes_it_and_it_no_longer_appears_in_the_paged_list()
    {
        var created = await _harness.ShippingService.CreateAsync(StandardRequest());

        await _harness.ShippingService.DeleteAsync(created.Value.Id);

        var page = await _harness.ShippingService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().NotContain(m => m.Id == created.Value.Id);

        var deletedPage = await _harness.ShippingService.GetPagedAsync(new PagedQuery { OnlyDeleted = true });
        deletedPage.Value.Items.Should().Contain(m => m.Id == created.Value.Id);
    }

    [Fact]
    public async Task Restoring_a_deleted_method_makes_it_visible_again()
    {
        var created = await _harness.ShippingService.CreateAsync(StandardRequest());
        await _harness.ShippingService.DeleteAsync(created.Value.Id);

        await _harness.ShippingService.RestoreAsync(created.Value.Id);

        var page = await _harness.ShippingService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().Contain(m => m.Id == created.Value.Id);
    }

    [Fact]
    public async Task Creating_a_method_matching_a_deleted_ones_name_and_jurisdiction_is_rejected_with_a_restore_hint()
    {
        var created = await _harness.ShippingService.CreateAsync(StandardRequest());
        await _harness.ShippingService.DeleteAsync(created.Value.Id);

        var result = await _harness.ShippingService.CreateAsync(StandardRequest() with { BaseRate = 10m });

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Message.Should().Contain("deleted");
    }

    [Fact]
    public async Task Updating_a_method_to_match_a_deleted_ones_name_and_jurisdiction_is_rejected()
    {
        var deleted = await _harness.ShippingService.CreateAsync(StandardRequest());
        await _harness.ShippingService.DeleteAsync(deleted.Value.Id);
        var other = await _harness.ShippingService.CreateAsync(StandardRequest() with { CountryCode = "CA" });

        var result = await _harness.ShippingService.UpdateAsync(new UpdateShippingMethodRequest(
            other.Value.Id, "Standard Shipping", null, "US", null, 8m, 2m, null, null, null, 0, true));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Cost_is_base_rate_plus_rate_per_kg_times_total_weight()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest() with { BaseRate = 5m, RatePerKg = 2m });

        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(3m, 50m, "US", null);

        options.Should().ContainSingle();
        options[0].Cost.Should().Be(11m); // 5 + 2*3
    }

    [Fact]
    public async Task Meeting_the_free_shipping_threshold_makes_the_cost_zero()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest() with { BaseRate = 5m, RatePerKg = 2m, FreeShippingThreshold = 50m });

        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(3m, 60m, "US", null);

        options[0].Cost.Should().Be(0m);
    }

    [Fact]
    public async Task Not_meeting_the_free_shipping_threshold_still_charges_the_computed_cost()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest() with { BaseRate = 5m, RatePerKg = 2m, FreeShippingThreshold = 50m });

        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(3m, 30m, "US", null);

        options[0].Cost.Should().Be(11m);
    }

    [Fact]
    public async Task Multiple_methods_for_the_same_jurisdiction_are_all_returned()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest() with { Name = "Standard", BaseRate = 5m });
        await _harness.ShippingService.CreateAsync(StandardRequest() with { Name = "Express", BaseRate = 15m });

        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(1m, 10m, "US", null);

        options.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_whole_country_method_and_a_region_specific_method_are_both_returned_for_a_matching_region()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest() with { Name = "Standard", RegionCode = null });
        await _harness.ShippingService.CreateAsync(StandardRequest() with { Name = "Regional Express", RegionCode = "CA" });

        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(1m, 10m, "US", "CA");

        options.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_region_specific_method_does_not_apply_to_a_different_region()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest() with { Name = "Regional Express", RegionCode = "CA" });

        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(1m, 10m, "US", "NY");

        options.Should().BeEmpty();
    }

    [Fact]
    public async Task No_methods_configured_returns_an_empty_list()
    {
        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(1m, 10m, "US", null);

        options.Should().BeEmpty();
    }

    [Fact]
    public async Task An_inactive_method_is_not_returned()
    {
        await _harness.ShippingService.CreateAsync(StandardRequest() with { IsActive = false });

        var options = await _harness.ShippingService.GetAvailableShippingOptionsAsync(1m, 10m, "US", null);

        options.Should().BeEmpty();
    }

    [Fact]
    public async Task Estimated_shipping_picks_the_cheapest_available_option()
    {
        // The harness configures Store:DefaultShippingCountryCode=US, Store:DefaultShippingRegionCode=CA.
        await _harness.ShippingService.CreateAsync(StandardRequest() with { Name = "Standard", RegionCode = "CA", BaseRate = 5m, RatePerKg = 0m });
        await _harness.ShippingService.CreateAsync(StandardRequest() with { Name = "Express", RegionCode = "CA", BaseRate = 15m, RatePerKg = 0m });

        var result = await _harness.ShippingService.CalculateEstimatedShippingAsync(1m, 10m);

        result.RateConfigured.Should().BeTrue();
        result.Cost.Should().Be(5m);
    }

    [Fact]
    public async Task Estimated_shipping_with_no_methods_configured_is_zero_and_unconfigured()
    {
        var result = await _harness.ShippingService.CalculateEstimatedShippingAsync(1m, 10m);

        result.RateConfigured.Should().BeFalse();
        result.Cost.Should().Be(0m);
    }

    private static CreateShippingMethodRequest StandardRequest() => new(
        "Standard Shipping", null, "US", null, 5m, 1m, null, null, null, 0, true);
}
