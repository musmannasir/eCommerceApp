using ECommerceApp.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.StaffRolesCsv)]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
