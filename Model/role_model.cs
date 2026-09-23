using System.ComponentModel.DataAnnotations.Schema;

namespace kalanjali_api.Model
{

    [Table("tbl_role")]

    public class role_model
    {
        public int id { get; set; }
        public string? name { get; set; }
        public int? hierarchy_id { get; set; }

        public int? delete_status { get; set; }
    }
}
