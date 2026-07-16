using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Models.Account;

public class RegisterViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
