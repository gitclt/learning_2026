using System.ComponentModel.DataAnnotations;

namespace kalanjali_api.Model
{
    public class venuemodel
    {
        public int id { get; set; }
        public int? institute_id { get; set; }

        [MaxLength(100, ErrorMessage = "Venue must not exceed 100 characters.")]

        public string? venue { get; set; }

        [MaxLength(100, ErrorMessage = "Latitude must not exceed 100 characters.")]

        public string? lattitude { get; set; }

        [MaxLength(100, ErrorMessage = "Longitude must not exceed 100 characters.")]

        public string? longitude { get; set; }

        [MaxLength(100, ErrorMessage = "Location must not exceed 100 characters.")]

        public string? location { get; set; }

        [MaxLength(100, ErrorMessage = "Remark must not exceed 100 characters.")]

        public string? remark { get; set; }
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
    }
}
