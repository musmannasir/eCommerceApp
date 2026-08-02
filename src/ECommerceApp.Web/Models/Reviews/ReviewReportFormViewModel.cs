using System.ComponentModel.DataAnnotations;
using ECommerceApp.Domain.Reviews;

namespace ECommerceApp.Web.Models.Reviews;

public class ReviewReportFormViewModel
{
    [Required]
    public int ReviewId { get; set; }

    [Required]
    public ReviewReportReason Reason { get; set; }

    [StringLength(500), Display(Name = "Additional details (optional)")]
    public string? Comment { get; set; }
}
