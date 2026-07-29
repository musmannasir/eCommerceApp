namespace ECommerceApp.Application.Addresses.Models;

public record AddressDto(
    int Id,
    string? Label,
    string FullName,
    string Phone,
    string Line1,
    string? Line2,
    string City,
    string? RegionCode,
    string PostalCode,
    string CountryCode,
    bool IsDefault);

public record CreateAddressRequest(
    string? Label,
    string FullName,
    string Phone,
    string Line1,
    string? Line2,
    string City,
    string? RegionCode,
    string PostalCode,
    string CountryCode,
    bool IsDefault);

public record UpdateAddressRequest(
    int Id,
    string? Label,
    string FullName,
    string Phone,
    string Line1,
    string? Line2,
    string City,
    string? RegionCode,
    string PostalCode,
    string CountryCode,
    bool IsDefault);
