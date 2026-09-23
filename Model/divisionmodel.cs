using System.ComponentModel.DataAnnotations;

namespace kalanjali_api.Model
{
    public class divisionmodel
    {
        public int id { get; set; }
        public int? class_id { get; set; }

        [MaxLength(100, ErrorMessage = "class cannot exceed 50 characters.")]

        public string? division { get; set; }
        public DateTime? addedon { get; set; }
        public int? added_by { get; set; }
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
