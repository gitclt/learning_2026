using System.ComponentModel.Design;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class eventController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public eventController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Postevent")]
        public async Task<ActionResult<eventmodel>> Postevent(eventmodel request)
        {
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

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/event");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var base64Image = request.image_data;  // base64 string
            var filename = request.image;          // image file name like "arts.jpg"

            if (!string.IsNullOrEmpty(base64Image) && !string.IsNullOrEmpty(filename))
            {
                try
                {
                    if (base64Image.Contains(","))
                    {
                        base64Image = base64Image.Substring(base64Image.IndexOf(",") + 1);
                    }

                    byte[] imgBytes = Convert.FromBase64String(base64Image);
                    string imagePath = Path.Combine(uploadsFolder, filename);

                    if (!System.IO.File.Exists(imagePath))
                    {
                        await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);
                    }
                    else
                    {
                        return Conflict(new { status = false, message = "File already exists." });
                    }
                }
                catch (FormatException ex)
                {
                    return BadRequest(new { status = false, message = "Invalid base64 string.", error = ex.Message });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { status = false, message = "An error occurred while uploading the image.", error = ex.Message });
                }
            }

            var division = new eventmodel
            {
                event_name = request.event_name,
                short_name = request.short_name,
                venue_id = request.venue_id,
                from_date = request.from_date,
                to_date = request.to_date,
                addedon = DateTime.Now,
                image = filename, // save the filename if provided
                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                students_per_institute=request.students_per_institute
            };

            _context.tbl_event.Add(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }


        [HttpPut]
        [Route("edit_event")]
        public async Task<ActionResult> edit_event(eventmodel events)
        {
            if (_context.tbl_event == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_event' is null.");
            }

            var existingEvent = await _context.tbl_event.FindAsync(events.id);
            if (existingEvent == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var isImageChanged = events.is_img_chged?.ToLower() == "yes";

            if (isImageChanged)
            {
                if (string.IsNullOrEmpty(events.image_data))
                {
                    return BadRequest(new { status = false, message = "Image update requested but no image data provided." });
                }

                // Delete old image
                var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/event", events.img_oldname);
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/event");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                try
                {
                    byte[] imgBytes = Convert.FromBase64String(events.image_data);
                    string imagePath = Path.Combine(uploadsFolder, events.image);

                    // Check for duplicate file
                    if (System.IO.File.Exists(imagePath))
                    {
                        return Conflict(new { status = false, message = "File already exists." });
                    }

                    await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);

                    // ✅ Save image filename to database
                    existingEvent.image = events.image;
                }
                catch (FormatException ex)
                {
                    return BadRequest(new { status = false, message = "Invalid base64 string.", error = ex.Message });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { status = false, message = "An error occurred while uploading the image.", error = ex.Message });
                }
            }

            // Update other fields
            if (events.from_date.HasValue)
            {
                existingEvent.from_date = events.from_date.Value;
            }

            if (events.to_date.HasValue)
            {
                existingEvent.to_date = events.to_date.Value;
            }

            if (!string.IsNullOrEmpty(events.event_name))
            {
                existingEvent.event_name = events.event_name;
            }

            if (!string.IsNullOrEmpty(events.short_name))
            {
                existingEvent.short_name = events.short_name;
            }

            if (events.venue_id.HasValue)
            {
                existingEvent.venue_id = events.venue_id.Value;
            }

            if (events.modified_by.HasValue)
            {
                existingEvent.modified_by = events.modified_by.Value;
            }

            if (!string.IsNullOrEmpty(events.modified_type))
            {
                existingEvent.modified_type = events.modified_type;
            }

            if (events.students_per_institute.HasValue)
            {
                existingEvent.students_per_institute = events.students_per_institute.Value;
            }

            existingEvent.modifiedon = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }


        [HttpGet]
        [Route("view_event")]
        public async Task<ActionResult> view_event(int? id)
        {
            // Check if the DbSet is null
            if (_context.tbl_event == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

          //  return Ok(baseUrl);
            var query = from e in _context.tbl_event

                        join v in _context.tbl_venue on e.venue_id equals v.id into ven
                        from vn in ven.DefaultIfEmpty()

                        where e.status == "active" && e.ac_year_id==3// Ensure this condition is applied correctly
                        select new
                        {
                            id = e.id,
                            event_name = e.event_name,
                            short_name = e.short_name,
                            from_date = e.from_date,
                            to_date = e.to_date,
                            venue_id = e.venue_id,
                            venue=vn.venue,
                            students_per_institute=e.students_per_institute,
                            image = e.image,
                            imageurl = !string.IsNullOrEmpty(e.image) ? $"{baseUrl}uploads/event/{e.image}" : null

                        };

            // If an ID is provided, filter by that ID
            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }

            var categories = await query.ToListAsync();

            if (categories == null || !categories.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Return the result
            return Ok(new { status = true, message = "Success", data = categories });
        }


        [HttpDelete]
        [Route("Delete_event")]
        public async Task<IActionResult> Delete_event([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_event == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_event' is null.");
            }

            var eventItem = await _context.tbl_event.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Delete the image file from the server
            if (!string.IsNullOrEmpty(eventItem.image))
            {
                string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/event", eventItem.image);
                if (System.IO.File.Exists(imagePath))
                {
                    try
                    {
                        System.IO.File.Delete(imagePath);
                    }
                    catch (Exception ex)
                    {
                        return StatusCode(500, new { status = false, message = "Failed to delete image from server.", error = ex.Message });
                    }
                }
            }

            // Mark as deleted in the database
            eventItem.status = "deleted";
            eventItem.deleted_by = deleted_by;
            eventItem.deleted_type = deleted_type;  
            eventItem.deletedon=DateTime.Now;   

            _context.Entry(eventItem).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data and image deleted successfully" });
        }


    }
}
