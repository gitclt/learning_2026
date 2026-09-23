namespace kalanjali_api.Model
{
    public class group_members
    {
        public int? id { get; set; }     
        public int? prgm_id { get; set; }     
        public int? student_id { get; set; }     
       public string? verify_status { get; set; }     
       public string? token_no { get; set; }     
       public DateTime? status_updated { get; set; }     
       public int? user_id { get; set; }     
       public string? group_name { get; set; }

        //extra fields
        public int? instit_id { get; set; }
        public string? chest_no { get; set; }
        //
    }
}
