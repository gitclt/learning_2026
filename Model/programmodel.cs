using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace kalanjali_api.Model
{
    public class programmodel
    {
        public int id { get; set; }
        //public int? institute_id { get; set; }
        public int? event_id { get; set; }
        public int? stage_id { get; set; }

        [MaxLength(100, ErrorMessage = "program name cannot exceed 100 characters.")]

        public string? program_name { get; set; }
        public DateTime? date { get; set; }
        public string? time { get; set; }
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
        public string? delete_status { get; set; }
        public string? program_type { get; set; }

        public string? participant_type { get; set; }
        public string? gender { get; set; }
        public string? item_code { get; set; }
        public string? offstage_onstage { get; set; }

        public int? no_of_regstn_per_institute { get; set; }
        public int? group_max_participants { get; set; }
        public int? group_min_participants { get; set; }
        public DateTime? status_updated { get; set; } 
        public string? no_of_group { get; set; } 
        [NotMapped]
        public List<prgm_judgement_criteria_model>? judgementcriteria { get; set; }
        public int? ac_year_id { get; set; }

    }

    public class judgementcriteriaRequest
    {
        public int? prgm_id { get; set; }
        public DateTime? addedon { get; set; }
        public double? point { get; set; }

        public int? judgement_criteria { get; set; }
        public string? name { get; set; }


    }




}
