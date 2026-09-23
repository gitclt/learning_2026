using System.ComponentModel.DataAnnotations.Schema;

namespace kalanjali_api.Model
{
    public class usermodel
    {

        public int id { get; set; }
        public int? reference_id { get; set; }

        public string? type { get; set; }
        public string? name { get; set; }
        public string? phone_no { get; set; }
        public string? admsn_no { get; set; }
        public string? username { get; set; }       
        public string? address { get; set; }       
        public string? location { get; set; }       
        public string? contact_person { get; set; }       
        public string? password { get; set; }
        public string? enc_key { get; set; }
        public DateTime? enc_key_date { get; set; }
      
        public DateTime? addedon { get; set; }
        public int? added_by { get; set; }
        public int? stage_id { get; set; }
        public string? addedtype { get; set; }
        public DateTime? modifiedon { get; set; }
        public int? modified_by { get; set; }
        public string? modified_type { get; set; }
        public string? status { get; set; }
        public DateTime? deletedon { get; set; }
        public int? deleted_by { get; set; }
        public string? deleted_type { get; set; }
        public string? image { get; set; }

        [NotMapped]
        public string? image_data { get; set; }
        [NotMapped]
        public string? is_img_chged { get; set; } //yes/no
        [NotMapped]
        public string? img_oldname { get; set; }
        public string? fcm { get; set; }
        public string? device { get; set; }
        public int? logged_ac_year { get; set; }



    }
    //public class judges_prgmRequest
    //{
    //    public int? id { get; set; }
    //    public int? judge_id { get; set; }
    //    public int? program_id { get; set; }
    //    public string? status { get; set; }
    //    public DateTime? addedon { get; set; }

    //}

    //public class student_prgmRequest
    //{
    //    public int? id { get; set; }
    //    public int? student_id { get; set; }
    //    public int? prgm_id { get; set; }
    //    public string? status { get; set; }
    //    public DateTime? addedon { get; set; }

    //}

}
