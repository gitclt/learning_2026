using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class prgmjudgmentcriteriaController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public prgmjudgmentcriteriaController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Post_judgement_criteria")]
        public async Task<ActionResult> Post_judgement_criteria([FromBody] List<prgm_judgement_criteria_model> requests)
        {
            if (_context.tbl_prgm_judgement_criteria == null)
            {
                return Problem("Entity set 'kalanjaliDbContext.tbl_prgm_judgement_criteria' is null.");
            }

            if (requests == null || !requests.Any())
            {
                return BadRequest(new { status = false, message = "No data provided." });
            }

            var divisions = requests.Select(request => new prgm_judgement_criteria_model
            {
                prgm_id = request.prgm_id,
                judgement_criteria = request.judgement_criteria,
                name = request.name,        
                point=request.point,    
                addedon = DateTime.Now,
            }).ToList();

            _context.tbl_prgm_judgement_criteria.AddRange(divisions);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }

        [HttpPut]
        [Route("update_judgement_criteria")]
        public async Task<ActionResult> update_judgement_criteria([FromBody] List<prgm_judgement_criteria_model> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return Ok(new { status = false, message = "No data provided for update" });
            }

            // Extract distinct subadmin_id values from the request list
            var prgmIds = requests.Select(r => r.prgm_id).Distinct().ToList();

            // Fetch all records that match any of the provided subadmin_id values
            var existingRecords = _context.tbl_prgm_judgement_criteria
                                          .Where(x => prgmIds.Contains(x.prgm_id))
                                          .ToList();

            if (existingRecords.Count > 0)
            {
                // Delete all existing records that match the subadmin_id
                _context.tbl_prgm_judgement_criteria.RemoveRange(existingRecords);
                await _context.SaveChangesAsync();
            }

            // Insert new records from the request
            _context.tbl_prgm_judgement_criteria.AddRange(requests);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("get_judgement_criteria")]
        public async Task<ActionResult> get_judgement_criteria(int? id)
        {
            if (_context.tbl_judgement_criteria == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var query = from d in _context.tbl_judgement_criteria
                        join u in _context.tbl_user on d.added_by equals u.id into usr
                        from uu in usr.DefaultIfEmpty()
                        where d.status == "active"
                        select new
                        {
                            d.id,
                             d.name,
                            d.added_by,
                           
                            d.addedtype,
                            d.addedon
                        };

            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }

            var result = await query.ToListAsync(); 

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
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
