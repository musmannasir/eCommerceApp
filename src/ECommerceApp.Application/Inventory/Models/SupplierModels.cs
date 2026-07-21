namespace ECommerceApp.Application.Inventory.Models;

public record SupplierDto(
    int Id,
    string Name,
    string Code,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string? Website,
    string? Notes,
    bool IsActive,
    bool IsDeleted);

public record CreateSupplierRequest(
    string Name,
    string Code,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string? Website,
    string? Notes,
    bool IsActive);

public record UpdateSupplierRequest(
    int Id,
    string Name,
    string Code,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country,
    string? Website,
    string? Notes,
    bool IsActive);

public record SupplierProductDto(
    int Id,
    int SupplierId,
    int ProductId,
    string ProductName,
    string ProductBaseSku,
    string? SupplierSku,
    decimal? CostPrice,
    int? LeadTimeDays,
    bool IsPreferred);

public record LinkSupplierProductRequest(
    int SupplierId,
    int ProductId,
    string? SupplierSku,
    decimal? CostPrice,
    int? LeadTimeDays,
    bool IsPreferred);

public record UpdateSupplierProductRequest(
    int Id,
    string? SupplierSku,
    decimal? CostPrice,
    int? LeadTimeDays,
    bool IsPreferred);
