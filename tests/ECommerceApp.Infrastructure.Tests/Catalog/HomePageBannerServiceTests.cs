using ECommerceApp.Application.Common.Models;
using ECommerceApp.Application.Marketing.Models;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class HomePageBannerServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_banner_succeeds_with_valid_data()
    {
        var result = await _harness.HomePageBannerService.CreateAsync(new CreateHomePageBannerRequest(
            "Summer Sale", "Up to 30% off", null, "Hero", 0, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Summer Sale");
        result.Value.BannerType.Should().Be("Hero");
        result.Value.ImagePath.Should().BeNull();
    }

    [Fact]
    public async Task Setting_the_image_persists_the_path()
    {
        var created = await _harness.HomePageBannerService.CreateAsync(new CreateHomePageBannerRequest(
            "Summer Sale", null, null, "Hero", 0, true));

        var result = await _harness.HomePageBannerService.SetImageAsync(created.Value.Id, "/uploads/home-banners/abc.jpg");

        result.IsSuccess.Should().BeTrue();
        var loaded = await _harness.HomePageBannerService.GetByIdAsync(created.Value.Id);
        loaded.Value.ImagePath.Should().Be("/uploads/home-banners/abc.jpg");
    }

    [Fact]
    public async Task Updating_a_banner_persists_changes()
    {
        var created = await _harness.HomePageBannerService.CreateAsync(new CreateHomePageBannerRequest(
            "Summer Sale", null, null, "Hero", 0, true));

        var result = await _harness.HomePageBannerService.UpdateAsync(new UpdateHomePageBannerRequest(
            created.Value.Id, "Winter Sale", "Up to 50% off", "/deals", "Promo", 2, false));

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Winter Sale");
        result.Value.BannerType.Should().Be("Promo");
        result.Value.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_banner_soft_deletes_it_and_it_no_longer_appears_in_the_paged_list()
    {
        var created = await _harness.HomePageBannerService.CreateAsync(new CreateHomePageBannerRequest(
            "Summer Sale", null, null, "Hero", 0, true));

        await _harness.HomePageBannerService.DeleteAsync(created.Value.Id);

        var page = await _harness.HomePageBannerService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().NotContain(b => b.Id == created.Value.Id);

        var deletedPage = await _harness.HomePageBannerService.GetPagedAsync(new PagedQuery { OnlyDeleted = true });
        deletedPage.Value.Items.Should().Contain(b => b.Id == created.Value.Id);
    }

    [Fact]
    public async Task Restoring_a_deleted_banner_makes_it_visible_again()
    {
        var created = await _harness.HomePageBannerService.CreateAsync(new CreateHomePageBannerRequest(
            "Summer Sale", null, null, "Hero", 0, true));
        await _harness.HomePageBannerService.DeleteAsync(created.Value.Id);

        await _harness.HomePageBannerService.RestoreAsync(created.Value.Id);

        var page = await _harness.HomePageBannerService.GetPagedAsync(new PagedQuery());
        page.Value.Items.Should().Contain(b => b.Id == created.Value.Id);
    }

    [Fact]
    public async Task Deactivating_and_reactivating_a_banner_updates_its_active_flag()
    {
        var created = await _harness.HomePageBannerService.CreateAsync(new CreateHomePageBannerRequest(
            "Summer Sale", null, null, "Hero", 0, true));

        await _harness.HomePageBannerService.DeactivateAsync(created.Value.Id);
        (await _harness.HomePageBannerService.GetByIdAsync(created.Value.Id)).Value.IsActive.Should().BeFalse();

        await _harness.HomePageBannerService.ActivateAsync(created.Value.Id);
        (await _harness.HomePageBannerService.GetByIdAsync(created.Value.Id)).Value.IsActive.Should().BeTrue();
    }
}
