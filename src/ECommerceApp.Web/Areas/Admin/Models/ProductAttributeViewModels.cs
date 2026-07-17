using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class CreateAttributeViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
}

public class CreateAttributeValueViewModel
{
    [Required]
    public int ProductAttributeId { get; set; }

    [Required, StringLength(100)]
    public string Value { get; set; } = string.Empty;
}
