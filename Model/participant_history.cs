namespace kalanjali_api.Model
{
    public class participant_history
    {
        public int id { get; set; }
        public int? prgm_id { get; set; }
        public int? student_id { get; set; }
        public string chess_no { get; set; }
        public int? added_by { get; set; }
        public string addedtype { get; set; }
        public string status { get; set; }
        public DateTime? deletedon { get; set; }
        public int? deleted_by { get; set; }
        public string deleted_type { get; set; }
        public double? average_point { get; set; }
        public string program_status { get; set; }
        public int? user_id { get; set; }
        public DateTime? status_updated { get; set; }
        public string token_no { get; set; }
        public string grade { get; set; }
        public double? grade_point { get; set; }
        public string position { get; set; }
        public double? position_point { get; set; }
        public double? total_point { get; set; }
        public string ispresent { get; set; }
        public string remark { get; set; }
        public string group_name { get; set; }
        public string history_type { get; set; }
        public int? created_user_id { get; set; }
        public DateTime? addedon { get; set; }

    }
}
