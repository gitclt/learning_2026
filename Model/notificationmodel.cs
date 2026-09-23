using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace kalanjali_api.Model
{
    public class notificationmodel
    {
        public int id { get; set; }

        [MaxLength(100, ErrorMessage = "user_type cannot exceed 50 characters.")]
        public string? user_type { get; set; }

        public string? type { get; set; }

        public string? heading { get; set; }
        public string? description { get; set; }
        public string? image { get; set; }
        [NotMapped]
        public string? data { get; set; }
        [NotMapped]
        public string? is_img_chged { get; set; }
        [NotMapped]
        public string? img_oldname { get; set; }
        public string? notification_type { get; set; }


        public int? user_id { get; set; }
        public int? delete_status { get; set; }
        public int? deleted_by { get; set; }
        public DateTime? deleted_on { get; set; }
      
        public DateTime? date { get; set; }
    }
}
