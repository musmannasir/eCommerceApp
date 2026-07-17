using ECommerceApp.Application.Catalog.Models;
using ECommerceApp.Application.Common.Models;
using ECommerceApp.Domain.Common;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Catalog;

public class CategoryServiceTests : IDisposable
{
    private readonly CatalogTestHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Creating_a_category_succeeds_and_generates_a_slug()
    {
        var result = await _harness.CategoryService.CreateAsync(
            new CreateCategoryRequest("Electronics", null, "Gadgets and gizmos", null, 1, true, false));

        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be("electronics");
    }

    [Fact]
    public async Task Creating_a_category_with_a_slug_already_in_use_is_rejected()
    {
        await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Electronics", null, null, null, 0, true, false));

        var result = await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("electronics", null, null, null, 0, true, false));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Assigning_a_category_as_its_own_grandchild_parent_is_rejected()
    {
        var root = (await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Root", null, null, null, 0, true, false))).Value;
        var child = (await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Child", null, null, root.Id, 0, true, false))).Value;
        var grandchild = (await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Grandchild", null, null, child.Id, 0, true, false))).Value;

        // Attempt to move Root under its own grandchild - a cycle.
        var result = await _harness.CategoryService.UpdateAsync(
            new UpdateCategoryRequest(root.Id, root.Name, root.Slug, null, grandchild.Id, 0, true, false));

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task A_category_cannot_be_set_as_its_own_parent()
    {
        var category = (await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Standalone", null, null, null, 0, true, false))).Value;

        var result = await _harness.CategoryService.UpdateAsync(
            new UpdateCategoryRequest(category.Id, category.Name, category.Slug, null, category.Id, 0, true, false));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_category_soft_deletes_it_and_it_can_be_restored()
    {
        var category = (await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Temporary", null, null, null, 0, true, false))).Value;

        var deleteResult = await _harness.CategoryService.DeleteAsync(category.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        (await _harness.CategoryService.GetByIdAsync(category.Id)).IsFailure.Should().BeTrue();

        var restoreResult = await _harness.CategoryService.RestoreAsync(category.Id);
        restoreResult.IsSuccess.Should().BeTrue();

        (await _harness.CategoryService.GetByIdAsync(category.Id)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_category_with_subcategories_is_rejected()
    {
        var parent = (await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Parent", null, null, null, 0, true, false))).Value;
        await _harness.CategoryService.CreateAsync(new CreateCategoryRequest("Child", null, null, parent.Id, 0, true, false));

        var result = await _harness.CategoryService.DeleteAsync(parent.Id);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task GetPagedAsync_paginates_and_searches_by_name()
    {
        for (var i = 1; i <= 25; i++)
        {
            await _harness.CategoryService.CreateAsync(new CreateCategoryRequest($"Category {i:00}", null, null, null, i, true, false));
        }

        var firstPage = await _harness.CategoryService.GetPagedAsync(new PagedQuery { Page = 1, PageSize = 10 });
        firstPage.Value.Items.Should().HaveCount(10);
        firstPage.Value.TotalCount.Should().Be(25);
        firstPage.Value.TotalPages.Should().Be(3);

        var searchResult = await _harness.CategoryService.GetPagedAsync(new PagedQuery { Search = "Category 07" });
        searchResult.Value.Items.Should().ContainSingle(c => c.Name == "Category 07");
    }
}
