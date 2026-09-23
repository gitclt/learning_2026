using System.ComponentModel.DataAnnotations;

namespace kalanjali_api.Model
{
    public class stagemodel
    {
        public int id { get; set; }
        public int? institute_id { get; set; }

        public int? event_id { get; set; }

        [MaxLength(100, ErrorMessage = "stage name must not exceed 100 characters.")]

        public string? stage_name { get; set; }


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
        public string? deleted_type { get; set; }
        public string? color_code { get; set; }
        public string? token_prefix { get; set; }
        public string? is_datewise { get; set; }
        public int chest_start { get; set; }
        public int chest_end { get; set; }
        public string? chest_image { get; set; }
        //public int? ac_year_id { get; set; }
    }
}
