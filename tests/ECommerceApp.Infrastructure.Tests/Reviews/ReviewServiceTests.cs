using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Application.Carts.Models;
using ECommerceApp.Application.Checkout.Models;
using ECommerceApp.Application.Orders.Models;
using ECommerceApp.Application.Payments.Models;
using ECommerceApp.Application.Reviews.Models;
using ECommerceApp.Application.Shipping.Models;
using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Catalog;
using ECommerceApp.Domain.Common;
using ECommerceApp.Domain.Reviews;
using ECommerceApp.Infrastructure.Tests.Catalog;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Reviews;

public class ReviewServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Submitting_a_review_succeeds_and_is_not_verified_without_a_purchase()
    {
        var product = await SeedProductAsync();

        var result = await _harness.ReviewService.SubmitReviewAsync(
            "user-1", new CreateReviewRequest(product.Id, 4, "Pretty good", "Does the job."));

        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(4);
        result.Value.Title.Should().Be("Pretty good");
        result.Value.IsVerifiedPurchase.Should().BeFalse();
    }

    [Fact]
    public async Task Submitting_a_second_review_for_the_same_product_is_rejected_as_a_conflict()
    {
        var product = await SeedProductAsync();
        await _harness.ReviewService.SubmitReviewAsync("user-1", new CreateReviewRequest(product.Id, 3, null, "First."));

        var result = await _harness.ReviewService.SubmitReviewAsync("user-1", new CreateReviewRequest(product.Id, 5, null, "Second."));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Submitting_a_review_for_an_unpublished_product_is_not_found()
    {
        var product = await SeedProductAsync(isPublished: false);

        var result = await _harness.ReviewService.SubmitReviewAsync("user-1", new CreateReviewRequest(product.Id, 5, null, "Body."));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task A_review_is_verified_when_the_reviewer_has_a_paid_order_containing_the_product()
    {
        var product = await SeedProductAsync();
        await PlaceOrderAsync("user-1", product.Id);

        var result = await _harness.ReviewService.SubmitReviewAsync(
            "user-1", new CreateReviewRequest(product.Id, 5, null, "Bought and loved it."));

        result.IsSuccess.Should().BeTrue();
        result.Value.IsVerifiedPurchase.Should().BeTrue();
    }

    [Fact]
    public async Task HasReviewedAsync_reflects_submission_state()
    {
        var product = await SeedProductAsync();

        (await _harness.ReviewService.HasReviewedAsync("user-1", product.Id)).Should().BeFalse();
        await _harness.ReviewService.SubmitReviewAsync("user-1", new CreateReviewRequest(product.Id, 4, null, "Body."));
        (await _harness.ReviewService.HasReviewedAsync("user-1", product.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task GetRatingSummaryAsync_computes_average_and_a_zero_filled_breakdown()
    {
        var product = await SeedProductAsync();
        await _harness.ReviewService.SubmitReviewAsync("user-1", new CreateReviewRequest(product.Id, 5, null, "Body 1."));
        await _harness.ReviewService.SubmitReviewAsync("user-2", new CreateReviewRequest(product.Id, 3, null, "Body 2."));

        var summary = await _harness.ReviewService.GetRatingSummaryAsync(product.Id);

        summary.ReviewCount.Should().Be(2);
        summary.AverageRating.Should().Be(4.0m);
        summary.RatingBreakdown[5].Should().Be(1);
        summary.RatingBreakdown[3].Should().Be(1);
        summary.RatingBreakdown[1].Should().Be(0);
        summary.RatingBreakdown.Keys.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public async Task GetReviewsAsync_returns_newest_first_and_paginates()
    {
        var product = await SeedProductAsync();
        await _harness.ReviewService.SubmitReviewAsync("user-1", new CreateReviewRequest(product.Id, 5, null, "Oldest."));
        await _harness.ReviewService.SubmitReviewAsync("user-2", new CreateReviewRequest(product.Id, 4, null, "Newest."));

        var page = await _harness.ReviewService.GetReviewsAsync(product.Id, page: 1, pageSize: 1);

        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle();
        page.Items[0].Body.Should().Be("Newest.");
    }

    [Fact]
    public async Task Reporting_a_review_succeeds()
    {
        var product = await SeedProductAsync();
        var review = await _harness.ReviewService.SubmitReviewAsync("author", new CreateReviewRequest(product.Id, 1, null, "Bad review."));

        var result = await _harness.ReviewService.ReportReviewAsync(
            "reporter-1", new CreateReviewReportRequest(review.Value.Id, ReviewReportReason.Spam, "Looks like spam."));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reporting_the_same_review_twice_by_the_same_user_is_rejected_as_a_conflict()
    {
        var product = await SeedProductAsync();
        var review = await _harness.ReviewService.SubmitReviewAsync("author", new CreateReviewRequest(product.Id, 1, null, "Bad review."));
        await _harness.ReviewService.ReportReviewAsync("reporter-1", new CreateReviewReportRequest(review.Value.Id, ReviewReportReason.Spam, null));

        var result = await _harness.ReviewService.ReportReviewAsync("reporter-1", new CreateReviewReportRequest(review.Value.Id, ReviewReportReason.Other, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Reporting_an_unknown_review_is_not_found()
    {
        var result = await _harness.ReviewService.ReportReviewAsync("reporter-1", new CreateReviewReportRequest(999999, ReviewReportReason.Other, null));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Moderation_queue_only_includes_reviews_with_open_reports()
    {
        var product = await SeedProductAsync();
        var reported = await _harness.ReviewService.SubmitReviewAsync("author-1", new CreateReviewRequest(product.Id, 1, "Reported", "Reported body."));
        await _harness.ReviewService.SubmitReviewAsync("author-2", new CreateReviewRequest(product.Id, 5, "Not reported", "Fine body."));
        await _harness.ReviewService.ReportReviewAsync("reporter-1", new CreateReviewReportRequest(reported.Value.Id, ReviewReportReason.Offensive, "Rude."));
        await _harness.ReviewService.ReportReviewAsync("reporter-2", new CreateReviewReportRequest(reported.Value.Id, ReviewReportReason.Spam, null));

        var queue = await _harness.ReviewService.GetModerationQueueAsync(new ReviewModerationQuery());

        queue.Items.Should().ContainSingle();
        queue.Items[0].ReviewId.Should().Be(reported.Value.Id);
        queue.Items[0].ReportCount.Should().Be(2);
        queue.Items[0].Reports.Should().HaveCount(2);
        queue.Items[0].ProductName.Should().Be("Widget");
    }

    [Fact]
    public async Task Dismissing_reports_clears_them_and_the_review_stays_live()
    {
        var product = await SeedProductAsync();
        var review = await _harness.ReviewService.SubmitReviewAsync("author", new CreateReviewRequest(product.Id, 1, null, "Body."));
        await _harness.ReviewService.ReportReviewAsync("reporter-1", new CreateReviewReportRequest(review.Value.Id, ReviewReportReason.Spam, null));

        var result = await _harness.ReviewService.DismissReportsAsync(review.Value.Id);

        result.IsSuccess.Should().BeTrue();
        var queue = await _harness.ReviewService.GetModerationQueueAsync(new ReviewModerationQuery());
        queue.Items.Should().BeEmpty();
        var summary = await _harness.ReviewService.GetRatingSummaryAsync(product.Id);
        summary.ReviewCount.Should().Be(1);
    }

    [Fact]
    public async Task Removing_a_review_soft_deletes_it_and_excludes_it_from_reads()
    {
        var product = await SeedProductAsync();
        var review = await _harness.ReviewService.SubmitReviewAsync("author", new CreateReviewRequest(product.Id, 1, null, "Body."));
        await _harness.ReviewService.ReportReviewAsync("reporter-1", new CreateReviewReportRequest(review.Value.Id, ReviewReportReason.Offensive, null));

        var result = await _harness.ReviewService.RemoveReviewAsync(review.Value.Id);

        result.IsSuccess.Should().BeTrue();
        var summary = await _harness.ReviewService.GetRatingSummaryAsync(product.Id);
        summary.ReviewCount.Should().Be(0);
        var reviews = await _harness.ReviewService.GetReviewsAsync(product.Id, page: 1, pageSize: 10);
        reviews.Items.Should().BeEmpty();
        var queue = await _harness.ReviewService.GetModerationQueueAsync(new ReviewModerationQuery());
        queue.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Dismissing_or_removing_an_unknown_review_is_not_found()
    {
        var dismissResult = await _harness.ReviewService.DismissReportsAsync(999999);
        var removeResult = await _harness.ReviewService.RemoveReviewAsync(999999);

        dismissResult.FirstError.Type.Should().Be(ErrorType.NotFound);
        removeResult.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    private async Task<Product> SeedProductAsync(bool isActive = true, bool isPublished = true)
    {
        var category = new Category { Name = "Cat", Slug = $"cat-{Guid.NewGuid():N}", DisplayOrder = 0, IsActive = true };
        _harness.DbContext.Categories.Add(category);
        await _harness.DbContext.SaveChangesAsync();

        var product = new Product
        {
            Name = "Widget",
            Slug = $"widget-{Guid.NewGuid():N}",
            CategoryId = category.Id,
            BaseSKU = $"SKU-{Guid.NewGuid():N}",
            CostPrice = 5m,
            SellingPrice = 10m,
            IsActive = isActive,
            IsPublished = isPublished,
            PublishedAtUtc = DateTime.UtcNow,
        };
        _harness.DbContext.Products.Add(product);
        await _harness.DbContext.SaveChangesAsync();
        return product;
    }

    private async Task PlaceOrderAsync(string userId, int productId)
    {
        var request = new CreateOrderRequest(
            userId,
            Guid.NewGuid().ToString("N"),
            new AddressDto(1, "Home", "Jane Doe", "555-0100", "123 Main St", null, "Springfield", "CA", "90210", "US", true),
            AppliedPromotionId: null,
            new ShippingOptionDto(1, "Standard Shipping", null, 7m, null, null),
            new List<CartItemDto>
            {
                new(1, productId, null, "Widget", "widget", null, "SKU-1", null, 100m, null, null, 1, 100m,
                    ProductStockState.InStock, 10, true, false, null, false),
            },
            new CheckoutCalculationResult(
                Subtotal: 100m, PromotionDiscount: 0m, AppliedCouponCode: null, AppliedPromotionName: null,
                DiscountedSubtotal: 100m, Tax: 10m, TaxRateConfigured: true,
                Shipping: 7m, ShippingRateConfigured: true, GrandTotal: 117m),
            new ChargeRequest("4242424242424242", "Jane Doe", 12, 2030, "123", Amount: 0m));

        var result = await _harness.OrderService.CreateOrderAsync(request);
        result.IsSuccess.Should().BeTrue();
    }
}
