using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace kalanjali_api.Model
{
    public class judgemodel
    {
        public int id { get; set; }
        public int? event_id { get; set; }

        [MaxLength(100, ErrorMessage = "Judge name cannot exceed 100 characters.")]

        public string? judge_name { get; set; }

        [MaxLength(15, ErrorMessage = "phone number cannot exceed 15 characters.")]

        public string? phone_no { get; set; }
        public string? password { get; set; }
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

        //    [NotMapped]
        //    public List<judges_prgmmodel>? judges_prgm { get; set; }
    }
    //public class judges_prgmRequest
    //{
    //    public int? id { get; set; }
    //    public int? judge_id { get; set; }
    //    public int? program_id { get; set; }
    //    public string? status { get; set; }
    //    public DateTime? addedon { get; set; }

    //}
}
