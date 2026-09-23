using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace kalanjali_api.Model
{
    public class eventmodel
    {
        public int id { get; set; }
        public DateTime? from_date { get; set; }
        public DateTime? to_date { get; set; }

        [MaxLength(100, ErrorMessage = "event name must not exceed 100 characters.")]

        public string? event_name { get; set; }

        [MaxLength(100, ErrorMessage = "short name must not exceed 100 characters.")]

        public string? short_name { get; set; }
        public string? image { get; set; }

        public string? image1 { get; set; }
        [NotMapped]
        public string? image_data { get; set; }

        [NotMapped]
        public string? is_img_chged { get; set; } //yes/no
        [NotMapped]
        public string? img_oldname { get; set; }

        public int? venue_id { get; set; }
        public DateTime? addedon { get; set; }
        public int? added_by { get; set; }
        public string? addedtype { get; set; }
        public DateTime? modifiedon { get; set; }
        public int? modified_by { get; set; }
        public string? modified_type { get; set; }
        public string? status { get; set; }
        public DateTime? deletedon { get; set; }
        public int? deleted_by { get; set; }
        public int? students_per_institute { get; set; }
        public string? deleted_type { get; set; }
        public string? program_status { get; set; }
        public int? ac_year_id { get; set; }

    }
}
