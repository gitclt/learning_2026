using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SponsorController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public SponsorController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Postsponsor")]
        public async Task<ActionResult<sponsor>> Postsponsor(sponsor request)
        {
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

            string fileName = null;

            try
            {
                if (!string.IsNullOrEmpty(request.image_data) && !string.IsNullOrEmpty(request.image))
                {
                    //string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "sponsor");
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/sponsor");


                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string base64Image = request.image_data;
                    if (base64Image.Contains(","))
                    {
                        base64Image = base64Image.Substring(base64Image.IndexOf(",") + 1);
                    }

                    byte[] imgBytes = Convert.FromBase64String(base64Image);
                    fileName = request.image;
                    string imagePath = Path.Combine(uploadsFolder, fileName);

                    if (System.IO.File.Exists(imagePath))
                    {
                        return Conflict(new { status = false, message = "File already exists." });
                    }

                    await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);
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

           
            var institute = new sponsor
            {
                name = request.name,
              
                addedon = DateTime.Now,
                image = fileName,
                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype
            };

            _context.tbl_sponsor.Add(institute);
            await _context.SaveChangesAsync();

          

            return Ok(new
            {
                status = true,
                message = "Data added successfully"
            });
        }

        [HttpPut]
        [Route("update_sponsor")]
        public async Task<ActionResult> update_sponsor(sponsor events)
        {
            if (_context.tbl_sponsor == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_sponsor' is null.");
            }

            var existingEvent = await _context.tbl_sponsor.FindAsync(events.id);
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
                var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/sponsor", events.img_oldname);
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/sponsor");
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
            if (!string.IsNullOrEmpty(events.name))
            {
                existingEvent.name = events.name;
            }

            if (events.modified_by.HasValue)
            {
                existingEvent.modified_by = events.modified_by.Value;
            }

            if (!string.IsNullOrEmpty(events.modified_type))
            {
                existingEvent.modified_type = events.modified_type;
            }

            existingEvent.modifiedon = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }


        [HttpGet]
        [Route("view_sponsor")]
        public async Task<ActionResult> view_sponsor(int? id)
        {
            // Check if the DbSet is null
            if (_context.tbl_sponsor == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            //  return Ok(baseUrl);
            var query = from e in _context.tbl_sponsor

                    
                        where e.status == "active" // Ensure this condition is applied correctly
                        select new
                        {
                            id = e.id,
                            name = e.name,
                          
                            image = e.image,
                            imageurl = !string.IsNullOrEmpty(e.image) ? $"{baseUrl}uploads/sponsor/{e.image}" : null

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
        [Route("Delete_sponsor")]
        public async Task<IActionResult> Delete_sponsor([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_sponsor == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_sponsor' is null.");
            }

            var eventItem = await _context.tbl_sponsor.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Delete the image file from the server
            if (!string.IsNullOrEmpty(eventItem.image))
            {
                string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/sponsor", eventItem.image);
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
            eventItem.deletedon = DateTime.Now;

            _context.Entry(eventItem).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data and image deleted successfully" });
        }




    }
}
