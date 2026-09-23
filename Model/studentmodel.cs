using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace kalanjali_api.Model
{
    public class studentmodel
    {

        public int id { get; set; }
        public int? institute { get; set; }

        [MaxLength(100, ErrorMessage = "name must not exceed 100 characters.")]

        public string? name { get; set; }
        [MaxLength(50, ErrorMessage = "length must not exceed 50 characters.")]

        public string? admsn_no { get; set; }

        [MaxLength(15, ErrorMessage = "phone number cannot exceed 15 characters.")]

        public string? phone_no { get; set; }
        public string? username { get; set; }
        public string? password { get; set; }

        public int? class_id { get; set; }
        public int? division_id { get; set; }
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
      
        public string? gender { get; set; }
        public string? kalathilakam_kalaprathiba { get; set; }
        public string? email { get; set; }

        public string? category { get; set; }


        public string? image { get; set; }
        [NotMapped]
        public string? image_data { get; set; }

        [NotMapped]
        public string? is_img_chged { get; set; } //yes/no
        [NotMapped]
        public string? img_oldname { get; set; }


        [NotMapped]
        public List<prgm_participants>? students_prgm { get; set; }
        public int? ac_year_id { get; set; }

    }
    public class student_prgmRequest
    {
        public int? id { get; set; }
        public int? student_id { get; set; }
        public int? prgm_id { get; set; }
        public string? chess_no {  get; set; }  
        public string? status { get; set; }
        public DateTime? addedon { get; set; }

    }
}



