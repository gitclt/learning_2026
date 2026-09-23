 using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class instituteController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public instituteController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Postinstitute")]
        public async Task<ActionResult<institutemodel>> Postinstitute(institutemodel request)
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
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "institute");

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

            //  Check  username already exists
            var existingStudent = await _context.tbl_user
                .FirstOrDefaultAsync(s => s.username == request.username && s.password==request.password);

            if (existingStudent != null)
            {
                return Ok(new { status = false, message = "username and password already exists." });
            }

            var institute = new institutemodel
            {
                name = request.name,
                username = request.username,
                password = request.password,
                address = request.address,
                contact_person = request.contact_person,
                location = request.location,
                phone_no = request.phone_no,
                addedon = DateTime.Now,
                image = fileName,
                status = "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                code=request.code       
            };

            _context.tbl_institute.Add(institute);
            await _context.SaveChangesAsync();

            var user = new usermodel
            {
                type = "institute",
                name = institute.name,
                username = institute.username,
                phone_no = institute.phone_no,
                password = institute.password,
                addedon = DateTime.Now,
                status = "active",
                added_by = institute.added_by,
                addedtype = institute.addedtype,
                reference_id = institute.id,
            };

            _context.tbl_user.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Data added successfully"
            });
        }


        [HttpPut]
        [Route("edit_institute")]
        public async Task<ActionResult> edit_institute(institutemodel events)
        {
            if (_context.tbl_institute == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_institute' is null.");
            }

            var existingEvent = await _context.tbl_institute.FindAsync(events.id);
            if (existingEvent == null)
            {
                return Ok(new { status = false, message = "Institute not found" });
            }

            var isImageChanged = events.is_img_chged?.ToLower() == "yes";
            string? newImageName = existingEvent.image;

            if (isImageChanged)
            {
                if (string.IsNullOrEmpty(events.image_data))
                {
                    return BadRequest(new { status = false, message = "no image data provided." });
                }

                var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/institute", events.img_oldname);
                if (System.IO.File.Exists(oldImagePath))
                {
                    System.IO.File.Delete(oldImagePath);
                }

                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/institute");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                try
                {
                    byte[] imgBytes = Convert.FromBase64String(events.image_data);
                    string imagePath = Path.Combine(uploadsFolder, events.image);

                    if (System.IO.File.Exists(imagePath))
                    {
                        return Conflict(new { status = false, message = "File already exists." });
                    }

                    await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);
                    newImageName = events.image;
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

            if (!string.IsNullOrEmpty(events.name)) existingEvent.name = events.name;
            if (!string.IsNullOrEmpty(events.contact_person)) existingEvent.contact_person = events.contact_person;
            if (!string.IsNullOrEmpty(events.phone_no)) existingEvent.phone_no = events.phone_no;
            if (!string.IsNullOrEmpty(events.address)) existingEvent.address = events.address;
            if (!string.IsNullOrEmpty(events.location)) existingEvent.location = events.location;
            if (!string.IsNullOrEmpty(events.username)) existingEvent.username = events.username;
            if (!string.IsNullOrEmpty(events.password)) existingEvent.password = events.password;
            if (!string.IsNullOrEmpty(events.address)) existingEvent.address = events.address;
            if (!string.IsNullOrEmpty(events.code)) existingEvent.code = events.code;


            existingEvent.image = newImageName;

            if (events.modified_by.HasValue) existingEvent.modified_by = events.modified_by.Value;
            if (!string.IsNullOrEmpty(events.modified_type)) existingEvent.modified_type = events.modified_type;

            existingEvent.modifiedon = DateTime.Now;

            var user = await _context.tbl_user
                .Where(u => u.reference_id == existingEvent.id && u.type == "institute")
                .FirstOrDefaultAsync();

            if (user != null)
            {
                user.name = existingEvent.name;
                user.phone_no = existingEvent.phone_no;
                user.username = existingEvent.username;
                user.password = existingEvent.password;
              
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("view_institute")]
        public async Task<ActionResult> view_institute(int? id)
        {
            if (_context.tbl_institute == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var query = _context.tbl_institute.Where(d => d.status.ToLower() == "active"); ;

            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}/";



            var categories = await query
                .Select(g => new
                {
                    id = g.id,
                    name = g.name,
                    username = g.username,
                    password = g.password,
                    phone_no = g.phone_no,
                    contact_person = g.contact_person,
                    address = g.address,
                    location = g.location,
                    code=g.code,        
                    image = g.image,
                   // imageurl = !string.IsNullOrEmpty(g.image) ? $"{baseUrl}wwwroot/uploads/institute/{g.image}" : null

                    imageurl = !string.IsNullOrEmpty(g.image) ? $"{baseUrl}uploads/institute/{g.image}" : null


                })
                .ToListAsync();

           
            if (categories == null || !categories.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = categories });
        }


        [HttpDelete]
        [Route("Delete_institute")]
        public async Task<IActionResult> Delete_institute([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_institute == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_institute' is null.");
            }

            var eventItem = await _context.tbl_institute.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            if (!string.IsNullOrEmpty(eventItem.image))
            {
                string imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/institute", eventItem.image);
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

            eventItem.status = "deleted";
            eventItem.deleted_by = deleted_by;
            eventItem.deleted_type = deleted_type;
            eventItem.deletedon = DateTime.Now;

            _context.Entry(eventItem).State = EntityState.Modified;
            var user = _context.tbl_user.Where(c => c.reference_id == id && c.type.ToLower() == "institute");
            //_context.tbl_user.RemoveRange(user);

            foreach (var usr in user)
            {
                usr.status = "deleted";
                usr.deleted_by = deleted_by;
                usr.deletedon = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data and image deleted successfully" });
        }

        //institute_total_points announcement team
        //[HttpGet]
        //[Route("get_institute_points_with_position")]
        //public async Task<ActionResult> get_institute_points_with_position(int?ac_year_id)
        //{
        //    // Get published count
        //    int publishedCount = _context.tbl_program
        //        .Count(p =>( p.status.ToLower() == "result published" || p.status== "Transferred to Media" )&&p.ac_year_id==ac_year_id);

        //    // Get pending count
        //    int pendingCount = _context.tbl_program
        //        .Count(p => (p.status.ToLower() != "result published" || p.status!= "Transferred to Media")&&p.ac_year_id==ac_year_id);

        //    // Get total points per institute
        //    var pointsData = (from pp in _context.tbl_prgm_participants
        //                      join p in _context.tbl_program on pp.prgm_id equals p.id
        //                      where  (pp.position == "1" || pp.position == "2" || pp.position == "3")

        //                      join s in _context.tbl_student on pp.student_id equals s.id into studentJoin
        //                      from s in studentJoin.DefaultIfEmpty()
        //                      join i in _context.tbl_institute on s.institute equals i.id into instituteJoin
        //                      from i in instituteJoin.DefaultIfEmpty()
        //                      where p.ac_year_id==ac_year_id
        //                      group new { pp, s, i } by new
        //                      {
        //                          institute_id = s.institute,
        //                          institute_name = i.name
        //                      } into g
        //                      select new
        //                      {
        //                          institute = g.Key.institute_name,
        //                          //    total_marks = g.Sum(x => x.pp.average_point)

        //                          //}).OrderByDescending(x => x.total_marks).ToList();
        //                          total_marks = Convert.ToDecimal(Math.Round(g.Sum(x => x.pp.total_point) ?? 0, 2))
        //                      }).OrderByDescending(x => x.total_marks).ToList();



        //    // Add ranking
        //    var rankedData = pointsData.Select((x, index) => new
        //    {
        //        institute = x.institute,
        //        total_marks = x.total_marks,
        //        prize = index == 0 ? "First Position"
        //               : index == 1 ? "Second Position"
        //               : index == 2 ? "Third Position"
        //               : ""
        //    }).ToList();

        //    // Final response
        //    return Ok(new
        //    {
        //        status = true,
        //        message = "Success",
        //        result_published = publishedCount,
        //        pending = pendingCount,
        //        data = rankedData
        //    });
        //}

        [HttpGet]
        [Route("get_institute_points_with_position")]
        public async Task<ActionResult> get_institute_points_with_position(int? ac_year_id)
        {
            // Get published count
            int publishedCount = _context.tbl_program
                .Count(p => (p.status.ToLower() == "result published" || p.status == "Transferred to Media")
                            && p.ac_year_id == ac_year_id&&p.delete_status== "active");

            // Get pending count
            int pendingCount = _context.tbl_program
                .Count(p => p.status.ToLower() != "result published"
                            && p.status.ToLower() != "transferred to media"
                            && p.ac_year_id == ac_year_id && p.delete_status == "active");

            // Get total points per institute — inner joins only
            var pointsData = (from pp in _context.tbl_prgm_participants
                              join p in _context.tbl_program on pp.prgm_id equals p.id
                              join s in _context.tbl_student on pp.student_id equals s.id
                              join i in _context.tbl_institute on s.institute equals i.id
                              where (pp.position == "1" || pp.position == "2" || pp.position == "3")
                                    && p.ac_year_id == ac_year_id
                              group pp by new
                              {
                                  institute_id = s.institute,
                                  institute_name = i.name
                              } into g
                              select new
                              {
                                  institute = g.Key.institute_name,
                                  total_marks = Convert.ToDecimal(Math.Round(g.Sum(x => x.total_point) ?? 0, 2))
                              })
                              .OrderByDescending(x => x.total_marks)
                              .ToList();

            // Add ranking
            var rankedData = pointsData.Select((x, index) => new
            {
                institute = x.institute,
                total_marks = x.total_marks,
                prize = index == 0 ? "First Position"
                       : index == 1 ? "Second Position"
                       : index == 2 ? "Third Position"
                       : ""
            }).ToList();

            // Final response
            return Ok(new
            {
                status = true,
                message = "Success",
                result_published = publishedCount,
                pending = pendingCount,
                data = rankedData
            });
        }
        //

        //get institute points excel
        [HttpGet]
        [Route("institute_points_excel")]

        public async Task<IActionResult> institute_points_excel(int?ac_year_id)
        {

            // Step 1: Build query without student filter inside join
            var query =
                from p in _context.tbl_prgm_participants
                join s in _context.tbl_student on p.student_id equals s.id into studentJoin
                from s in studentJoin.DefaultIfEmpty()
                join i in _context.tbl_institute on s.institute equals i.id into instituteJoin
                from i in instituteJoin.DefaultIfEmpty()
                join r in _context.tbl_program on p.prgm_id equals r.id into programJoin
                from r in programJoin.DefaultIfEmpty()
                join ac in _context.tbl_academic_year on p.ac_year_id equals ac.id into academicYearJoin
                from ac in academicYearJoin.DefaultIfEmpty()
                where p.position == "1" || p.position == "2" || p.position == "3"
                select new { p, s, i, r, ac };

            if (ac_year_id.HasValue)
            {
                query = query.Where(x => x.p.ac_year_id == ac_year_id);
            }

            var result = query
                .GroupBy(x => new
                {
                    x.s.institute,
                    x.i.name,
                    x.i.color_code,
                    x.i.address,
                    x.i.location,
                    x.i.phone_no,
                    x.i.contact_person,
                    x.ac.year
                })
                .OrderByDescending(g => g.Sum(x => x.p.total_point))
                .Select(g => new
                {
                    institute_id = g.Key.institute,
                    institute_name = g.Key.name,
                    address = g.Key.address,
                    location = g.Key.location,
                    phone_no = g.Key.phone_no,
                    contact_person = g.Key.contact_person,
                    academic_year = g.Key.year,
                    total_points = Math.Round(g.Sum(x => (double?)x.p.total_point ?? 0), 2)
                })
                .ToList();

            // Step 3: Create Excel workbook
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Institute Points");

                worksheet.Cell(1, 1).Value = "Institute";
                worksheet.Cell(1, 2).Value = "Address";
                worksheet.Cell(1, 3).Value = "Location";
                worksheet.Cell(1, 4).Value = "Phone Number";
                worksheet.Cell(1, 5).Value = "Contact Person";

                worksheet.Cell(1, 6).Value = "Total Points";
                worksheet.Cell(1, 7).Value = "Academic Year";
               

                int row = 2;
                foreach (var item in result)
                {
                    worksheet.Cell(row, 1).Value = item.institute_name;
                    worksheet.Cell(row, 2).Value = item.address;
                    worksheet.Cell(row, 3).Value = item.location;
                    worksheet.Cell(row, 4).Value = item.phone_no;
                    worksheet.Cell(row, 5).Value = item.contact_person;

                    worksheet.Cell(row, 6).Value = item.total_points;
                    worksheet.Cell(row, 7).Value = item.academic_year;
                   
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"Institute_points.xlsx");
                }
            }
        }



        //
    }
}
