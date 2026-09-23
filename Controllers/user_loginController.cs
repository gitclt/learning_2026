using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class user_loginController : ControllerBase
    {

        private readonly kalanjaliDbContext _context;

        public user_loginController(kalanjaliDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Route("Postuser")]
        public async Task<ActionResult> Postuser([FromBody] usermodel request)
        {
            if (_context.tbl_user == null)
            {
                return Ok("Entity set '_context.tbl_user' is null.");
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

            // Check if username and password already exist
            var userExists = await _context.tbl_user
                .AnyAsync(u => u.username == request.username && u.password == request.password && u.type==request.type);

            if (userExists)
            {
                return Ok(new
                {
                    status = false,
                    message = "Username and Password already exist."
                });
            }

            var division = new usermodel
            {
                name = request.name,
                username = request.username,
                phone_no=request.phone_no,
                password = request.password,
                addedon = DateTime.Now,
                type = request.type,    
                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                stage_id=request.stage_id
            };

            _context.tbl_user.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }

        [HttpPut]
        [Route("update_user")]
        public async Task<ActionResult> update_user([FromBody] usermodel request)
        {
            var data = await _context.tbl_user.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "stage not found" });
            }

            if (!string.IsNullOrEmpty(request.name))
            {
                data.name = request.name;
            }
            if (!string.IsNullOrEmpty(request.phone_no))
            {
                data.phone_no = request.phone_no;
            }
            if (!string.IsNullOrEmpty(request.username))
            {
                data.username = request.username;
            }
            if (!string.IsNullOrEmpty(request.password))
            {
                data.password = request.password;
            }
            if (!string.IsNullOrEmpty(request.type))
            {
                data.type = request.type;
            }
            if (request.stage_id.HasValue)
            {
                data.stage_id = request.stage_id.Value;
            }


            request.modifiedon = DateTime.Now;

            // Save changes to the database
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }


        [HttpGet] 
        [Route("get_user")]
       
        public async Task<ActionResult> get_user(int? id, int? stage_id)
        {
            if (_context.tbl_user == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            //if (!stage_id.HasValue)
            //{
            //    return Ok(new { status = false, message = "stage_id is mandatory" });
            //}

            var query = from u in _context.tbl_user

                        join s in _context.tbl_stage on u.stage_id equals s.id into st
                        from sta in st.DefaultIfEmpty()
                        where u.status == "active" && u.type != "student" && u.type != "judge" && u.type != "institute" && u.type != "admin"

                        select new
                        {
                            u.id,
                            u.stage_id,
                            sta.stage_name,
                            u.type,
                            u.username,
                            u.name,
                            u.password,

                        };
            // If an ID is provided, filter by that ID
            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }
            if (stage_id.HasValue)
            {
                query = query.Where(g => g.stage_id == stage_id.Value);
            }

            if (query == null || !query.Any())
            {
                return Ok(new { status = false, message = "No data found" });
            }

            // Return the result
            return Ok(new { status = true, message = "Success", data = query });
        }



        [HttpDelete]
        [Route("Delete_user")]
        public async Task<IActionResult> Delete_user([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_user == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_user ' is null.");
            }

            var eventItem = await _context.tbl_user.FindAsync(id);

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
