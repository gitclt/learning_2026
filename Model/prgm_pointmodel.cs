namespace kalanjali_api.Model
{
    public class prgm_pointmodel
    {
        public int id { get; set; }
        public int? prgm_id { get; set; }
        public int? student_id { get; set; }
        public int? judge_id { get; set; }
        public string? chess_no { get; set; }
        public DateTime? addedon { get; set; }
     
        public int? judgement_criteria_id { get; set; }
        public double? point { get; set; }
        public string? ispresent { get; set; }
        public string? remark { get; set; }
        public string? comments { get; set; }

    }
}
