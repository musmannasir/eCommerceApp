using System.Security.Claims;
using ECommerceApp.Application.Addresses;
using ECommerceApp.Application.Addresses.Models;
using ECommerceApp.Web.Models.Addresses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Controllers;

/// <summary>
/// Customer address book (Milestone 8.1) - account-only, like Wishlist, no
/// guest concept. Classic server-rendered forms (not AJAX) since an address
/// has many fields, unlike Cart/Wishlist's single-value toggle actions -
/// mirrors AccountController's ChangePassword pattern instead.
/// </summary>
[Authorize]
[Route("Addresses")]
public class AddressesController : Controller
{
    private readonly IAddressService _addressService;

    public AddressesController(IAddressService addressService)
    {
        _addressService = addressService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var addresses = await _addressService.GetAddressesAsync(UserId, cancellationToken);
        return View(addresses);
    }

    [HttpGet("Create")]
    public IActionResult Create(string? returnUrl) => View(new AddressFormViewModel { ReturnUrl = returnUrl });

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AddressFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _addressService.CreateAsync(UserId, new CreateAddressRequest(
            model.Label, model.FullName, model.Phone, model.Line1, model.Line2,
            model.City, model.RegionCode, model.PostalCode, model.CountryCode, model.IsDefault), cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = "Address added.";
        return !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? Redirect(model.ReturnUrl)
            : RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var result = await _addressService.GetByIdAsync(UserId, id, cancellationToken);
        if (result.IsFailure)
        {
            return NotFound();
        }

        var address = result.Value;
        return View(new AddressFormViewModel
        {
            Id = address.Id,
            Label = address.Label,
            FullName = address.FullName,
            Phone = address.Phone,
            Line1 = address.Line1,
            Line2 = address.Line2,
            City = address.City,
            RegionCode = address.RegionCode,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode,
            IsDefault = address.IsDefault,
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AddressFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _addressService.UpdateAsync(UserId, new UpdateAddressRequest(
            id, model.Label, model.FullName, model.Phone, model.Line1, model.Line2,
            model.City, model.RegionCode, model.PostalCode, model.CountryCode, model.IsDefault), cancellationToken);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.FirstError.Message);
            return View(model);
        }

        TempData["Message"] = "Address updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _addressService.DeleteAsync(UserId, id, cancellationToken);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }
        else
        {
            TempData["Message"] = "Address removed.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("SetDefault/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id, CancellationToken cancellationToken)
    {
        var result = await _addressService.SetDefaultAsync(UserId, id, cancellationToken);
        if (result.IsFailure)
        {
            TempData["Error"] = result.FirstError.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
