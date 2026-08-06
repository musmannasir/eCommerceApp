using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Areas.Admin.Models;

public class UserCreateViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, Display(Name = "First name"), StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, Display(Name = "Last name"), StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public class UserEditViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required, Display(Name = "First name"), StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, Display(Name = "Last name"), StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public bool IsSelf { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastSuccessfulLoginAtUtc { get; set; }
}
