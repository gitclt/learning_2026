using FirebaseAdmin.Messaging;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class notificationController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public notificationController(kalanjaliDbContext context)
        {
            _context = context;
        }


        [HttpPost]
        [Route("send_notification")]
        public async Task<ActionResult> send_notification(notificationmodel request)
        {
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/notification");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var base64Image = request.data;  
            var filename = request.image;         

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


            var notification = new notificationmodel
            {
                date = DateTime.Now,
                type = request.type,
                user_type = request.user_type,      
                heading = request.heading,
                description = request.description,
                image = request.image,
               // user_id = request.user_id,
                notification_type=request.notification_type,
                delete_status = 0
            };

            await _context.tbl_notification.AddAsync(notification);
            await _context.SaveChangesAsync();

            var topics = request.user_type.Split(','); // Split the comma-separated user types

            try
            {
                var messaging = FirebaseMessaging.DefaultInstance;

                foreach (var topic in topics)
                {
                    var message = new Message
                    {
                        Notification = new Notification
                        {
                            Title = request.heading,
                            Body = request.description,
                            ImageUrl = request.image
                        },
                        Topic = topic.Trim() // Trim to remove whitespace
                    };

                    string result = await messaging.SendAsync(message);
                    // Optionally log or store `result`
                }

                return Ok(new
                {
                    status = true,
                    message = "Notifications sent successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "Error while sending notifications.",
                    error = ex.Message
                });
            }

        }

        [HttpPut]
        [Route("edit_notification")]
        public async Task<ActionResult> edit_notification(notificationmodel events)
        {
            if (_context.tbl_notification == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_notification' is null.");
            }

            var existingEvent = await _context.tbl_notification.FindAsync(events.id);
            if (existingEvent == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var isImageChanged = events.is_img_chged?.ToLower() == "yes";

            if (isImageChanged)
            {
                if (string.IsNullOrEmpty(events.data))
                {
                    return BadRequest(new { status = false, message = "Image update requested but no image data provided." });
                }

                // Delete old image
                var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/notification", events.img_oldname);
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/notification");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                try
                {
                    byte[] imgBytes = Convert.FromBase64String(events.data);
                    string imagePath = Path.Combine(uploadsFolder, events.image);

                    // Check for duplicate file
                    if (System.IO.File.Exists(imagePath))
                    {
                        return Conflict(new { status = false, message = "File already exists." });
                    }

                    await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);

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


            if (!string.IsNullOrWhiteSpace(events.type))
            {
                existingEvent.type = events.type;
            }


            if (!string.IsNullOrWhiteSpace(events.user_type))
            {
                existingEvent.user_type = events.user_type;
            }


            if (!string.IsNullOrWhiteSpace(events.heading))
            {
                existingEvent.heading = events.heading;
            }
            if (!string.IsNullOrWhiteSpace(events.description))
            {
                existingEvent.description = events.description;
            }



            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }



        [HttpGet]
        [Route("view_notification")]
        public async Task<IActionResult> view_notification(int pageSize = 10, int pageNumber = 1)
        {
            if (_context.tbl_notification == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_notification' is null.");
            }

            if (pageSize <= 0) pageSize = 10;
            if (pageNumber <= 0) pageNumber = 1;

            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            var query = _context.tbl_notification
                .Where(n => n.delete_status == 0)
                .OrderByDescending(n => n.id)
                .Select(n => new
                {
                    n.id,
                    n.user_type,
                    n.type,
                    n.heading,
                    n.description,
                    n.notification_type,
                    n.image,
                    imageurl = !string.IsNullOrEmpty(n.image) ? $"{baseUrl}uploads/notification/{n.image}" : null
                });

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var paginatedNotifications = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                status = true,
                Message = "Success.",
                totalCount,
                totalPages,
                data = paginatedNotifications
            });
        }

        [HttpDelete]
        [Route("Delete_notification")]
        public async Task<IActionResult> Delete_notification([FromForm] int id, [FromForm] int? deleted_by)
        {
            if (_context.tbl_notification == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_notification' is null.");
            }
            var div = await _context.tbl_notification.FindAsync(id);
            if (div == null)
            {
                return NotFound(new { status = false, message = "data not found" });
            }
            div.delete_status = 1;
            div.deleted_on = DateTime.UtcNow;
            div.deleted_by = deleted_by;
            await _context.SaveChangesAsync();
            return Ok(new { status = true, message = "Data deleted successfully" });
        }

        [HttpPost]
        [Route("read_status")]
        public async Task<ActionResult> read_status([FromForm] string? notification_id, [FromForm] int? user_id, [FromForm] int? read_status)
        {
            if (string.IsNullOrEmpty(notification_id))
            {
                return BadRequest(new { status = false, message = "Notification IDs cannot be empty." });
            }

            var notificationIds = notification_id.Split(',');

            foreach (var id in notificationIds)
            {
                if (int.TryParse(id, out int notificationId))
                {
                    var readStatus = new notification_read_status
                    {
                        notification_id = notificationId,
                        user_id = user_id,
                        date = DateTime.Now,
                        read_status = read_status,
                    };

                    await _context.tbl_notification_read_status.AddAsync(readStatus);
                }
                else
                {
                    return BadRequest(new { status = false, message = $"Invalid notification_id: {id}" });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }



        [HttpGet]
        [Route("list_notification")]
        public async Task<IActionResult> list_notification(string? type,string? user_type)
        {
            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = _context.tbl_user
                              .Where(u => u.enc_key == enc_key)
                              .FirstOrDefault();

            int user_id = 0;
            if (user != null)
            {
                //user_id = (int)user.reference_id;
                user_id = user.reference_id ?? 0;

            }

            if (_context.tbl_notification == null || _context.tbl_notification_read_status == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_notification' or 'tbl_notification_read_status' is null.");
            }
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            List<dynamic> notifications = new List<dynamic>();

            if (type == "read")
            {
                notifications = await (from n in _context.tbl_notification_read_status
                                       join fn in _context.tbl_notification on n.notification_id equals fn.id into fnGroup
                                       from fn in fnGroup.DefaultIfEmpty() 
                                       where n.read_status == 1
                                             && (user_id == null || n.user_id == user_id)
                                       select new
                                       {
                                           id = fn.id,
                                           type = fn.type,
                                           n.read_status,
                                           user_type = fn.user_type,        
                                           heading = fn.heading,
                                           description = fn.description,
                                           date = fn.date, 
                                           user_id = n.user_id,
                                           image=fn.image,
                                           imageurl = !string.IsNullOrEmpty(fn.image) ? $"{baseUrl}uploads/notification/{fn.image}" : null
                                       }).Cast<dynamic>().ToListAsync();
            }
            else if (type == "unread")
            {
                notifications = await (from fn in _context.tbl_notification
                                       join n in _context.tbl_notification_read_status
                                       on fn.id equals n.notification_id into nGroup
                                       from n in nGroup.DefaultIfEmpty() 
                                       where fn.delete_status == 0
                                             && n.notification_id == null  
                                             && (("," + fn.user_type + ",").Contains("," + user_type + ","))
                                       select new
                                       {
                                           id = fn.id,
                                           type = fn.type,
                                           n.read_status,

                                           user_type = fn.user_type,

                                           heading = fn.heading,
                                           description = fn.description,
                                           date = fn.date,
                                           user_id = fn.user_id,
                                           image = fn.image,
                                           imageurl = !string.IsNullOrEmpty(fn.image) ? $"{baseUrl}uploads/notification/{fn.image}" : null
                                       }).Cast<dynamic>().ToListAsync();
            }
            else if (type == "all")
            {
                //notifications = await _context.tbl_notification
                //     .Where(n => n.delete_status == 0 &&
                //("," + n.user_type + ",").Contains("," + user_type + ","))
                //    .Select(n => new
                //    {
                //        id = n.id,
                //        type = n.type, 
                //        user_type=n.user_type, 

                //        heading = n.heading,
                //        description = n.description,
                //        date = n.date,
                //        user_id = n.user_id,
                //        image = n.image,
                //        imageurl = !string.IsNullOrEmpty(n.image) ? $"{baseUrl}uploads/notification/{n.image}" : null
                //    })
                //    .Cast<dynamic>() 
                //    .ToListAsync();
                notifications = await (
        from n in _context.tbl_notification
        join rs in _context.tbl_notification_read_status
            on n.id equals rs.notification_id into readGroup
        from rs in readGroup.DefaultIfEmpty()
        where n.delete_status == 0 &&
              ("," + n.user_type + ",").Contains("," + user_type + ",")
        orderby rs.read_status == null ? 0 : rs.read_status // Order by read_status (nulls treated as 0)
        select new
        {
            id = n.id,
            type = n.type,
            user_type = n.user_type,
            heading = n.heading,
            description = n.description,
            date = n.date,
            user_id = n.user_id,
            image = n.image,
            imageurl = !string.IsNullOrEmpty(n.image) ? $"{baseUrl}uploads/notification/{n.image}" : null,
            read_status = rs != null ? rs.read_status : 0
        }).Cast<dynamic>().ToListAsync();

            }
            else
            {
                return Ok(new
                {
                    status = false,
                    Message = "Invalid type provided. Allowed values are 'read', 'unread', or 'all'.",
                    data = (List<dynamic>)null
                });
            }

            return Ok(new
            {
                status = notifications.Count > 0,
                Message = notifications.Count > 0 ? "Success." : "No data found.",
                data = notifications
            });
        }




    }
}
