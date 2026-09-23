using System.ComponentModel.DataAnnotations;

namespace kalanjali_api.Model
{
    public class event_registration
    {
        public int id { get; set; }
        [MaxLength(100, ErrorMessage = "name cannot exceed 100 characters.")]

        public string? name { get; set; }  
        public string? mobile { get; set; }

        [MaxLength(100, ErrorMessage = "email cannot exceed 100 characters.")]

        public string? email { get; set; }  
        public DateTime? addedon { get; set; }  
        public string? status { get; set; }  
        public DateTime? deletedon { get; set; }  
    }
}
