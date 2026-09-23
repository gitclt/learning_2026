using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class position_pointController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public position_pointController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Post_positionpoint")]
        public async Task<ActionResult> Post_positionpoint([FromBody] position_pointmodel request)
        {
            if (_context.tbl_position_point == null)
            {
                return Ok("Entity set '_context.tbl_position_point' is null.");
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
            //grade validation
            bool gradeExists = _context.tbl_position_point.Any(g => g.program_type == request.program_type && g.position==request.position && g.point==request.point);

            if (gradeExists)
            {
                return Ok(new
                {
                    status = false,
                    message = $"Point '{request.point}' is already assigned to a '{request.position}'"
                });
            }
            //
            var division = new position_pointmodel
            {
                program_type = request.program_type,
                position = request.position,
                point = request.point,
                addedon = DateTime.Now,
                ac_year_id= request.ac_year_id,
                status = "active"
              
            };

            _context.tbl_position_point.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }


        [HttpPut]
        [Route("update_positionpoint")]
        public async Task<ActionResult> update_positionpoint([FromBody] position_pointmodel request)
        {
            var data = await _context.tbl_position_point.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "data not found" });
            }



            if (!string.IsNullOrEmpty(request.program_type))
            {
                data.program_type = request.program_type;
            }

            if (!string.IsNullOrEmpty(request.position))
            {
                data.position = request.position;
            }
            if (request.ac_year_id.HasValue)
            {
                data.ac_year_id = request.ac_year_id.Value;
            }
            
            request.modifiedon = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("get_positionpoint")]
        public async Task<ActionResult> get_positionpoint(int? id)
        {
            var query = _context.tbl_position_point.Where(h => h.status == "active");

            if (id.HasValue)
            {
                query = query.Where(h => h.id == id.Value);
            }

            var roles = await query.Select(h => new
            {
                id = h.id,
                program_type = h.program_type,
              position=h.position,  
                point = h.point,
                ac_year_id=h.ac_year_id
            }).ToListAsync();

            return Ok(new { status = true, data = roles });
        }

        [HttpDelete]
        [Route("Delete_positionpoint")]
        public async Task<IActionResult> Delete_positionpoint([FromForm] int id)
        {
            if (_context.tbl_position_point == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_position_point ' is null.");
            }

            var eventItem = await _context.tbl_position_point.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            eventItem.status = "deleted";
         
            eventItem.deletedon = DateTime.Now;

            _context.Entry(eventItem).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }




    }
}
