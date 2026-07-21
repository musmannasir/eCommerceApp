using ECommerceApp.Application.Storefront.Models;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Storefront;

public interface ICatalogBrowseService
{
    Task<Result<CatalogBrowseResultDto>> BrowseAsync(CatalogBrowseQuery query, CancellationToken cancellationToken = default);

    /// <summary>Backs the header search box's debounced suggestions dropdown - a short,
    /// fast top-N match list, not the full filtered/sorted/paginated result set.</summary>
    Task<IReadOnlyList<SearchSuggestionDto>> GetSuggestionsAsync(string term, CancellationToken cancellationToken = default);
}
