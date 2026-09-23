using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class classController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public classController(kalanjaliDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Route("Postclass")]
        public async Task<ActionResult> Postclass([FromBody] classmodel request)
        {
            if (_context.tbl_class == null)
            {
                return Ok("Entity set '_context.tbl_class' is null.");
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


            var division = new classmodel
            {
                @class = request.@class,
                institute_id=request.institute_id,  
                addedon = DateTime.Now,

                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                type = request.type,    
            };

            _context.tbl_class.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }


        [HttpPut]
        [Route("update_class")]
        public async Task<ActionResult> update_class([FromBody] classmodel request)
        {
            var data = await _context.tbl_class.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "data not found" });
            }



            if (!string.IsNullOrEmpty(request.@class))
            {
                data.@class = request.@class;
            }


            if (!string.IsNullOrEmpty(request.modified_type))
            {
                data.modified_type = request.modified_type;
            }
            if (request.modified_by.HasValue)
            {
                data.modified_by = request.modified_by.Value;
            }

            if (request.institute_id.HasValue) data.institute_id = request.institute_id.Value;
            if (!string.IsNullOrEmpty(request.type))
            {
                data.type = request.type;
            }

            request.modifiedon = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("get_class")]
        public async Task<ActionResult> get_class(int? id,int? institute_id)
        {
            if (_context.tbl_class == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            //var query = _context.tbl_class.Where(d => d.status.ToLower() == "active"); ;

            var query = from c in _context.tbl_class

                        join i in _context.tbl_institute on c.institute_id equals i.id into inst
                        from it in inst.DefaultIfEmpty()

                        join u in _context.tbl_user on c.added_by equals u.id into usr
                        from uu in usr.DefaultIfEmpty()

                        where c.status == "active"
                        select new
                        {
                            c.id,
                            c.@class,
                            c.type,
                            c.addedon,
                            c.addedtype,
                            c.added_by,
                            c.institute_id,
                            it.name,

                        };

            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }

            if (institute_id.HasValue)
            {
                query = query.Where(g => g.institute_id == institute_id.Value);
            }


            var categories = await query
                .Select(g => new
                {
                    id = g.id,
                    @class = g.@class,
                  addedon=g.addedon,
                    addedtype = g.addedtype,
                    institute_id = g.institute_id,      
                    name=g.name,        
                    added_by = g.added_by,



                })
                .ToListAsync();

            //  return Ok(categories);
            if (categories == null || !categories.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Return the result
            return Ok(new { status = true, message = "Success", data = categories });
        }

        [HttpDelete]
        [Route("Delete_class")]
        public async Task<IActionResult> Delete_class([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_class == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_class' is null.");
            }

            var eventItem = await _context.tbl_class.FindAsync(id);

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
