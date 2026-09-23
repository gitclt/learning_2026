using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class participantstypeController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public participantstypeController(kalanjaliDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Route("Postparticipantstype")]
        public async Task<ActionResult> Postparticipantstype([FromBody] participants_type request)
        {
            if (_context.tbl_participants_type == null)
            {
                return Ok("Entity set '_context.tbl_participants_type' is null.");
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


            var division = new participants_type
            {
                type = request.type,
                class_id = request.class_id,
                addedon = DateTime.Now,

                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
            };

            _context.tbl_participants_type.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }


        [HttpPut]
        [Route("update_participantstype")]
        public async Task<ActionResult> update_participantstype([FromBody] participants_type request)
        {
            var data = await _context.tbl_participants_type.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "data not found" });
            }



            if (!string.IsNullOrEmpty(request.type))
            {
                data.type = request.type;
            }


            if (!string.IsNullOrEmpty(request.class_id))
            {
                data.class_id = request.class_id;
            }
            if (request.modified_by.HasValue)
            {
                data.modified_by = request.modified_by.Value;
            }
            if (!string.IsNullOrEmpty(request.modified_type))
            {
                data.modified_type = request.modified_type;
            }



            request.modifiedon = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("get_participantstype")]
        public async Task<ActionResult> get_participantstype(int? id)
        {
            if (_context.tbl_participants_type == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

         

            var participants = await _context.tbl_participants_type
                .Where(d => d.status == "active")
                .ToListAsync();

            if (id.HasValue)
            {
                participants = participants.Where(d => d.id == id.Value).ToList();
            }

         

            var classList = await _context.tbl_class.ToListAsync();

            var categories = participants
       .Select(d =>
       {
           var classIds = d.class_id?
               .Split(',')
               .Select(s => int.TryParse(s, out int val) ? val : (int?)null)
               .Where(val => val.HasValue)
               .Select(val => val.Value)
               .ToList() ?? new List<int>();

           var matchedClasses = classList
               .Where(c => classIds.Contains(c.id))
               .Select(c => c.@class)
               .ToList();


           return new
           {
               id = d.id,
               participants_type = d.type,
               addedon = d.addedon,
               addedtype = d.addedtype,
               added_by = d.added_by,
               class_id = d.class_id,
               classname = matchedClasses.Any() ? string.Join(", ", matchedClasses) : null,
           };
       })
       .ToList();


            if (!categories.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = categories });
        }

        [HttpDelete]
        [Route("Delete_participantstype")]
        public async Task<IActionResult> Delete_participantstype([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_participants_type == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_participants_type' is null.");
            }

            var eventItem = await _context.tbl_participants_type.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }



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
