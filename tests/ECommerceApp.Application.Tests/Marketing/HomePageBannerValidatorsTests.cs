using ECommerceApp.Application.Marketing.Models;
using ECommerceApp.Application.Marketing.Validators;
using FluentAssertions;

namespace ECommerceApp.Application.Tests.Marketing;

public class HomePageBannerValidatorsTests
{
    private readonly CreateHomePageBannerRequestValidator _createValidator = new();
    private readonly UpdateHomePageBannerRequestValidator _updateValidator = new();

    [Fact]
    public void A_well_formed_request_is_valid()
    {
        var request = new CreateHomePageBannerRequest("Summer Sale", "Up to 30% off", "/deals", "Hero", 0, true);

        _createValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_empty_title_is_rejected()
    {
        var request = new CreateHomePageBannerRequest("", null, null, "Hero", 0, true);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_invalid_banner_type_is_rejected()
    {
        var request = new CreateHomePageBannerRequest("Summer Sale", null, null, "NotAType", 0, true);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_negative_display_order_is_rejected()
    {
        var request = new CreateHomePageBannerRequest("Summer Sale", null, null, "Hero", -1, true);

        _createValidator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_well_formed_update_request_is_valid()
    {
        var request = new UpdateHomePageBannerRequest(1, "Summer Sale", "Up to 30% off", "/deals", "Hero", 0, true);

        _updateValidator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_id_is_rejected_on_update()
    {
        var request = new UpdateHomePageBannerRequest(0, "Summer Sale", null, null, "Hero", 0, true);

        _updateValidator.Validate(request).IsValid.Should().BeFalse();
    }
}
