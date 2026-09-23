using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class gradeController : ControllerBase
    {

        private readonly kalanjaliDbContext _context;

        public gradeController(kalanjaliDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Route("Postgrade")]
        public async Task<ActionResult> Postgrade([FromBody] grademodel request)
        {
            if (_context.tbl_grade == null)
            {
                return Ok("Entity set '_context.tbl_grade' is null.");
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
            bool gradeExists = _context.tbl_grade.Any(g => g.grade == request.grade && g.gradetype_id==request.gradetype_id && g.point==request.point && g.status=="active");

            if (gradeExists)
            {
                return Ok(new
                {
                    status = false,
                    message = $"Grade '{request.grade}' is already assigned to a point range."
                });
            }
            //
            var division = new grademodel
            {
                grade = request.grade,
                gradetype_id= request.gradetype_id,     
                from_point = request.from_point,
                to_point = request.to_point,
                addedon = DateTime.Now,

                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                point = request.point,
                ac_year_id = request.ac_year_id
            };

            _context.tbl_grade.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }

        [HttpPut]
        [Route("update_grade")]
        public async Task<ActionResult> update_grade([FromBody] grademodel request)
        {
            var data = await _context.tbl_grade.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "data not found" });
            }



            if (!string.IsNullOrEmpty(request.grade))
            {
                data.grade = request.grade;
            }

            if (request.from_point.HasValue)
            {
                data.from_point = request.from_point.Value;
            }
            if (request.to_point.HasValue)
            {
                data.to_point = request.to_point.Value;
            }

            if (request.point.HasValue)
            {
                data.point = request.point.Value;
            }


            if (request.gradetype_id.HasValue)
            {
                data.gradetype_id = request.gradetype_id.Value;
            }

            if (request.modified_by.HasValue)
            {
                data.modified_by = request.modified_by.Value;
            }
            if (!string.IsNullOrEmpty(request.modified_type))
            {
                data.modified_type = request.modified_type;
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
        [Route("get_grade")]
     

        public async Task<ActionResult> get_grade(int? id, int? gradetype_id,int? ac_year_id)
        {
            // Check if the DbSet is null
            if (_context.tbl_grade == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Query to retrieve categories
            var query = from g in _context.tbl_grade

                        join t in _context.tbl_gradetype on g.gradetype_id equals t.id into gt
                        from gd in gt.DefaultIfEmpty()
                        where g.status == "active"
                        select new
                        {
                            g.id,
                            g.gradetype_id,
                            gd.name,
                            g.grade,
                            g.from_point,

                            g.to_point,
                            g.point,
                            g.ac_year_id

                        };
            // If an ID is provided, filter by that ID
            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }
            if (gradetype_id.HasValue)
            {
                query = query.Where(g => g.gradetype_id == gradetype_id.Value);
            }
            if (ac_year_id.HasValue)
            {
                query = query.Where(g => g.ac_year_id == ac_year_id.Value);
            }
            if (query == null || !query.Any())
            {
                return Ok(new { status = false, message = "No data found" });
            }

            // Return the result
            return Ok(new { status = true, message = "Success", data = query });
        }




        [HttpDelete]
        [Route("Delete_grade")]
        public async Task<IActionResult> Delete_grade([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_grade == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_grade ' is null.");
            }

            var eventItem = await _context.tbl_grade.FindAsync(id);

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


        //get grade type
        [HttpGet]
        [Route("get_gradetype")]
        public async Task<ActionResult> get_gradetype(int? id)
        {
            var query = _context.tbl_gradetype.Where(h => h.delete_status==0);

            if (id.HasValue)
            {
                query = query.Where(h => h.id == id.Value);
            }

            var roles = await query.Select(h => new
            {
                id = h.id,
                grade = h.name
              
            }).ToListAsync();

            return Ok(new { status = true, data = roles });
        }

        //delete grade_type
        [HttpDelete]
        [Route("Delete_gradetype")]
        public async Task<IActionResult> Delete_gradetype([FromForm] int id)
        {
            if (_context.tbl_gradetype == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_gradetype ' is null.");
            }

            var eventItem = await _context.tbl_gradetype.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }



            // Mark as deleted in the database
            eventItem.delete_status = 1;
          
            _context.Entry(eventItem).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }



    }
}
