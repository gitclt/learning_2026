using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class stageController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public stageController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Poststage")]
        public async Task<ActionResult> Poststage([FromBody] stagemodel request)
        {
            if (_context.tbl_stage == null)
            {
                return Problem("Entity set '_context.tbl_stage' is null.");
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


            var division = new stagemodel
            {
                institute_id = request.institute_id,

                venue_id = request.venue_id,
                event_id = request.event_id,
                stage_name = request.stage_name,
             
                addedon = DateTime.Now,

                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                color_code = request.color_code,  
                //ac_year_id = request.ac_year_id
            };

            _context.tbl_stage.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }

        [HttpPut]
        [Route("update_stage")]
        public async Task<ActionResult> update_stage([FromBody] stagemodel request)
        {
            var data = await _context.tbl_stage.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "stage not found" });
            }

            if (request.institute_id.HasValue)
            {
                data.institute_id = request.institute_id.Value;
            }
            if (request.venue_id.HasValue)
            {
                data.venue_id = request.venue_id.Value;
            }
            if (request.event_id.HasValue)
            {
                data.event_id = request.event_id.Value;
            }


            if (!string.IsNullOrEmpty(request.stage_name))
            {
                data.stage_name = request.stage_name;
            }

            if (!string.IsNullOrEmpty(request.color_code))
            {
                data.color_code = request.color_code;
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
        [Route("view_stage")]
        public async Task<ActionResult> view_stage(int? id,int? institute_id,int? venue_id,int? event_id)
        {
            // Check if the DbSet is null
            if (_context.tbl_stage == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Query to retrieve categories
            var query = from s in _context.tbl_stage

                                join e in _context.tbl_event on s.event_id equals e.id into cass
                                from sa in cass.DefaultIfEmpty()
                                //join l in _context.tbl_institute on s.institute_id equals l.id into ass
                                //from sc in ass.DefaultIfEmpty()

                        join v in _context.tbl_venue on s.venue_id equals v.id into vv
                        from vn in vv.DefaultIfEmpty()

                        join u in _context.tbl_user on s.added_by equals u.id into uu
                        from ur in uu.DefaultIfEmpty()

                        where s.status == "active"
                                select new
                                {
                                    s.id,
                                    s.stage_name,
                                    s.venue_id,
                                    vn.venue,
                                   // s.institute_id,
                                   //institute= sc.name,

                                    s.event_id,
                                    sa.event_name,
                                    s.status,
                                    s.added_by,
                                    ur.name,
                                    s.addedtype,
                                    s.addedon,
                                    s.color_code,
                                };
            // If an ID is provided, filter by that ID
            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }
            //if (institute_id.HasValue)
            //{
            //    query = query.Where(g => g.institute_id == institute_id.Value);
            //}

            if (event_id.HasValue)
            {
                query = query.Where(g => g.event_id == event_id.Value);
            }


            if (venue_id.HasValue)
            {
                query = query.Where(g => g.venue_id == venue_id.Value);
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
        [Route("Delete_stage")]
        public async Task<IActionResult> Delete_stage([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_stage == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_stage' is null.");
            }

            var eventItem = await _context.tbl_stage.FindAsync(id);

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
