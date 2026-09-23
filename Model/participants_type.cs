using System.ComponentModel.DataAnnotations;

namespace kalanjali_api.Model
{
    public class participants_type
    {
        public int id { get; set; }
        [MaxLength(50, ErrorMessage = "type cannot exceed 50 characters.")]

        public string? type { get; set; }
        public string? class_id { get; set; }

       
        public DateTime? addedon { get; set; }
        public int? added_by { get; set; }

        [MaxLength(50, ErrorMessage = "addedtype cannot exceed 50 characters.")]
        public string? addedtype { get; set; }
        public DateTime? modifiedon { get; set; }
        public int? modified_by { get; set; }
        public string? modified_type { get; set; }
        public string? status { get; set; }
        public DateTime? deletedon { get; set; }
        public int? deleted_by { get; set; }
        public string? deleted_type { get; set; }
    }
}
