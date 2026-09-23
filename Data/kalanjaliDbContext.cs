using kalanjali_api.Model;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Data
{
    public class kalanjaliDbContext:DbContext
    {
        public kalanjaliDbContext(DbContextOptions options) : base(options)
        {

        }

        public string base64Decode(string data)
        {
            try
            {
                System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();
                System.Text.Decoder utf8Decode = encoder.GetDecoder();

                byte[] todecode_byte = Convert.FromBase64String(data);
                int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                string result = new String(decoded_char);
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Decode" + e.Message);
            }
        }
        public string base64Encode(string data)
        {
            try
            {
                byte[] encData_byte = new byte[data.Length];
                encData_byte = System.Text.Encoding.UTF8.GetBytes(data);
                string encodedData = Convert.ToBase64String(encData_byte);
                return encodedData;
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Encode" + e.Message);
            }
        }


        public DbSet<eventmodel> tbl_event { get; set; }
        public DbSet<venuemodel> tbl_venue { get; set; }
        public DbSet<institutemodel> tbl_institute { get; set; }
        public DbSet<stagemodel> tbl_stage { get; set; }
        public DbSet<classmodel> tbl_class { get; set; }
        public DbSet<divisionmodel> tbl_division { get; set; }
        public DbSet<programmodel> tbl_program { get; set; }
        public DbSet<prgm_judgement_criteria_model> tbl_prgm_judgement_criteria { get; set; }
        public DbSet<usermodel> tbl_user { get; set; }
        public DbSet<judgemodel> tbl_judge { get; set; }

        public DbSet<judges_prgmmodel> tbl_judge_prgm { get; set; }
        public DbSet<studentmodel> tbl_student { get; set; }

        public DbSet<student_prgmmodel> tbl_student_prgm { get; set; }
        public DbSet<judgement_criteriamodel> tbl_judgement_criteria { get; set; }
        public DbSet<prgm_participants> tbl_prgm_participants { get; set; }
        public DbSet<prgm_pointmodel> tbl_prgm_point { get; set; }
        public DbSet<appversionmodel> tbl_app_version { get; set; }
        public DbSet<notificationmodel> tbl_notification { get; set; }
        public DbSet<notification_read_status> tbl_notification_read_status { get; set; }
        public DbSet<role_model> tbl_role { get; set; }
        public DbSet<hierarchy> tbl_hierarchy { get; set; }

        public DbSet<tbl_privilage> tbl_privilage { get; set; }
        public DbSet<participants_type> tbl_participants_type { get; set; }
        public DbSet<gradetype_model> tbl_gradetype { get; set; }
        public DbSet<grademodel> tbl_grade { get; set; }
        public DbSet<group_members> tbl_group_members { get; set; }
        public DbSet<position_pointmodel> tbl_position_point { get; set; }
        public DbSet<event_registration> tbl_event_registration { get; set; }
        public DbSet<sponsor> tbl_sponsor { get; set; }
        public DbSet<participant_history> tbl_participant_history { get; set; }
        public DbSet<academic_year_model> tbl_academic_year { get; set; }


    }
}
