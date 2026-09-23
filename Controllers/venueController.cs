using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class venueController : ControllerBase
    {

        private readonly kalanjaliDbContext _context;

        public venueController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Postvenue")]
        public async Task<ActionResult> Postvenue([FromBody] venuemodel request)
        {
            if (_context.tbl_venue == null)
            {
                return Problem("Entity set '_context.tbl_venue' is null.");
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


            var division = new venuemodel
            {
                institute_id = request.institute_id,

                venue = request.venue,
                lattitude = request.lattitude,
                longitude = request.longitude,
                location = request.location,
                remark = request.remark,
                addedon = DateTime.Now,

                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
            };

            _context.tbl_venue.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }

        [HttpPut]
        [Route("update_venue")]
        public async Task<ActionResult> update_venue([FromBody] venuemodel request)
        {
            var data = await _context.tbl_venue.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "venue not found" });
            }

            if (request.institute_id.HasValue)
            {
                data.institute_id = request.institute_id.Value;
            }


            if (!string.IsNullOrEmpty(request.venue))
            {
                data.venue = request.venue;
            }

            if (!string.IsNullOrEmpty(request.remark))
            {
                data.remark = request.remark;
            }
            if (!string.IsNullOrEmpty(request.lattitude))
            {
                data.lattitude = request.lattitude;
            }
            if (!string.IsNullOrEmpty(request.longitude))
            {
                data.longitude = request.longitude;
            }

            if (!string.IsNullOrEmpty(request.location))
            {
                data.location = request.location;
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
        [Route("get_venue")]
        public async Task<ActionResult> get_venue(int? id)
        {
            // Check if the DbSet is null
            if (_context.tbl_venue == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Query to retrieve categories
            var query = from d in _context.tbl_venue

                        join i in _context.tbl_institute on d.institute_id equals i.id into ins
                        from inn in ins.DefaultIfEmpty()

                        join u in _context.tbl_user on d.added_by equals u.id into usr
                        from ur in usr.DefaultIfEmpty()

                        where d.status == "active" // Ensure this condition is applied correctly
                        select new
                        {
                            d.id,
                            d.institute_id,
                          institute=  inn.name,
                           d.lattitude,
                           d.longitude, 
                            d.location, 
                            d.remark,
                            d.venue,
                            d.added_by,
                            ur.name,
                            d.addedtype,
                            d.addedon,
                        };
            // If an ID is provided, filter by that ID
            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
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
        [Route("Delete_venue")]
        public async Task<IActionResult> Delete_venue([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_venue == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_venue' is null.");
            }

            var eventItem = await _context.tbl_venue.FindAsync(id);

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
