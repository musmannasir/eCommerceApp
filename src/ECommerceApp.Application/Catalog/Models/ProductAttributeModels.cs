namespace ECommerceApp.Application.Catalog.Models;

public record ProductAttributeValueDto(int Id, int ProductAttributeId, string Value);

public record ProductAttributeDto(int Id, string Name, IReadOnlyList<ProductAttributeValueDto> Values);

public record CreateProductAttributeRequest(string Name);

public record CreateProductAttributeValueRequest(int ProductAttributeId, string Value);
