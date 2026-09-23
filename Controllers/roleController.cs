using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class roleController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public roleController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("role_add")]
        public async Task<ActionResult> role_add(role_model request)
        {
            var roles = new role_model
            {
                name = request.name,
                hierarchy_id = request.hierarchy_id,
                delete_status = 0
            };

            _context.tbl_role.Add(roles);
            _context.SaveChanges();

            return Ok(new { status = true, message = "Data added successfully" });
        }

        [HttpPut]
        [Route("Edit_role")]
        public async Task<IActionResult> Edit_role(role_model role)
        {
            if (role.id == null)
            {
                return BadRequest("ID mismatch");
            }

            var existingrole = await _context.tbl_role.FindAsync(role.id);

            // Check if the color exists
            if (existingrole == null)
            {
                return NotFound("data not found");
            }

            // Update properties
            existingrole.name = role.name; // Assuming your color model has a property called color_name
            existingrole.hierarchy_id = role.hierarchy_id;
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpDelete]
        [Route("Delete_role")]
        public async Task<IActionResult> Delete_role([FromForm] int id)
        {
            if (_context.tbl_role == null)
            {
                return NotFound();
            }

            var role = await _context.tbl_role.FindAsync(id);

            if (role == null)
            {
                return NotFound("data not found");
            }

            role.delete_status = 1;
            // Mark the entity as modified
            _context.Entry(role).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }


        [HttpGet]
        [Route("view_role")]
        public async Task<ActionResult> view_role()
        {
            var roles = await (from h in _context.tbl_hierarchy
                               join r in _context.tbl_role
                               on h.id equals r.hierarchy_id
                               where r.delete_status == 0
                               select new
                               {
                                   id = r.id,
                                   hierarchy_id = r.hierarchy_id,
                                   name = r.name,
                                   hierarchy = h.name,
                                   //  delete_status = sub.delete_status
                               }).ToListAsync();
            return Ok(new { status = true, data = roles });


        }

    }
}
