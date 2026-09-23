using System.ComponentModel.DataAnnotations;

namespace kalanjali_api.Model
{
    public class grademodel
    {
        public int id { get; set; }
        [MaxLength(100, ErrorMessage = "grade not exceed 50 characters.")]
        public string? grade { get; set; }

        public int? added_by { get; set; }

        public double? from_point { get; set; }
        public double? to_point { get; set; }
        public DateTime? addedon { get; set; }
        public int? gradetype_id { get; set; }
        public string? addedtype { get; set; }
        public DateTime? modifiedon { get; set; }
        public int? modified_by { get; set; }
        public string? modified_type { get; set; }
        public string? status { get; set; }
        public DateTime? deletedon { get; set; }
        public int? deleted_by { get; set; }
        public string? deleted_type { get; set; }
        public double? point { get; set; }
        public int? ac_year_id { get; set; }
    }
}
