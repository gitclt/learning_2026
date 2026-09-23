using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class judgementcriteriaController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public judgementcriteriaController(kalanjaliDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Route("Postjudgementcriteria")]
        public async Task<ActionResult> Postjudgementcriteria([FromBody] judgement_criteriamodel request)
        {
            if (_context.tbl_judgement_criteria == null)
            {
                return Problem("Entity set '_context.tbl_judgement_criteria' is null.");
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

            // Duplicate check by name (case-insensitive)
            var exists = await _context.tbl_judgement_criteria
                .AnyAsync(j => j.name.ToLower() == request.name.ToLower());

            if (exists)
            {
                return Ok(new
                {
                    status = false,
                    message = "Judgement criteria already exists."
                });
            }

            var division = new judgement_criteriamodel
            {
              
                addedon = DateTime.Now,
                name=request.name,
                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
            };

            _context.tbl_judgement_criteria.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully",id=division.id });
        }

        [HttpPut]
        [Route("update_judgementcriteria")]
        public async Task<ActionResult> update_judgementcriteria([FromBody] judgement_criteriamodel request)
        {
            var data = await _context.tbl_judgement_criteria.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "data not found" });
            }



            if (!string.IsNullOrEmpty(request.name))
            {
                data.name = request.name;
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
        [Route("get_judgement_criteria")]
        public async Task<ActionResult> get_judgement_criteria(int? id)
        {
            // Check if the DbSet is null
            if (_context.tbl_class == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Query to retrieve categories
            var query = _context.tbl_judgement_criteria.Where(d => d.status.ToLower() == "active"); ;

            // If an ID is provided, filter by that ID
            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }


            var categories = await query
                .Select(g => new
                {
                    id = g.id,
                   name= g.name,        
                    addedon = g.addedon,
                    addedtype = g.addedtype,

                    added_by = g.added_by,



                })
                .ToListAsync();

            //  return Ok(categories);
            // Check if categories were found
            if (categories == null || !categories.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Return the result
            return Ok(new { status = true, message = "Success", data = categories });
        }

        [HttpDelete]
        [Route("Delete_judgement_criteria")]
        public async Task<IActionResult> Delete_judgement_criteria([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_judgement_criteria == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_judgement_criteria' is null.");
            }

            var eventItem = await _context.tbl_judgement_criteria.FindAsync(id);

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
