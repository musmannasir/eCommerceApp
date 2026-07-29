using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Domain.Common;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Addresses;

public class AddressServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_users_first_address_makes_it_the_default_even_if_not_requested()
    {
        var result = await _harness.AddressService.CreateAsync("user-1", ValidRequest() with { IsDefault = false });

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Creating_a_second_address_as_default_clears_the_first_ones_default_flag()
    {
        var first = await _harness.AddressService.CreateAsync("user-1", ValidRequest());

        var second = await _harness.AddressService.CreateAsync("user-1", ValidRequest() with { IsDefault = true });

        second.Value.IsDefault.Should().BeTrue();
        var reloadedFirst = await _harness.AddressService.GetByIdAsync("user-1", first.Value.Id);
        reloadedFirst.Value.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Creating_a_second_address_not_as_default_leaves_the_first_ones_default_flag_untouched()
    {
        var first = await _harness.AddressService.CreateAsync("user-1", ValidRequest());

        var second = await _harness.AddressService.CreateAsync("user-1", ValidRequest() with { IsDefault = false });

        second.Value.IsDefault.Should().BeFalse();
        var reloadedFirst = await _harness.AddressService.GetByIdAsync("user-1", first.Value.Id);
        reloadedFirst.Value.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Getting_an_address_belonging_to_a_different_user_is_not_found()
    {
        var created = await _harness.AddressService.CreateAsync("user-1", ValidRequest());

        var result = await _harness.AddressService.GetByIdAsync("user-2", created.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Getting_a_nonexistent_address_is_not_found()
    {
        var result = await _harness.AddressService.GetByIdAsync("user-1", 999999);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Updating_an_address_belonging_to_a_different_user_is_not_found()
    {
        var created = await _harness.AddressService.CreateAsync("user-1", ValidRequest());

        var result = await _harness.AddressService.UpdateAsync("user-2", new UpdateAddressRequest(
            created.Value.Id, "Work", "Someone Else", "555-0199", "999 Other St", null, "Metropolis", null, "10001", "US", false));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Updating_an_address_to_be_the_default_clears_the_previous_default()
    {
        var first = await _harness.AddressService.CreateAsync("user-1", ValidRequest());
        var second = await _harness.AddressService.CreateAsync("user-1", ValidRequest() with { IsDefault = false });

        var updated = await _harness.AddressService.UpdateAsync("user-1", new UpdateAddressRequest(
            second.Value.Id, "Work", "Jane Doe", "555-0100", "123 Main St", "Apt 4", "Springfield", "CA", "90210", "US", true));

        updated.Value.IsDefault.Should().BeTrue();
        var reloadedFirst = await _harness.AddressService.GetByIdAsync("user-1", first.Value.Id);
        reloadedFirst.Value.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Updating_an_address_persists_field_changes()
    {
        var created = await _harness.AddressService.CreateAsync("user-1", ValidRequest());

        var updated = await _harness.AddressService.UpdateAsync("user-1", new UpdateAddressRequest(
            created.Value.Id, "Work", "New Name", "555-9999", "456 Other Ave", null, "Newtown", "NY", "10002", "GB", true));

        updated.IsSuccess.Should().BeTrue();
        updated.Value.FullName.Should().Be("New Name");
        updated.Value.City.Should().Be("Newtown");
        updated.Value.CountryCode.Should().Be("GB");
    }

    [Fact]
    public async Task Deleting_an_address_belonging_to_a_different_user_is_not_found()
    {
        var created = await _harness.AddressService.CreateAsync("user-1", ValidRequest());

        var result = await _harness.AddressService.DeleteAsync("user-2", created.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Deleting_the_default_address_leaves_no_default_at_all()
    {
        var first = await _harness.AddressService.CreateAsync("user-1", ValidRequest());
        await _harness.AddressService.CreateAsync("user-1", ValidRequest() with { IsDefault = false });

        await _harness.AddressService.DeleteAsync("user-1", first.Value.Id);

        var remaining = await _harness.AddressService.GetAddressesAsync("user-1");
        remaining.Should().ContainSingle();
        remaining[0].IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Setting_a_different_address_as_default_clears_the_previous_one()
    {
        var first = await _harness.AddressService.CreateAsync("user-1", ValidRequest());
        var second = await _harness.AddressService.CreateAsync("user-1", ValidRequest() with { IsDefault = false });

        var result = await _harness.AddressService.SetDefaultAsync("user-1", second.Value.Id);

        result.Value.IsDefault.Should().BeTrue();
        var reloadedFirst = await _harness.AddressService.GetByIdAsync("user-1", first.Value.Id);
        reloadedFirst.Value.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Setting_an_address_belonging_to_a_different_user_as_default_is_not_found()
    {
        var created = await _harness.AddressService.CreateAsync("user-1", ValidRequest());

        var result = await _harness.AddressService.SetDefaultAsync("user-2", created.Value.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Two_different_users_addresses_are_fully_isolated()
    {
        await _harness.AddressService.CreateAsync("user-1", ValidRequest());
        await _harness.AddressService.CreateAsync("user-1", ValidRequest() with { IsDefault = false });

        var user2Addresses = await _harness.AddressService.GetAddressesAsync("user-2");

        user2Addresses.Should().BeEmpty();
    }

    private static CreateAddressRequest ValidRequest() => new(
        "Home", "Jane Doe", "555-0100", "123 Main St", "Apt 4", "Springfield", "CA", "90210", "US", true);
}
