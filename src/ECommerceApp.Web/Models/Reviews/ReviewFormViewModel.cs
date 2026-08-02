using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Web.Models.Reviews;

public class ReviewFormViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(150), Display(Name = "Title (optional)")]
    public string? Title { get; set; }

    [Required, StringLength(2000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;
}
