using System.ComponentModel.DataAnnotations;

namespace QMRv2.Models.DTO
{
    public class IfxLot
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Please input value on required fields.")]
        [MaxLength(20, ErrorMessage = "Lot Number length exceeded limit.")]
        public string? LotNumber { get; set; }
        public string? Status { get; set; }
        public string? Disposition { get; set; }
    }
}
