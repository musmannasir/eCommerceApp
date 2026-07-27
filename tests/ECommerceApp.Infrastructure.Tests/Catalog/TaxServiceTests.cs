using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Taxation.Models;
using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class TaxServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_country_wide_rate_succeeds_with_valid_data()
    {
        var result = await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 8.25m, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.CountryCode.Should().Be("US");
        result.Value.RegionCode.Should().BeNull();
    }

    [Fact]
    public async Task Creating_a_duplicate_country_wide_rate_is_rejected()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 8.25m, true));

        var result = await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("us", null, "standard", 5m, true));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task A_country_wide_rate_and_a_region_scoped_rate_for_the_same_country_and_category_can_coexist()
    {
        var countryWide = await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 5m, true));
        var regionScoped = await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 8.25m, true));

        countryWide.IsSuccess.Should().BeTrue();
        regionScoped.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Updating_a_rate_persists_changes()
    {
        var created = await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 8.25m, true));

        var result = await _harness.TaxService.UpdateAsync(new UpdateTaxRateRequest(created.Value.Id, "US", null, "Standard", 9.5m, false));

        result.IsSuccess.Should().BeTrue();
        result.Value.RatePercent.Should().Be(9.5m);
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_rate_soft_deletes_it_and_it_no_longer_appears_in_the_paged_list()
    {
        var created = await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 8.25m, true));

        await _harness.TaxService.DeleteAsync(created.Value.Id);

        var page = await _harness.TaxService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().NotContain(r => r.Id == created.Value.Id);

        var deletedPage = await _harness.TaxService.GetPagedAsync(new PagedQuery { OnlyDeleted = true });
        deletedPage.Value.Items.Should().Contain(r => r.Id == created.Value.Id);
    }

    [Fact]
    public async Task Restoring_a_deleted_rate_makes_it_visible_again()
    {
        var created = await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 8.25m, true));
        await _harness.TaxService.DeleteAsync(created.Value.Id);

        await _harness.TaxService.RestoreAsync(created.Value.Id);

        var page = await _harness.TaxService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().Contain(r => r.Id == created.Value.Id);
    }

    [Fact]
    public async Task An_exact_region_match_takes_precedence_over_a_country_wide_rate()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 5m, true));
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 8.25m, true));

        var result = await _harness.TaxService.CalculateTaxAsync(100m, "Standard", "US", "CA");

        result.RateConfigured.Should().BeTrue();
        result.RatePercent.Should().Be(8.25m);
        result.TaxAmount.Should().Be(8.25m);
    }

    [Fact]
    public async Task No_region_match_falls_back_to_the_country_wide_rate()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 5m, true));
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 8.25m, true));

        var result = await _harness.TaxService.CalculateTaxAsync(100m, "Standard", "US", "NY");

        result.RateConfigured.Should().BeTrue();
        result.RatePercent.Should().Be(5m);
    }

    [Fact]
    public async Task No_matching_rate_at_all_returns_zero_tax_with_RateConfigured_false()
    {
        var result = await _harness.TaxService.CalculateTaxAsync(100m, "Standard", "US", null);

        result.RateConfigured.Should().BeFalse();
        result.TaxAmount.Should().Be(0);
    }

    [Fact]
    public async Task Matching_is_case_insensitive_on_country_code_and_category()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 8.25m, true));

        var result = await _harness.TaxService.CalculateTaxAsync(100m, "standard", "us", null);

        result.RateConfigured.Should().BeTrue();
        result.RatePercent.Should().Be(8.25m);
    }

    [Fact]
    public async Task An_inactive_rate_is_not_applied()
    {
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", null, "Standard", 8.25m, false));

        var result = await _harness.TaxService.CalculateTaxAsync(100m, "Standard", "US", null);

        result.RateConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task Estimated_tax_sums_across_multiple_categories()
    {
        // The harness configures Store:DefaultTaxCountryCode=US, Store:DefaultTaxRegionCode=CA.
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Standard", 10m, true));
        await _harness.TaxService.CreateAsync(new CreateTaxRateRequest("US", "CA", "Reduced", 5m, true));
        var lines = new[] { new TaxableLine(100m, "Standard"), new TaxableLine(50m, "Reduced") };

        var result = await _harness.TaxService.CalculateEstimatedTaxAsync(lines);

        result.RateConfigured.Should().BeTrue();
        result.TaxAmount.Should().Be(12.5m); // 10 + 2.5
    }

    [Fact]
    public async Task Estimated_tax_with_no_lines_is_zero_and_unconfigured()
    {
        var result = await _harness.TaxService.CalculateEstimatedTaxAsync(Array.Empty<TaxableLine>());

        result.RateConfigured.Should().BeFalse();
        result.TaxAmount.Should().Be(0);
    }

    [Fact]
    public async Task Estimated_tax_with_no_configured_rate_for_the_default_jurisdiction_is_zero_and_unconfigured()
    {
        var lines = new[] { new TaxableLine(100m, "Standard") };

        var result = await _harness.TaxService.CalculateEstimatedTaxAsync(lines);

        result.RateConfigured.Should().BeFalse();
        result.TaxAmount.Should().Be(0);
    }
}
