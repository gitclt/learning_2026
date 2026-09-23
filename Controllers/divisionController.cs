using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class divisionController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public divisionController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Postdivision")]
        public async Task<ActionResult> Postdivision([FromBody] divisionmodel request)
        {
            if (_context.tbl_division == null)
            {
                return Problem("Entity set '_context.tbl_division' is null.");
            }

            // Check for validation errors
            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Ok(new
                {
                    status = false,
                    message = string.Join(", ", errorMessages)
                });
            }


            var division = new divisionmodel
            {
                class_id = request.class_id,

                division = request.division,
                addedon = DateTime.Now,
                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
            };

            _context.tbl_division.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }

        [HttpPut]
        [Route("update_division")]
        public async Task<ActionResult> update_division([FromBody] divisionmodel request)
        {
            var data = await _context.tbl_division.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "data not found" });
            }

            if (request.class_id.HasValue)
            {
                data.class_id = request.class_id.Value;
            }


            if (!string.IsNullOrEmpty(request.division))
            {
                data.division = request.division;
            }

         
            if (!string.IsNullOrEmpty(request.modified_type))
            {
                data.modified_type = request.modified_type;
            }
            if (request.modified_by.HasValue)
            {
                data.modified_by = request.modified_by.Value;
            }
            request.modifiedon = DateTime.Now;

            // Save changes to the database
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("get_division")]
        public async Task<ActionResult> get_division(int? id, int? class_id,int? institute_id)
        {
            // Check if the DbSet is null
            if (_context.tbl_division == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Query to retrieve categories
            var query = from d in _context.tbl_division

                        join c in _context.tbl_class on d.class_id equals c.id into cass
                        from sa in cass.DefaultIfEmpty()

                        join u in _context.tbl_user on d.added_by equals u.id into usr
                        from uu in usr.DefaultIfEmpty()

                        where d.status == "active" 
                        select new
                        {
                            d.id,
                            d.class_id,
                            sa.@class,
                            sa.institute_id,
                            d.division,
                         
                            d.added_by,
                            uu.name,
                            d.addedtype,
                            d.addedon,
                        };
            // If an ID is provided, filter by that ID
            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }
            if (class_id.HasValue)
            {
                query = query.Where(g => g.class_id == class_id.Value);
            }
            if (institute_id.HasValue)
            {
                query = query.Where(g => g.institute_id == institute_id.Value);
            }


            //  return Ok(categories);
            // Check if categories were found
            if (query == null || !query.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Return the result
            return Ok(new { status = true, message = "Success", data = query });
        }

        [HttpDelete]
        [Route("Delete_division")]
        public async Task<IActionResult> Delete_division([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_division == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_division' is null.");
            }

            var eventItem = await _context.tbl_division.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }



            // Mark as deleted in the database
            eventItem.status = "deleted";
            eventItem.deleted_by = deleted_by;
            eventItem.deleted_type = deleted_type;
            eventItem.deletedon = DateTime.Now;

            _context.Entry(eventItem).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }



    }
}
