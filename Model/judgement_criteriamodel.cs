using System.ComponentModel.DataAnnotations;

namespace kalanjali_api.Model
{
    public class judgement_criteriamodel
    {

        public int? id { get; set; }

        [MaxLength(100, ErrorMessage = "name must not exceed 100 characters.")]

        public string? name { get; set; }
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
