using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Xml.XPath;
using Azure.Core;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Excel;
using Google.Apis.Http;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class studentController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public studentController(kalanjaliDbContext context)
        {
            _context = context;
        }



        [HttpPost]
        [Route("Poststudent")]
        public async Task<ActionResult> Poststudent([FromBody] studentmodel request)
        {
            if (_context.tbl_student == null)
                return Ok(new { status = false, message = "Entity set 'tbl_student' is null." });

            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Ok(new { status = false, message = string.Join(", ", errorMessages) });
            }

            // Gender mandatory check
            if (string.IsNullOrEmpty(request.gender))
            {
                return Ok(new { status = false, message = "Gender is required." });
            }

            //if (string.IsNullOrEmpty(request.image))
            //{
            //    return Ok(new { status = false, message = "Image is required." });
            //}

            // --- Admission number check ---
            var existingStudent = await _context.tbl_student
                .FirstOrDefaultAsync(s =>
                    s.admsn_no == request.admsn_no &&
                    s.ac_year_id == request.ac_year_id &&
                    s.status == "active");

            if (existingStudent != null)
                return Ok(new { status = false, message = "QID already exists." });

            // --- PROGRAM VALIDATIONS ---
            if (request.students_prgm != null && request.students_prgm.Count > 0)
            {
                foreach (var group in request.students_prgm.Where(x => x.prgm_id.HasValue)
                                                           .GroupBy(x => x.prgm_id.Value))
                {
                    int prgmId = group.Key;

                    var program = await _context.tbl_program
                        .Where(p => p.id == prgmId && p.delete_status != "deleted")
                        .Select(p => new
                        {
                            p.program_type,
                            p.gender,
                            p.group_min_participants,
                            p.group_max_participants,
                            p.status,
                            p.participant_type,
                        })
                        .FirstOrDefaultAsync();

                    if (program == null)
                        return Ok(new { status = false, message = "Program not found" });

                    if (program.status.ToLower() != "pending")
                        return Ok(new { status = false, message = $"Program status is '{program.status}', cannot add participants." });

                    if (program.program_type.ToLower() == "group")
                    {
                        if (request.students_prgm == null ||
                            request.students_prgm.Any(x => string.IsNullOrWhiteSpace(x.group_name)))
                        {
                            return Ok(new
                            {
                                status = false,
                                message = "Group name is required for group programs."
                            });
                        }
                    }

                    // Gender validation
                    string programGender = program.gender?.Trim().ToLower().Replace("s", "");
                    string studentGender = request.gender?.Trim().ToLower();
                    if (!string.IsNullOrEmpty(programGender) &&
                        programGender != "common" &&
                        !string.IsNullOrEmpty(studentGender) &&
                        programGender != studentGender)
                    {
                        return Ok(new { status = false, message = $"This program is only for {program.gender} students." });
                    }

                    // Category validation
                    string programCategory = program.participant_type?.Trim().ToLower();
                    string studentCategory = request.category?.Trim().ToLower();
                    if (!string.IsNullOrEmpty(programCategory) &&
                        programCategory != "common" &&
                        !string.IsNullOrEmpty(studentCategory) &&
                        programCategory != studentCategory)
                    {
                        return Ok(new { status = false, message = $"This program is only for {program.participant_type} category students." });
                    }

                    // Participant limit
                    int existingCount = await (from p in _context.tbl_prgm_participants
                                               join s in _context.tbl_student on p.student_id equals s.id
                                               where p.prgm_id == prgmId &&
                                                     s.institute == request.institute &&
                                                     p.status != "deleted"
                                               select p).CountAsync();

                    int newParticipantCount = group.Count();
                    int totalCount = existingCount + newParticipantCount;

                    //if (program.program_type.ToLower() == "group")
                    //{
                    //    if (totalCount > program.group_max_participants)
                    //    {
                    //        return Ok(new { status = false, message = $"Maximum {program.group_max_participants} participants allowed from this institute." });
                    //    }
                    //}
                    if (program.program_type.ToLower() == "solo")
                    {
                        if (totalCount > program.group_max_participants)
                        {
                            return Ok(new { status = false, message = $"Maximum {program.group_max_participants} participants allowed from this institute." });
                        }
                    }
                }
            }

            // --- SAVE STUDENT DETAILS ---
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/student");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var filename = request.image;
            if (!string.IsNullOrEmpty(request.image_data) && !string.IsNullOrEmpty(filename))
            {
                var base64Image = request.image_data;
                if (base64Image.Contains(","))
                    base64Image = base64Image.Substring(base64Image.IndexOf(",") + 1);

                string imagePath = Path.Combine(uploadsFolder, filename);
                if (System.IO.File.Exists(imagePath))
                    return Ok(new { status = false, message = "File already exists." });

                byte[] imgBytes = Convert.FromBase64String(base64Image);
                await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);
            }

            var student = new studentmodel
            {
                name = request.name,
                phone_no = request.phone_no,
                class_id = request.class_id,
                division_id = request.division_id,
                admsn_no = request.admsn_no,
                institute = request.institute,
                status = request.status ?? "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                addedon = DateTime.Now,
                gender = request.gender,
                email = request.email,
                image = filename,
                category = request.category,
                ac_year_id = request.ac_year_id
            };

            _context.tbl_student.Add(student);
            await _context.SaveChangesAsync();

            var user = new usermodel
            {
                type = "student",
                name = student.name,
                admsn_no = student.admsn_no,
                phone_no = student.phone_no,
                username = student.admsn_no,
                password = student.admsn_no,
                addedon = DateTime.Now,
                status = "active",
                added_by = student.added_by,
                addedtype = student.addedtype,
                reference_id = student.id,
            };

            _context.tbl_user.Add(user);
            await _context.SaveChangesAsync();

            // Groups that need a minimum-members check after everything is saved
            var groupsToCheck = new List<(int PrgmId, string GroupName, int MinParticipants)>();

            // --- PROGRAM HANDLING ---
            if (request.students_prgm != null && request.students_prgm.Count > 0)
            {
                var groupMembersToAdd = new List<group_members>();
                var prgmParticipantsToAdd = new List<prgm_participants>();

                foreach (var group in request.students_prgm.Where(x => x.prgm_id.HasValue)
                                                           .GroupBy(x => x.prgm_id.Value))
                {
                    int prgmId = group.Key;
                    var group_name = group.First().group_name;

                    var program = await _context.tbl_program
                        .Where(p => p.id == prgmId && p.delete_status != "deleted")
                        .Select(p => new
                        {
                            p.program_type,
                            p.group_min_participants,
                            p.group_max_participants,
                            p.no_of_group,
                        })
                        .FirstOrDefaultAsync();

                    if (program == null)
                        continue;

                    // Get existing count for the institute
                    int existingCount = await (from p in _context.tbl_prgm_participants
                                               join s in _context.tbl_student on p.student_id equals s.id
                                               where p.prgm_id == prgmId &&
                                                     s.institute == request.institute &&
                                                     p.status != "deleted"
                                               select p).CountAsync();

                    if (program.program_type.ToLower() == "group")
                    {
                        int noOfGroup = Convert.ToInt32(program.no_of_group);
                        int maxParticipants = Convert.ToInt32(program.group_max_participants);

                        if (noOfGroup > 1)
                        {
                            var currentCount = await (from gm in _context.tbl_group_members
                                                      join s in _context.tbl_student on gm.student_id equals s.id
                                                      where gm.prgm_id == prgmId
                                                            && gm.group_name == group_name
                                                            && s.institute == request.institute
                                                      select gm.id).CountAsync();

                            if (currentCount >= maxParticipants)
                            {
                                return Ok(new
                                {
                                    status = false,
                                    message = $"Cannot add members '{group_name}' already has {currentCount} participants from this institute."
                                });
                            }
                        }

                        // Step 1: Add a single record in tbl_prgm_participants per (group_name + institute)
                        bool groupInstituteEntryExists = await (from p in _context.tbl_prgm_participants
                                                                join s in _context.tbl_student on p.student_id equals s.id
                                                                where p.prgm_id == prgmId
                                                                      && p.group_name == group_name
                                                                      && s.institute == request.institute
                                                                      && p.status != "deleted"
                                                                select p).AnyAsync();

                        if (!groupInstituteEntryExists)
                        {
                            prgmParticipantsToAdd.Add(new prgm_participants
                            {
                                student_id = student.id,   // first student from this institute = group representative
                                prgm_id = prgmId,
                                addedon = DateTime.Now,
                                status = "active",
                                program_status = "pending",
                                group_name = group_name,
                                ac_year_id = request.ac_year_id
                            });
                        }

                        // Step 2: Add student to group_members if not already present
                        bool existsInGroup = await _context.tbl_group_members
                            .AnyAsync(g => g.prgm_id == prgmId && g.student_id == student.id);

                        if (existsInGroup)
                        {
                            return Ok(new
                            {
                                status = false,
                                message = $"Student {student.name} is already a group member for this program."
                            });
                        }

                        groupMembersToAdd.Add(new group_members
                        {
                            student_id = student.id,
                            prgm_id = prgmId,
                            verify_status = "pending",
                            group_name = group_name,
                            instit_id = request.institute
                        });

                        // Remember this group so we can check the minimum after saving
                        groupsToCheck.Add((
                            prgmId,
                            group_name,
                            Convert.ToInt32(program.group_min_participants)
                        ));
                    }
                    else if (program.program_type.ToLower() == "solo")
                    {
                        // Prevent duplicate solo registration
                        bool alreadyExists = await _context.tbl_prgm_participants
                            .AnyAsync(p => p.prgm_id == prgmId && p.student_id == student.id && p.status != "deleted");

                        if (alreadyExists)
                        {
                            return Ok(new
                            {
                                status = false,
                                message = $"Student {student.name} is already registered for this solo program."
                            });
                        }

                        prgmParticipantsToAdd.Add(new prgm_participants
                        {
                            student_id = student.id,
                            prgm_id = prgmId,
                            addedon = DateTime.Now,
                            status = "active",
                            program_status = "pending",
                            ac_year_id = request.ac_year_id
                        });
                    }
                }

                // Save all
                if (prgmParticipantsToAdd.Any())
                    _context.tbl_prgm_participants.AddRange(prgmParticipantsToAdd);

                if (groupMembersToAdd.Any())
                    _context.tbl_group_members.AddRange(groupMembersToAdd);

                await _context.SaveChangesAsync();
            }

            // --- MINIMUM GROUP MEMBER CHECK ---
            // Runs after the save, so the count already includes this student (no "+ 1" needed).
            var pendingMessages = new List<string>();

            foreach (var g in groupsToCheck)
            {
                int currentGroupMemberCount = await (
                    from gm in _context.tbl_group_members
                    join s2 in _context.tbl_student on gm.student_id equals s2.id
                    where gm.prgm_id == g.PrgmId
                          && gm.group_name == g.GroupName
                          && s2.institute == request.institute
                    select gm.id
                ).CountAsync();

                int remaining = g.MinParticipants - currentGroupMemberCount;

                if (remaining > 0)
                {
                    pendingMessages.Add(
                        $"You need to add {remaining} more student{(remaining > 1 ? "s" : "")} to group '{g.GroupName}'");
                }
            }

            if (pendingMessages.Any())
            {
                // Student is saved successfully; this is only a heads-up
                return Ok(new
                {
                    status = true,
                    message = string.Join(". ", pendingMessages),
                    id = student.id
                });
            }

            return Ok(new { status = true, message = "Data added successfully", id = student.id });
        }




        [HttpPut]
        [Route("update_student")]

        public async Task<ActionResult> update_student([FromBody] studentmodel request)
        {
            var qid = "";
            var data = await _context.tbl_student.FindAsync(request.id);
            if (data == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Check if student is assigned to any active program
            var assignedPrograms = await _context.tbl_prgm_participants
                .Where(p => p.student_id == request.id && p.status == "active")
                .ToListAsync();

            if (assignedPrograms.Any())
            {
                // Allow other details to be edited,
                // but category/gender cannot be changed
                bool categoryChanged =
                    !string.IsNullOrEmpty(request.category) &&
                    !string.Equals(
                        data.category?.Trim(),
                        request.category.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                bool genderChanged =
                    !string.IsNullOrEmpty(request.gender) &&
                    !string.Equals(
                        data.gender?.Trim(),
                        request.gender.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                if (categoryChanged || genderChanged)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "Please delete the assigned program before changing category or gender."
                    });
                }
            }

            // --- Admission number check ---
            var existingStudent = await _context.tbl_student
                .FirstOrDefaultAsync(s => s.admsn_no == request.admsn_no && s.status == "active");

            string existingqid = data.admsn_no;
            if (existingqid != request.admsn_no)
            {
                var duplicateStudent = await _context.tbl_student
                    .FirstOrDefaultAsync(s => s.admsn_no == request.admsn_no && s.status == "active" && s.id != request.id);

                if (duplicateStudent != null)
                {
                    return Ok(new { status = false, message = "QID already exists." });
                }
            }

            var isImageChanged = request.is_img_chged?.ToLower() == "yes";

            //if (isImageChanged)
            //{
            //    if (string.IsNullOrEmpty(request.image_data))
            //    {
            //        return BadRequest(new { status = false, message = "Image update requested but no image data provided." });
            //    }

            //    // Delete old image
            //    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/student", request.img_oldname);
            //    if (System.IO.File.Exists(oldImagePath))
            //    {
            //        System.IO.File.Delete(oldImagePath);
            //    }

            //    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads/student");
            //    if (!Directory.Exists(uploadsFolder))
            //    {
            //        Directory.CreateDirectory(uploadsFolder);
            //    }

            //    try
            //    {
            //        byte[] imgBytes = Convert.FromBase64String(request.image_data);
            //        string imagePath = Path.Combine(uploadsFolder, request.image);

            //        // Check for duplicate file
            //        if (System.IO.File.Exists(imagePath))
            //        {
            //            return Conflict(new { status = false, message = "File already exists." });
            //        }

            //        await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);

            //        // ✅ Save image filename to database
            //        data.image = request.image;
            //    }
            //    catch (FormatException ex)
            //    {
            //        return BadRequest(new { status = false, message = "Invalid base64 string.", error = ex.Message });
            //    }
            //    catch (Exception ex)
            //    {
            //        return StatusCode(500, new { status = false, message = "An error occurred while uploading the image.", error = ex.Message });
            //    }
            //}

            // Update image only when a new image is actually provided
            if (isImageChanged && !string.IsNullOrWhiteSpace(request.image_data))
            {
                string uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/uploads/student"
                );

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                try
                {
                    // Remove data:image/...;base64, prefix if frontend sends it
                    string base64Image = request.image_data;

                    if (base64Image.Contains(","))
                    {
                        base64Image = base64Image.Split(',')[1];
                    }

                    byte[] imgBytes = Convert.FromBase64String(base64Image);

                    // Generate filename if not provided
                    string fileName = !string.IsNullOrWhiteSpace(request.image)
                        ? request.image
                        : Guid.NewGuid().ToString() + ".jpg";

                    string imagePath = Path.Combine(uploadsFolder, fileName);

                    // Save new image first
                    await System.IO.File.WriteAllBytesAsync(imagePath, imgBytes);

                    // Delete old image only after successfully saving the new image
                    if (!string.IsNullOrWhiteSpace(request.img_oldname))
                    {
                        string oldImagePath = Path.Combine(
                            uploadsFolder,
                            request.img_oldname
                        );

                        if (System.IO.File.Exists(oldImagePath) &&
                            !string.Equals(request.img_oldname, fileName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }

                    // Save new filename in database
                    data.image = fileName;
                }
                catch (FormatException ex)
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Invalid base64 image data.",
                        error = ex.Message
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        status = false,
                        message = "An error occurred while uploading the image.",
                        error = ex.Message
                    });
                }
            }

            // Update student info
            if (!string.IsNullOrEmpty(request.name)) data.name = request.name;
            if (!string.IsNullOrEmpty(request.phone_no)) data.phone_no = request.phone_no;
            if (!string.IsNullOrEmpty(request.password)) data.password = request.password;
            if (!string.IsNullOrEmpty(request.admsn_no)) data.admsn_no = request.admsn_no;
            if (!string.IsNullOrEmpty(request.gender)) data.gender = request.gender;
            if (!string.IsNullOrEmpty(request.category)) data.category = request.category;

            if (!string.IsNullOrEmpty(request.email)) data.email = request.email;

            if (request.institute != null) data.institute = request.institute;
            if (request.class_id != null) data.class_id = request.class_id;
            if (request.division_id != null) data.division_id = request.division_id;

            // Update student programs
            if (request.students_prgm != null && request.students_prgm.Count > 0)
            {
                var existingEntries = _context.tbl_prgm_participants.Where(p => p.student_id == data.id);
                _context.tbl_prgm_participants.RemoveRange(existingEntries);

                var subadminAssemblies = new List<prgm_participants>();
                var groupMembers = new List<group_members>();

                var groupedByPrgm = request.students_prgm
                    .Where(x => x.prgm_id != null)
                    .GroupBy(x => x.prgm_id.Value);

                foreach (var group in groupedByPrgm)
                {
                    int prgmId = group.Key;

                    // Get student info
                    var studentData = await _context.tbl_student
                        .Where(s => s.id == data.id)
                        .Select(s => new { s.institute, s.gender })
                        .FirstOrDefaultAsync();

                    if (studentData == null)
                        return Ok(new { status = false, message = "Student not found" });

                    var program = await _context.tbl_program
                        .Where(p => p.id == prgmId && p.delete_status != "deleted")
                        .Select(p => new
                        {
                            p.program_type,
                            p.gender,
                            p.group_min_participants,
                            p.group_max_participants
                        })
                        .FirstOrDefaultAsync();

                    if (program == null)
                        return Ok(new { status = false, message = $"Program not found for ID {prgmId}" });

                    //  Gender validation
                    string programGender = program.gender?.Trim().ToLower().Replace("s", "");
                    string studentGender = studentData.gender?.Trim().ToLower();

                    if (!string.IsNullOrEmpty(programGender) &&
                        programGender != "common" &&
                        !string.IsNullOrEmpty(studentGender) &&
                        programGender != studentGender)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"This program is only for {program.gender} students."
                        });
                    }


                    // Duplicate check
                    bool alreadyExists = await _context.tbl_prgm_participants.AnyAsync(x =>
                        x.student_id == data.id && x.prgm_id == prgmId && x.status != "deleted");

                    if (alreadyExists)
                    {
                        return Ok(new { status = false, message = $"Student {data.id} is already registered for this program." });
                    }

                    bool alreadyInGroup = await _context.tbl_group_members.AnyAsync(g =>
                        g.student_id == data.id && g.prgm_id == prgmId);

                    if (alreadyInGroup)
                    {
                        return Ok(new { status = false, message = $"Student {data.id} is already a group member for this program." });
                    }

                    //  Participant count validation
                    int existingCount = await (from p in _context.tbl_prgm_participants
                                               join s in _context.tbl_student on p.student_id equals s.id
                                               where p.prgm_id == prgmId &&
                                                     s.institute == studentData.institute &&
                                                     p.status != "deleted"
                                               select p).CountAsync();

                    int newParticipantCount = group.Count();
                    int totalCount = existingCount + newParticipantCount;

                    string programType = program.program_type.ToLower();

                    if (programType == "group" || programType == "solo")
                    {
                        //if (existingCount == 0 && newParticipantCount < program.group_min_participants)
                        //{
                        //    return Ok(new
                        //    {
                        //        status = false,
                        //        message = $"Minimum {program.group_min_participants} participants are required for first-time registration from this institute."
                        //    });
                        //}

                        if (totalCount > program.group_max_participants)
                        {
                            return Ok(new
                            {
                                status = false,
                                message = $"Maximum {program.group_max_participants} participants allowed for this program from this institute."
                            });
                        }
                    }

                    if (programType == "group")
                    {
                        // Add leader
                        subadminAssemblies.Add(new prgm_participants
                        {
                            student_id = data.id,
                            prgm_id = prgmId,
                            addedon = DateTime.Now,
                            status = "active",
                            program_status = "pending"
                        });

                        // Add group members
                        foreach (var member in group)
                        {
                            groupMembers.Add(new group_members
                            {
                                student_id = member.student_id,
                                prgm_id = prgmId
                            });
                        }
                    }
                    else
                    {
                        // Solo program
                        foreach (var prgm in group)
                        {
                            subadminAssemblies.Add(new prgm_participants
                            {
                                student_id = data.id,
                                prgm_id = prgmId,
                                addedon = DateTime.Now,
                                status = prgm.status ?? "active",
                                program_status = "pending"
                            });
                        }
                    }
                }

                await _context.tbl_prgm_participants.AddRangeAsync(subadminAssemblies);

                if (groupMembers.Any())
                {
                    await _context.tbl_group_members.AddRangeAsync(groupMembers);
                }
            }

            // Update user info
            var user = _context.tbl_user.FirstOrDefault(u => u.reference_id == request.id && u.type == "student");

            if (user != null)
            {
                if (!string.IsNullOrEmpty(request.name)) user.name = request.name;
                if (!string.IsNullOrEmpty(request.phone_no)) user.phone_no = request.phone_no;
                if (!string.IsNullOrEmpty(request.admsn_no)) user.admsn_no = request.admsn_no;
                if (!string.IsNullOrEmpty(request.admsn_no)) user.username = request.admsn_no;
                if (!string.IsNullOrEmpty(request.admsn_no)) user.password = request.admsn_no;
                user.modifiedon = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }


        //[HttpGet]
        //[Route("get_student")]

        //public async Task<ActionResult> get_student(int? institute_id, string? keyword, DateTime? verify_date, int? id, string? admsn_no, int? program_id, string? item_code, int? stage_id,int?ac_year_id)
        //{
        //    if (_context.tbl_student == null)
        //    {
        //        return Ok(new { status = false, message = "Data not found" });
        //    }
        //    var baseUrl = $"{Request.Scheme}://{Request.Host}/";

        //    //  Base student query
        //    var studentQuery = from p in _context.tbl_student.AsNoTracking()
        //                       join i in _context.tbl_institute on p.institute equals i.id into ins
        //                       from it in ins.DefaultIfEmpty()
        //                       join c in _context.tbl_class on p.class_id equals c.id into cla
        //                       from cl in cla.DefaultIfEmpty()
        //                       join d in _context.tbl_division on p.division_id equals d.id into div
        //                       from di in div.DefaultIfEmpty()
        //                       where p.status == "active"
        //                       select new
        //                       {
        //                           p.id,
        //                           p.name,
        //                           p.phone_no,
        //                           p.password,
        //                           p.institute,
        //                           institute_name = it.name,
        //                           code = it.code,
        //                           p.class_id,
        //                           @class = cl.@class,
        //                           p.division_id,
        //                           division = di.division,
        //                           p.admsn_no,
        //                           p.added_by,
        //                           p.addedtype,
        //                           p.addedon,
        //                           p.gender,
        //                           p.category,
        //                           p.email,
        //                           image = p.image,
        //                           imageurl = !string.IsNullOrEmpty(p.image) ? $"{baseUrl}uploads/student/{p.image}" : null,
        //                           p.ac_year_id
        //                       };

        //    // ✅ Apply filters
        //    if (id.HasValue)
        //    {
        //        studentQuery = studentQuery.Where(g => g.id == id.Value);
        //    }

        //    if (institute_id.HasValue)
        //    {
        //        studentQuery = studentQuery.Where(g => g.institute == institute_id.Value);
        //    }

        //    if (!string.IsNullOrWhiteSpace(keyword))
        //    {
        //        studentQuery = studentQuery.Where(e =>
        //            e.admsn_no.Contains(keyword) ||
        //            e.name.Contains(keyword));
        //    }

        //    if (!string.IsNullOrWhiteSpace(admsn_no))
        //        studentQuery = studentQuery.Where(e => e.admsn_no == admsn_no);

        //    if (ac_year_id.HasValue)
        //    {
        //        studentQuery = studentQuery.Where(g => g.ac_year_id == ac_year_id);
        //    }
        //    var students = await studentQuery.ToListAsync();

        //    if (students == null || !students.Any())
        //    {
        //        return Ok(new { status = false, message = "Data not found" });
        //    }

        //    var studentIds = students.Select(s => s.id).ToList();

        //    //  Individual programs (program_type != "group")
        //    var individualPrograms = await (from c in _context.tbl_prgm_participants.AsNoTracking()
        //                                    join pr in _context.tbl_program on c.prgm_id equals pr.id into prog
        //                                    from prg in prog.DefaultIfEmpty()
        //                                    join s in _context.tbl_stage on prg.stage_id equals s.id into sta
        //                                    from stg in sta.DefaultIfEmpty()
        //                                    where c.status != "deleted" && c.student_id != null && studentIds.Contains(c.student_id.Value)
        //                                    && prg.program_type != "group"
        //                                    select new
        //                                    {
        //                                        student_id = c.student_id,
        //                                        id = c.id,
        //                                        prgm_id = prg.id,
        //                                        item_code = prg.item_code,
        //                                        participants_type = prg.participant_type,
        //                                        gender = prg.gender,
        //                                        program_type = prg.program_type,
        //                                        program_name = prg.program_name,
        //                                        program_date = prg.date,
        //                                        program_time = prg.time,
        //                                        stage_id = prg.stage_id,
        //                                        program_stage = stg.stage_name,
        //                                        status = c.program_status
        //                                    }).ToListAsync();
        //    // Apply filters after ToListAsync()
        //    if (program_id.HasValue)
        //        individualPrograms = individualPrograms
        //            .Where(x => x.prgm_id == program_id.Value)
        //            .ToList();

        //    if (!string.IsNullOrEmpty(item_code))
        //        individualPrograms = individualPrograms
        //            .Where(x => x.item_code == item_code)
        //            .ToList();

        //    //filter by stage 
        //    if (stage_id.HasValue)
        //        individualPrograms = individualPrograms
        //            .Where(x => x.stage_id == stage_id.Value)
        //            .ToList();
        //    //

        //    //  Group programs (program_type = "group")
        //    var groupPrograms = await (from gm in _context.tbl_group_members.AsNoTracking()
        //                               join g in _context.tbl_prgm_participants on gm.prgm_id equals g.prgm_id
        //                               join prg in _context.tbl_program on g.prgm_id equals prg.id
        //                               join s in _context.tbl_stage on prg.stage_id equals s.id into sta
        //                               from stg in sta.DefaultIfEmpty()
        //                               where gm.student_id != null && studentIds.Contains(gm.student_id.Value)
        //                               && prg.program_type == "group"
        //                               select new
        //                               {
        //                                   student_id = gm.student_id,
        //                                   id = gm.id,
        //                                   prgm_id = prg.id,
        //                                   item_code = prg.item_code,
        //                                   participants_type = prg.participant_type,
        //                                   gender = prg.gender,
        //                                   program_type = prg.program_type,

        //                                   program_name = prg.program_name,
        //                                   program_date = prg.date,
        //                                   program_time = prg.time,
        //                                   stage_id = prg.stage_id,
        //                                   program_stage = stg.stage_name,
        //                                   status = gm.verify_status
        //                               }).ToListAsync();
        //    if (program_id.HasValue)
        //        groupPrograms = groupPrograms.Where(x => x.prgm_id == program_id.Value).ToList();

        //    if (!string.IsNullOrEmpty(item_code))
        //        groupPrograms = groupPrograms.Where(x => x.item_code == item_code).ToList();

        //    if (stage_id.HasValue)
        //        groupPrograms = groupPrograms.Where(x => x.stage_id == stage_id.Value).ToList();

        //    // Combine and remove duplicates
        //    var studentPrograms = individualPrograms
        //        .Concat(groupPrograms)
        //        .GroupBy(x => new { x.student_id, x.prgm_id }) // Unique by student_id + prgm_id
        //        .Select(g => g.First())
        //        .OrderBy(x => x.program_date) // ✅ Sort by program_date
        //        .ThenBy(x => x.program_time) // ✅ Sort by program_time
        //        .ToList();

        //    // ✅ Final result
        //    var result = students.Select(s => new
        //    {
        //        s.id,
        //        s.name,
        //        s.phone_no,
        //        s.password,
        //        s.institute,
        //        s.institute_name,
        //        s.code,
        //        s.class_id,
        //        s.@class,
        //        s.division_id,
        //        s.division,
        //        s.admsn_no,
        //        s.added_by,
        //        s.addedtype,
        //        s.addedon,
        //        s.gender,
        //        s.category,
        //        s.image,
        //        s.imageurl,
        //        studentprograms = studentPrograms
        //            .Where(p => p.student_id == s.id)
        //            .OrderBy(p => p.program_date) // Ensure individual student's programs are sorted
        //            .ThenBy(p => p.program_time)
        //            .ToList()
        //    }).ToList();

        //    return Ok(new { status = true, message = "Success", data = result });
        //}

        [HttpGet]
        [Route("get_student")]
        public async Task<ActionResult> get_student(
    int? institute_id,
    string? keyword,
    DateTime? verify_date,
    int? id,
    string? admsn_no,
    int? program_id,
    string? item_code,
    int? stage_id,
    int? ac_year_id,
    int? page = 1,
    int? size = 10)
        {
            if (_context.tbl_student == null)
            {
                return Ok(new
                {
                    status = false,
                    message = "Data not found"
                });
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            // Pagination defaults
            int currentPage = page.GetValueOrDefault(1);
            int pageSize = size.GetValueOrDefault(10);

            if (currentPage <= 0)
                currentPage = 1;

            if (pageSize <= 0)
                pageSize = 10;

            // Base student query
            var studentQuery =
                from p in _context.tbl_student.AsNoTracking()
                join i in _context.tbl_institute on p.institute equals i.id into ins
                from it in ins.DefaultIfEmpty()

                join c in _context.tbl_class on p.class_id equals c.id into cla
                from cl in cla.DefaultIfEmpty()

                join d in _context.tbl_division on p.division_id equals d.id into div
                from di in div.DefaultIfEmpty()

                where p.status == "active"

                select new
                {
                    p.id,
                    p.name,
                    p.phone_no,
                    p.password,
                    p.institute,
                    institute_name = it.name,
                    code = it.code,
                    p.class_id,
                    @class = cl.@class,
                    p.division_id,
                    division = di.division,
                    p.admsn_no,
                    p.added_by,
                    p.addedtype,
                    p.addedon,
                    p.gender,
                    p.category,
                    p.email,
                    image = p.image,
                    imageurl = !string.IsNullOrEmpty(p.image)
                        ? $"{baseUrl}uploads/student/{p.image}"
                        : null,
                    p.ac_year_id
                };

            // Apply filters

            if (id.HasValue)
            {
                studentQuery = studentQuery.Where(g => g.id == id.Value);
            }

            if (institute_id.HasValue)
            {
                studentQuery = studentQuery.Where(g => g.institute == institute_id.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                studentQuery = studentQuery.Where(e =>
                    e.admsn_no.Contains(keyword) ||
                    e.name.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(admsn_no))
            {
                studentQuery = studentQuery.Where(e => e.admsn_no == admsn_no);
            }

            if (ac_year_id.HasValue)
            {
                studentQuery = studentQuery.Where(g => g.ac_year_id == ac_year_id.Value);
            }

            // Total count BEFORE pagination
            var totalCount = await studentQuery.CountAsync();

            if (totalCount == 0)
            {
                return Ok(new
                {
                    status = false,
                    message = "Data not found",
                    total_count = 0,
                    total_pages = 0,
                    page = currentPage,
                    size = pageSize,
                    data = new List<object>()
                });
            }

            // Calculate total pages
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // Apply pagination
            var students = await studentQuery
                .OrderBy(s => s.id)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var studentIds = students.Select(s => s.id).ToList();

            // Individual programs
            var individualPrograms = await
                (from c in _context.tbl_prgm_participants.AsNoTracking()

                 join pr in _context.tbl_program
                 on c.prgm_id equals pr.id into prog

                 from prg in prog.DefaultIfEmpty()

                 join s in _context.tbl_stage
                 on prg.stage_id equals s.id into sta

                 from stg in sta.DefaultIfEmpty()

                 where c.status != "deleted"
                       && c.student_id != null
                       && studentIds.Contains(c.student_id.Value)
                       && prg.program_type != "group"

                 select new
                 {
                     student_id = c.student_id,
                     id = c.id,
                     prgm_id = prg.id,
                     item_code = prg.item_code,
                     participants_type = prg.participant_type,
                     gender = prg.gender,
                     program_type = prg.program_type,
                     program_name = prg.program_name,
                     program_date = prg.date,
                     program_time = prg.time,
                     stage_id = prg.stage_id,
                     program_stage = stg.stage_name,
                     status = c.program_status
                 })
                .ToListAsync();

            // Program filters

            if (program_id.HasValue)
            {
                individualPrograms = individualPrograms
                    .Where(x => x.prgm_id == program_id.Value)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(item_code))
            {
                individualPrograms = individualPrograms
                    .Where(x => x.item_code == item_code)
                    .ToList();
            }

            if (stage_id.HasValue)
            {
                individualPrograms = individualPrograms
                    .Where(x => x.stage_id == stage_id.Value)
                    .ToList();
            }

            // Group programs
            var groupPrograms = await
                (from gm in _context.tbl_group_members.AsNoTracking()

                 join g in _context.tbl_prgm_participants
                 on gm.prgm_id equals g.prgm_id

                 join prg in _context.tbl_program
                 on g.prgm_id equals prg.id

                 join s in _context.tbl_stage
                 on prg.stage_id equals s.id into sta

                 from stg in sta.DefaultIfEmpty()

                 where gm.student_id != null
                       && studentIds.Contains(gm.student_id.Value)
                       && prg.program_type == "group"

                 select new
                 {
                     student_id = gm.student_id,
                     id = gm.id,
                     prgm_id = prg.id,
                     item_code = prg.item_code,
                     participants_type = prg.participant_type,
                     gender = prg.gender,
                     program_type = prg.program_type,
                     program_name = prg.program_name,
                     program_date = prg.date,
                     program_time = prg.time,
                     stage_id = prg.stage_id,
                     program_stage = stg.stage_name,
                     status = gm.verify_status
                 })
                .ToListAsync();

            if (program_id.HasValue)
            {
                groupPrograms = groupPrograms
                    .Where(x => x.prgm_id == program_id.Value)
                    .ToList();
            }

            if (!string.IsNullOrEmpty(item_code))
            {
                groupPrograms = groupPrograms
                    .Where(x => x.item_code == item_code)
                    .ToList();
            }

            if (stage_id.HasValue)
            {
                groupPrograms = groupPrograms
                    .Where(x => x.stage_id == stage_id.Value)
                    .ToList();
            }

            // Combine individual and group programs
            var studentPrograms = individualPrograms
                .Concat(groupPrograms)
                .GroupBy(x => new
                {
                    x.student_id,
                    x.prgm_id
                })
                .Select(g => g.First())
                .OrderBy(x => x.program_date)
                .ThenBy(x => x.program_time)
                .ToList();

            // Final result
            var result = students.Select(s => new
            {
                s.id,
                s.name,
                s.phone_no,
                s.password,
                s.institute,
                s.institute_name,
                s.code,
                s.class_id,
                s.@class,
                s.division_id,
                s.division,
                s.admsn_no,
                s.added_by,
                s.addedtype,
                s.addedon,
                s.gender,
                s.category,
                s.email,
                s.image,
                s.imageurl,

                studentprograms = studentPrograms
                    .Where(p => p.student_id == s.id)
                    .OrderBy(p => p.program_date)
                    .ThenBy(p => p.program_time)
                    .ToList()
            }).ToList();

            return Ok(new
            {
                status = true,
                message = "Success",

                total_count = totalCount,
                total_pages = totalPages,
                page = currentPage,
                size = pageSize,

                data = result
            });
        }

        [HttpDelete]
        [Route("Delete_student")]
        //public async Task<IActionResult> Delete_student([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        //{
        //    if (_context.tbl_student == null)
        //    {
        //        return Ok("Entity set 'AeDbContext.tbl_student' is null.");
        //    }

        //    var eventItem = await _context.tbl_student.FindAsync(id);

        //    if (eventItem == null)
        //    {
        //        return Ok(new { status = false, message = "Data not found" });
        //    }

        //    // Soft delete the program
        //    eventItem.status = "deleted";
        //    eventItem.deleted_by = deleted_by;
        //    eventItem.deleted_type = deleted_type;
        //    eventItem.deletedon = DateTime.Now;

        //    _context.Entry(eventItem).State = EntityState.Modified;

        //    // Remove related judgement criteria
        //    var judgement = _context.tbl_prgm_participants.Where(c => c.student_id == id);
        //    _context.tbl_prgm_participants.RemoveRange(judgement);

        //    var user = _context.tbl_user.Where(c => c.reference_id == id && c.type.ToLower()=="student");
        //    foreach (var usr in user)
        //    {
        //        usr.status = "deleted";
        //        usr.deleted_by = deleted_by;
        //        usr.deletedon = DateTime.Now;
        //    }


        //    await _context.SaveChangesAsync();

        //    return Ok(new { status = true, message = "Data deleted successfully" });
        //}

        public async Task<IActionResult> Delete_student([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_student == null)
            {
                return Ok(new { status = false, message = "Entity set 'AeDbContext.tbl_student' is null." });
            }

            var student = await _context.tbl_student.FindAsync(id);

            if (student == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Check if student is assigned to any program
            var assignedPrograms = await _context.tbl_prgm_participants
                .Where(p => p.student_id == id && p.status == "active")
                .ToListAsync();

            if (assignedPrograms.Any())
            {
                return Ok(new { status = false, message = "First delete the program assigned for this student." });
            }

            // Soft delete student
            student.status = "deleted";
            student.deleted_by = deleted_by;
            student.deleted_type = deleted_type;
            student.deletedon = DateTime.Now;
            _context.Entry(student).State = EntityState.Modified;

            // Soft delete related user records
            var users = _context.tbl_user.Where(u => u.reference_id == id && u.type.ToLower() == "student");
            foreach (var usr in users)
            {
                usr.status = "deleted";
                usr.deleted_by = deleted_by;
                usr.deletedon = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }




        [HttpPost]
        [Route("add_student_prgm")]
        public async Task<ActionResult> add_student_prgm([FromBody] List<prgm_participants> requestList)
        {
            if (_context.tbl_prgm_participants == null)
            {
                return Ok(new { status = false, message = "Entity set '_context.tbl_prgm_participants' is null." });
            }

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

            var subadminAssemblies = new List<prgm_participants>();
            var groupMembersToAdd = new List<group_members>();

            // Group students by program id
            var groupedByPrgm = requestList
                .Where(x => x.prgm_id.HasValue)
                .GroupBy(x => x.prgm_id.Value);

            foreach (var group in groupedByPrgm)
            {
                int prgmId = group.Key;

                foreach (var request in group)
                {
                    // ✅ Already added validation
                    bool isAlreadyAdded = await _context.tbl_prgm_participants
                        .AnyAsync(p => p.student_id == request.student_id &&
                                       p.prgm_id == prgmId &&
                                       p.status != "deleted");

                    if (isAlreadyAdded)
                        return Ok(new { status = false, message = $"Student {request.student_id} is already added to this program." });

                    // ✅ Student validation
                    var studentDetails = await _context.tbl_student
                        .Where(s => s.id == request.student_id)
                        .Select(s => new { s.institute, s.gender })
                        .FirstOrDefaultAsync();

                    if (studentDetails == null)
                        return Ok(new { status = false, message = "Student not found." });

                    // ✅ Program validation
                    var program = await _context.tbl_program
                        .Where(p => p.id == prgmId && p.delete_status != "deleted")
                        .Select(p => new
                        {
                            p.program_type,
                            p.gender,
                            p.group_min_participants,
                            p.group_max_participants,
                            p.status
                        })
                        .FirstOrDefaultAsync();

                    if (program == null)
                        return Ok(new { status = false, message = "Program not found." });

                    if (program.status.ToLower() != "pending")
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"Cannot add participants. Program status is '{program.status}'. Only programs with 'pending' status allow participant additions."
                        });
                    }

                    // ✅ Gender validation
                    string programGender = program.gender?.Trim().ToLower().Replace("s", "");
                    string studentGender = studentDetails.gender?.Trim().ToLower();

                    if (!string.IsNullOrEmpty(programGender) &&
                        programGender != "common" &&
                        !string.IsNullOrEmpty(studentGender) &&
                        programGender != studentGender)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"This program is only for {program.gender} students."
                        });
                    }

                    // ✅ Existing participant count from same institute
                    int existingCount = await (from p in _context.tbl_prgm_participants
                                               join s in _context.tbl_student on p.student_id equals s.id
                                               where p.prgm_id == prgmId &&
                                                     s.institute == studentDetails.institute &&
                                                     p.status != "deleted"
                                               select p).CountAsync();

                    int newParticipantCount = group.Count();
                    int totalCount = existingCount + newParticipantCount;

                    if (program.program_type.ToLower() == "group")
                    {
                        if (totalCount > program.group_max_participants)
                        {
                            return Ok(new
                            {
                                status = false,
                                message = $"Maximum {program.group_max_participants} participants allowed from this institute. Already: {existingCount}, Adding: {newParticipantCount}"
                            });
                        }

                        // First-time group registration → add leader to participants
                        if (existingCount == 0)
                        {
                            subadminAssemblies.Add(new prgm_participants
                            {
                                student_id = request.student_id,
                                prgm_id = prgmId,
                                addedon = DateTime.Now,
                                status = "active",
                                program_status = "pending"
                            });
                        }

                        // Prevent duplicate group members
                        bool existsInGroup = await _context.tbl_group_members
                            .AnyAsync(g => g.prgm_id == prgmId && g.student_id == request.student_id);

                        if (existsInGroup)
                        {
                            return Ok(new
                            {
                                status = false,
                                message = $"Student {request.student_id} is already a group member for this program."
                            });
                        }

                        // Add group member
                        groupMembersToAdd.Add(new group_members
                        {
                            student_id = request.student_id,
                            prgm_id = prgmId,
                            verify_status = "pending"
                        });
                    }
                    else if (program.program_type.ToLower() == "solo")
                    {
                        if (totalCount > 2)
                        {
                            return Ok(new
                            {
                                status = false,
                                message = $"Only two student from this institute can register for a solo program."
                            });
                        }

                        // Add solo student
                        subadminAssemblies.Add(new prgm_participants
                        {
                            student_id = request.student_id,
                            prgm_id = prgmId,
                            addedon = DateTime.Now,
                            status = request.status ?? "active",
                            program_status = "pending"
                        });
                    }
                }
            }

            if (subadminAssemblies.Any())
                _context.tbl_prgm_participants.AddRange(subadminAssemblies);

            if (groupMembersToAdd.Any())
                _context.tbl_group_members.AddRange(groupMembersToAdd);

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data added successfully" });
        }



        [HttpPut]
        [Route("update_student_prgm")]
        public async Task<ActionResult> update_student_prgm([FromBody] List<prgm_participants> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return Ok(new { status = false, message = "No data provided for update" });
            }

            var studentIds = requests.Select(r => r.student_id).Distinct().ToList();

            // Remove existing program participants for these students
            var existingRecords = _context.tbl_prgm_participants
                                          .Where(x => studentIds.Contains(x.student_id))
                                          .ToList();

            if (existingRecords.Any())
            {
                _context.tbl_prgm_participants.RemoveRange(existingRecords);
            }

            // Also remove existing group members for these students
            var existingGroupMembers = _context.tbl_group_members
                                               .Where(x => studentIds.Contains(x.student_id))
                                               .ToList();

            if (existingGroupMembers.Any())
            {
                _context.tbl_group_members.RemoveRange(existingGroupMembers);
            }

            var newParticipants = new List<prgm_participants>();
            var groupMembers = new List<group_members>();

            // Remove duplicates from request (based on prgm_id + student_id)
            var uniqueRequests = requests
                .Where(r => r.prgm_id != null)
                .GroupBy(r => new { r.prgm_id, r.student_id })
                .Select(g => g.First())
                .ToList();

            // Group by prgm_id
            var groupedByPrgm = uniqueRequests.GroupBy(x => x.prgm_id.Value);

            foreach (var group in groupedByPrgm)
            {
                int prgmId = group.Key;

                // Get program type
                var program = await _context.tbl_program
                    .Where(p => p.id == prgmId)
                    .Select(p => new { p.program_type })
                    .FirstOrDefaultAsync();

                if (program == null)
                {
                    return Ok(new { status = false, message = $"Program {prgmId} not found" });
                }

                if (program.program_type == "group")
                {
                    // Add first student to tbl_prgm_participants
                    var first = group.First();
                    first.addedon = DateTime.Now;
                    first.status = first.status ?? "active";
                    newParticipants.Add(first);

                    // All to tbl_group_members
                    foreach (var req in group)
                    {
                        groupMembers.Add(new group_members
                        {
                            prgm_id = prgmId,
                            student_id = req.student_id
                        });
                    }

                    continue;
                }

                // For "single" program_type
                foreach (var req in group)
                {
                    req.addedon = DateTime.Now;
                    req.status = req.status ?? "active";
                    newParticipants.Add(req);
                }
            }

            if (newParticipants.Any())
                _context.tbl_prgm_participants.AddRange(newParticipants);

            if (groupMembers.Any())
                _context.tbl_group_members.AddRange(groupMembers);

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }


        [HttpGet]
        [Route("get_student_prgm")]
        //public async Task<ActionResult> get_student_prgm(int? student_id)
        //{
        //    if (_context.tbl_student == null)
        //        return Ok(new { status = false, message = "Data not found" });

        //    var baseUrl = $"{Request.Scheme}://{Request.Host}/";

        //    // Get student info
        //    var studentInfo = await (from st in _context.tbl_student
        //                             join i in _context.tbl_institute on st.institute equals i.id into inst
        //                             from ins in inst.DefaultIfEmpty()
        //                             where student_id == null || st.id == student_id.Value
        //                             select new
        //                             {
        //                                 st.id,
        //                                 st.name,
        //                                 st.admsn_no,
        //                                 st.email,
        //                                 st.phone_no,
        //                                 st.gender,
        //                                 st.image,
        //                                 imageurl = !string.IsNullOrEmpty(st.image) ? $"{baseUrl}uploads/student/{st.image}" : null,
        //                                 st.institute,
        //                                 institute_name = ins.name
        //                             }).FirstOrDefaultAsync();

        //    if (studentInfo == null)
        //        return Ok(new { status = false, message = "Student not found" });

        //    // Fetch all active programs where the student is solo participant
        //    var soloPrograms = await (from sp in _context.tbl_prgm_participants
        //                              join pr in _context.tbl_program on sp.prgm_id equals pr.id
        //                              where sp.student_id == studentInfo.id && sp.status == "active" && pr.program_type == "Solo"
        //                              select new
        //                              {
        //                                  pr.id,
        //                                  pr.program_name,
        //                                  pr.program_type
        //                              }).ToListAsync();

        //    // Fetch all group programs where student is a group member
        //    var groupPrograms = await (from gm in _context.tbl_group_members
        //                               join pr in _context.tbl_program on gm.prgm_id equals pr.id
        //                               where gm.student_id == studentInfo.id
        //                               select new
        //                               {
        //                                   pr.id,
        //                                   pr.program_name,
        //                                   pr.program_type
        //                               }).ToListAsync();

        //    // Map solo programs
        //    var allPrograms = soloPrograms.Select(sp => new
        //    {
        //        student_id = studentInfo.id,
        //        student_name = studentInfo.name,
        //        studentInfo.admsn_no,
        //        studentInfo.email,
        //        studentInfo.phone_no,
        //        studentInfo.gender,
        //        studentInfo.image,
        //        studentInfo.imageurl,
        //        studentInfo.institute,
        //        studentInfo.institute_name,
        //        prgm_id = sp.id,
        //        program_name = sp.program_name,
        //        program_type = "solo"
        //    }).ToList();

        //    // Map group programs
        //    allPrograms.AddRange(groupPrograms.Select(gp => new
        //    {
        //        student_id = studentInfo.id,
        //        student_name = studentInfo.name,
        //        studentInfo.admsn_no,
        //        studentInfo.email,
        //        studentInfo.phone_no,
        //        studentInfo.gender,
        //        studentInfo.image,
        //        studentInfo.imageurl,
        //        studentInfo.institute,
        //        studentInfo.institute_name,
        //        prgm_id = gp.id,
        //        program_name = gp.program_name,
        //        program_type = "group"
        //    }));

        //    // Order by program id
        //    allPrograms = allPrograms.OrderBy(x => x.prgm_id).ToList();

        //    return Ok(new { status = true, message = "Success", data = allPrograms });
        //}

        public async Task<ActionResult> get_student_prgm(int? student_id)
        {
            if (_context.tbl_student == null)
                return Ok(new { status = false, message = "Data not found" });

            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            // Get student info
            var studentInfo = await (from st in _context.tbl_student
                                     join i in _context.tbl_institute on st.institute equals i.id into inst
                                     from ins in inst.DefaultIfEmpty()
                                     where student_id == null || st.id == student_id.Value
                                     select new
                                     {
                                         st.id,
                                         st.name,
                                         st.admsn_no,
                                         st.email,
                                         st.phone_no,
                                         st.gender,
                                         st.image,
                                         imageurl = !string.IsNullOrEmpty(st.image) ? $"{baseUrl}uploads/student/{st.image}" : null,
                                         st.institute,
                                         institute_name = ins.name,
                                         st.category
                                     }).FirstOrDefaultAsync();

            if (studentInfo == null)
                return Ok(new { status = false, message = "Student not found" });

            // Fetch all active solo programs
            var soloPrograms = await (from sp in _context.tbl_prgm_participants
                                      join pr in _context.tbl_program on sp.prgm_id equals pr.id
                                      where sp.student_id == studentInfo.id && sp.status == "active" && pr.program_type == "Solo"
                                      select new
                                      {
                                          pr.id,
                                          pr.program_name,
                                          pr.program_type,pr.item_code
                                      }).ToListAsync();

            // Fetch all group programs
            var groupPrograms = await (from gm in _context.tbl_group_members
                                       join pr in _context.tbl_program on gm.prgm_id equals pr.id
                                       where gm.student_id == studentInfo.id
                                       select new
                                       {
                                           pr.id,
                                           pr.program_name,
                                           pr.program_type,pr.item_code
                                       }).ToListAsync();

            // Merge programs using List<dynamic> so we can add null program if needed
            List<dynamic> allPrograms = soloPrograms.Select(sp => new
            {
                student_id = studentInfo.id,
                student_name = studentInfo.name,
                studentInfo.admsn_no,
                studentInfo.email,
                studentInfo.phone_no,
                studentInfo.gender,
                studentInfo.image,
                studentInfo.imageurl,
                studentInfo.institute,
                studentInfo.institute_name,
                prgm_id = sp.id,
                program_name = sp.program_name,
                program_type = "solo",
                item_code = sp.item_code,
                category = studentInfo.category
            }).Cast<dynamic>().ToList();

            allPrograms.AddRange(groupPrograms.Select(gp => new
            {
                student_id = studentInfo.id,
                student_name = studentInfo.name,
                studentInfo.admsn_no,
                studentInfo.email,
                studentInfo.phone_no,
                studentInfo.gender,
                studentInfo.image,
                studentInfo.imageurl,
                studentInfo.institute,
                studentInfo.institute_name,
                prgm_id = gp.id,
                program_name = gp.program_name,
                program_type = "group",

                item_code = gp.item_code,
                category = studentInfo.category
            }).Cast<dynamic>());

            // If no programs exist, return student info with programs as null
            if (!allPrograms.Any())
            {
                allPrograms.Add(new
                {
                    student_id = studentInfo.id,
                    student_name = studentInfo.name,
                    admsn_no = studentInfo.admsn_no,
                    email = studentInfo.email,
                    phone_no = studentInfo.phone_no,
                    gender = studentInfo.gender,
                    image = studentInfo.image,
                    imageurl = studentInfo.imageurl,
                    institute = studentInfo.institute,
                    institute_name = studentInfo.institute_name,
                    prgm_id = (int?)null,
                    program_name = (string)null,
                    program_type = (string)null,
                    item_code = (string)null,
                    category = (string)null

                });
            }

            // Order by program id
            allPrograms = allPrograms.OrderBy(x => x.prgm_id).ToList();

            return Ok(new { status = true, message = "Success", data = allPrograms });
        }


        [HttpDelete]
        [Route("Delete_student_prgm")]
        //public async Task<IActionResult> Delete_student_prgm([FromForm] int prgm_id, [FromForm] int student_id)
        //{
        //    if (_context.tbl_prgm_participants == null)
        //    {
        //        return Problem("Entity set 'AeDbContext.tbl_prgm_participants' is null.");
        //    }

        //    // Get the participant entry
        //    var eventItem = await _context.tbl_prgm_participants
        //        .FirstOrDefaultAsync(p => p.prgm_id == prgm_id && p.student_id == student_id && p.status != "deleted");

        //    if (eventItem == null)
        //    {
        //        return Ok(new { status = false, message = "Data not found" });
        //    }

        //    // Check if points have been assigned
        //    bool pointsExist = await _context.tbl_prgm_point
        //        .AnyAsync(p => p.student_id == student_id && p.prgm_id == prgm_id);

        //    if (pointsExist)
        //    {
        //        return Ok(new { status = false, message = "Cannot delete since judge has already assigned points to this student for the program." });
        //    }

        //    // Check program type
        //    var program = await _context.tbl_program
        //        .Where(p => p.id == prgm_id)
        //        .Select(p => new { p.program_type })
        //        .FirstOrDefaultAsync();

        //    if (program != null && program.program_type.ToLower() == "group")
        //    {
        //        // Delete from tbl_group_members where prgm_id and student_id match
        //        var groupMember = await _context.tbl_group_members
        //            .Where(g => g.prgm_id == prgm_id)
        //            .ToListAsync();

        //        if (groupMember.Any())
        //        {
        //            _context.tbl_group_members.RemoveRange(groupMember);
        //        }
        //    }

        //    // Soft-delete from tbl_prgm_participants
        //    //eventItem.status = "deleted";
        //    //eventItem.deletedon = DateTime.Now;
        //    //_context.Entry(eventItem).State = EntityState.Modified;
        //    // 🔍 Conditional delete
        //    if (eventItem != null && eventItem.program_status == "Verification Completed")
        //    {
        //        // Soft delete
        //        eventItem.status = "deleted";
        //        eventItem.deletedon = DateTime.Now;
        //        _context.Entry(eventItem).State = EntityState.Modified;
        //    }
        //    else
        //    {
        //        // Hard delete
        //        _context.tbl_prgm_participants.Remove(eventItem);
        //    }


        //    await _context.SaveChangesAsync();

        //    return Ok(new { status = true, message = "Data deleted successfully" });
        //}

        public async Task<IActionResult> Delete_student_prgm([FromForm] int prgm_id, [FromForm] int student_id)
        {
            if (_context.tbl_prgm_participants == null)
            {
                return Ok("Entity set 'AeDbContext.tbl_prgm_participants' is null.");
            }

            // Check program type
            var program = await _context.tbl_program
                .Where(p => p.id == prgm_id)
                .Select(p => new { p.program_type })
                .FirstOrDefaultAsync();

            //if (program != null && program.program_type.ToLower() == "group")
            //{
            //    // Check if the student is the main group participant
            //    bool isMainGroupParticipant = await _context.tbl_prgm_participants
            //        .AnyAsync(p => p.prgm_id == prgm_id && p.student_id == student_id && p.status != "deleted");

            //    if (isMainGroupParticipant)
            //    {
            //        // Do not delete main participant
            //        return Ok(new { status = false, message = "Cannot delete main group participant from a group program." });
            //    }

            //    // Student is not main participant → check in tbl_group_members
            //    var groupMember = await _context.tbl_group_members
            //        .FirstOrDefaultAsync(g => g.prgm_id == prgm_id && g.student_id == student_id);

            //    if (groupMember == null)
            //    {
            //        return Ok(new { status = false, message = "Data not found" });
            //    }

            //    // Delete the student from tbl_group_members
            //    _context.tbl_group_members.Remove(groupMember);
            //    await _context.SaveChangesAsync();

            //    return Ok(new { status = true, message = "Student removed from group program successfully" });
            //}

            // For non-group programs
            var eventItem = await _context.tbl_prgm_participants
                .FirstOrDefaultAsync(p => p.prgm_id == prgm_id && p.student_id == student_id && p.status != "deleted");

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Check if points have been assigned
            bool pointsExist = await _context.tbl_prgm_point
                .AnyAsync(p => p.student_id == student_id && p.prgm_id == prgm_id);

            if (pointsExist)
            {
                return Ok(new { status = false, message = "Cannot delete since judge has already assigned points to this student for the program." });
            }

            // 🔍 Conditional delete (for non-group programs only)
            if (eventItem.program_status.ToLower() != "pending")
            {
                return Ok(new { status = false, message = "Cannot delete. Only programs with pending status can be removed." });
            }
            else
            {
                // Hard delete
                _context.tbl_prgm_participants.Remove(eventItem);
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }




        //web 
        [HttpGet]
        [Route("get_student_point_details")]

        public async Task<ActionResult> get_student_point_details(int student_id, int program_id)
        {
            var result = await (from jp in _context.tbl_prgm_point
                                join s in _context.tbl_student on jp.student_id equals s.id into studentJoin
                                from s in studentJoin.DefaultIfEmpty()

                                join j in _context.tbl_judgement_criteria on jp.judgement_criteria_id equals j.id into criteriaJoin
                                from j in criteriaJoin.DefaultIfEmpty()

                                join jd in _context.tbl_judge on jp.judge_id equals jd.id into judgeJoin
                                from jd in judgeJoin.DefaultIfEmpty()

                                where jp.student_id == student_id && jp.prgm_id == program_id
                                select new
                                {
                                    student_name = s != null ? s.name : null,
                                    judgement_criteria = j != null ? j.name : null,
                                    jp.judge_id,
                                    judge_name = jd != null ? jd.judge_name : null,
                                    jp.point
                                }).ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "No data found" });
            }

            var studentName = result.First().student_name;

            var grouped = result
                .GroupBy(r => r.judgement_criteria)
                .Select(g => new
                {
                    criteria = g.Key,
                    total = g.Sum(x => x.point ?? 0),
                    judge_points = g
                        .GroupBy(x => new { x.judge_id, x.judge_name }) //  group by both id and name
                        .Select(jg => new
                        {
                            judge_name = jg.Key.judge_name,
                            points = jg.Sum(y => y.point ?? 0)
                        })
                        .ToList()
                }).ToList();

            var totalPoints = (from participant in _context.tbl_prgm_participants
                               where participant.student_id == student_id && participant.prgm_id == program_id
                               select participant.average_point).FirstOrDefault();

            return Ok(new
            {
                status = true,
                message = "Success",
                student_name = studentName,
                total_points = totalPoints,
                details = grouped
            });
        }


        //verify student

        [HttpPut]
        [Route("verify_status")]

        public async Task<ActionResult> verify_status([FromForm] int? program_id, [FromForm] int? student_id, [FromForm] string? status, [FromForm] int? verified_by)
        {
            int? ac_year_id = await _context.tbl_program
     .Where(e => e.id == program_id)
     .Select(e => e.ac_year_id)
     .FirstOrDefaultAsync();
            //fetch events
            var eventData = await _context.tbl_event
                .FirstOrDefaultAsync(e => e.status == "active"&&e.ac_year_id== ac_year_id);
            //
            if (student_id == null)
                return BadRequest(new { status = false, message = "student_id is required" });

            if (program_id == null)
                return BadRequest(new { status = false, message = "program_id is required" });

            if (string.IsNullOrEmpty(status))
                return BadRequest(new { status = false, message = "Status is required" });

            // ✅ Fetch program details first
            //var prgm = await _context.tbl_program.FindAsync(program_id);
            var prgm = await (
    from p in _context.tbl_program
    join s in _context.tbl_stage on p.stage_id equals s.id into stageJoin
    from s in stageJoin.DefaultIfEmpty()
    where p.id == program_id
    select new
    {
        p.id,
        p.program_name,
        p.program_type,
        p.stage_id,
        stage = s.stage_name,
        participant_type = p.participant_type,
        gender = p.gender,
        item_code = p.item_code,
        date = p.date,
        s.is_datewise,
        s.token_prefix
    }
).FirstOrDefaultAsync();


            if (prgm == null)
                return NotFound(new { status = false, message = "Program not found." });

            if (prgm.stage_id == null)
                return BadRequest(new { status = false, message = "Program stage_id is missing." });

            var student = await _context.tbl_student.FindAsync(student_id);
            if (student == null)
                return NotFound(new { status = false, message = "Student not found." });

            // ✅ Generate token
            //string token_no = TokenGenerator.GetNextToken(prgm.stage_id.Value);
            string token_no = TokenGenerator.GetNextToken(prgm.token_prefix, prgm.is_datewise);

            if (prgm.program_type.ToLower() != "group")
            {
                // For Individual Programs
                var participant = await _context.tbl_prgm_participants
                    .FirstOrDefaultAsync(p => p.prgm_id == program_id && p.student_id == student_id && p.status != "deleted");

                if (participant == null)
                    return NotFound(new { status = false, message = "This student is not part of the specified program." });

                if (!string.IsNullOrEmpty(participant.program_status) && participant.program_status.ToLower() == "Verification Completed")
                {
                    return Ok(new { status = false, message = "This student is already verified for this program." });
                }

                participant.program_status = status;
                participant.user_id = verified_by;
                participant.status_updated = DateTime.Now;
                participant.token_no = token_no;
            }
            else
            {
                //// For Group Programs
                //var groupMember = await _context.tbl_group_members
                //    .FirstOrDefaultAsync(g => g.student_id == student_id && g.prgm_id == program_id);

                //if (groupMember == null)
                //    return NotFound(new { status = false, message = "This student is not part of the specified group program." });

                //if (!string.IsNullOrEmpty(groupMember.verify_status) && groupMember.verify_status.ToLower() == "verification completed")
                //{
                //    return Ok(new { status = false, message = "This student is already verified for this group program." });
                //}

                //// Update student in tbl_group_members
                //groupMember.verify_status = status;
                //groupMember.token_no = token_no;
                //groupMember.status_updated = DateTime.Now;
                //groupMember.user_id = verified_by;

                //  Also update the group record in tbl_prgm_participants (single entry)
                var groupParticipant = await _context.tbl_prgm_participants
                    .FirstOrDefaultAsync(p => p.prgm_id == program_id && p.student_id == student_id && p.status != "deleted");

                if (groupParticipant != null)
                {
                    groupParticipant.program_status = status;
                    groupParticipant.token_no = token_no;
                    groupParticipant.status_updated = DateTime.Now;
                    groupParticipant.user_id = verified_by;
                }
            }

            await _context.SaveChangesAsync();

            // ✅ Prepare program details for response
            var programData = new
            {
                program_id = prgm.id,
                program_name = prgm.program_name,
                program_type = prgm.program_type,
                participant_type = prgm.participant_type,
                item_code = prgm.item_code,
                gender = prgm.gender,
                date = prgm.date,
                stage = prgm.stage,
            };

            return Ok(new
            {
                status = true,
                message = "Status updated successfully",
                event_name = eventData.event_name,

                token_no = token_no,
                date = programData.date,
                name = student.name,
                qatar_id = student.admsn_no,
                program_id = programData.program_id,
                program_name = programData.program_name,
                program_type = programData.program_type,

                participant_type = programData.participant_type,
                item_code = programData.item_code,
                gender = programData.gender,
                stage = programData.stage,
            });
        }

        //public static class TokenGenerator
        //{
        //    private static Dictionary<int, int> stageTokenTracker = new Dictionary<int, int>();

        //    public static string GetNextToken(int stage_id)
        //    {
        //        if (!stageTokenTracker.ContainsKey(stage_id))
        //        {
        //            stageTokenTracker[stage_id] = 1;
        //        }
        //        else
        //        {
        //            stageTokenTracker[stage_id]++;
        //        }

        //        return $"S{stage_id.ToString().PadLeft(2, '0')}-{stageTokenTracker[stage_id].ToString().PadLeft(3, '0')}";
        //    }
        //}

        public static class TokenGenerator
        {
            // Tracks last token per key
            private static readonly Dictionary<string, int> stageTokenTracker = new Dictionary<string, int>();
            private static readonly object lockObj = new object();

            public static string GetNextToken(string token_prefix, string is_datewise)
            {
                if (string.IsNullOrEmpty(token_prefix))
                    token_prefix = "S"; // default prefix if null or empty

                bool datewiseFlag = !string.IsNullOrEmpty(is_datewise) && is_datewise.ToLower() == "yes";

                string key;

                if (datewiseFlag)
                {
                    // If datewise, include date in key to reset daily
                    key = $"{token_prefix}_{DateTime.Now:yyyyMMdd}";
                }
                else
                {
                    // If not datewise, just use prefix
                    key = token_prefix;
                }

                int tokenNumber;

                lock (lockObj)
                {
                    if (!stageTokenTracker.ContainsKey(key))
                    {
                        // Check if we should continue from previous day
                        if (datewiseFlag)
                        {
                            // Try to get previous day's last token for this prefix
                            string prevDayKey = $"{token_prefix}_{DateTime.Now.AddDays(-1):yyyyMMdd}";
                            if (stageTokenTracker.ContainsKey(prevDayKey))
                                stageTokenTracker[key] = stageTokenTracker[prevDayKey] + 1;
                            else
                                stageTokenTracker[key] = 1;
                        }
                        else
                        {
                            stageTokenTracker[key] = 1;
                        }
                    }
                    else
                    {
                        stageTokenTracker[key]++;
                    }

                    tokenNumber = stageTokenTracker[key];
                }

                string tokenPart = tokenNumber.ToString().PadLeft(3, '0');
                return $"{token_prefix}{tokenPart}";
            }
        }

        //registered students from institute
        [HttpGet]
        [Route("get_registered_students")]
        public async Task<ActionResult> get_registered_students(
    int? program_id,
    int? institute_id,
    string? participant_category,
    string? keyword,
    string? item_code,
    int?ac_year_id,
    int page = 1,
    int pageSize = 100)
        {
            if (!institute_id.HasValue)
            {
                return Ok(new { status = false, message = "Please provide institute_id" });
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            // SOLO participants
            var soloQuery = from s in _context.tbl_prgm_participants
                            join p in _context.tbl_program on s.prgm_id equals p.id
                            join st in _context.tbl_student on s.student_id equals st.id
                            where st.institute == institute_id.Value
                                  && s.status != "deleted"
                                  && p.program_type == "solo"&&p.delete_status!= "deleted"
                            select new
                            {
                                student_id = s.student_id,
                                st.name,
                                st.image,
                                st.gender,
                                st.phone_no,
                                st.admsn_no,
                                program_name = p.program_name,
                                program_type = p.program_type,
                                prgm_id = s.prgm_id,
                                participant_type = p.participant_type,
                                item_code = p.item_code,
                                group_name = (string?)null,
                                st.ac_year_id
                            };

            // GROUP participants
            var groupQuery = from gm in _context.tbl_group_members
                             join p in _context.tbl_program on gm.prgm_id equals p.id
                             join st in _context.tbl_student on gm.student_id equals st.id
                             where st.institute == institute_id.Value
                                   && p.program_type == "group" && st.status != "deleted" && p.delete_status!= "deleted"
                             select new
                             {
                                 student_id = gm.student_id,
                                 st.name,
                                 st.image,
                                 st.gender,
                                 st.phone_no,
                                 st.admsn_no,
                                 program_name = p.program_name,
                                 program_type = p.program_type,
                                 prgm_id = gm.prgm_id,
                                 participant_type = p.participant_type,
                                 item_code = p.item_code,
                                 group_name = gm.group_name,
                                 st.ac_year_id
                             };

            // Combine both BEFORE doing any extra projection
            var baseQuery = soloQuery.Union(groupQuery);

            // Apply filters
            if (program_id.HasValue)
                baseQuery = baseQuery.Where(g => g.prgm_id == program_id.Value);

            if (!string.IsNullOrWhiteSpace(participant_category))
                baseQuery = baseQuery.Where(e => e.participant_type == participant_category);

            if (!string.IsNullOrWhiteSpace(item_code))
                baseQuery = baseQuery.Where(e => e.item_code == item_code);
            if (ac_year_id.HasValue)
            {
                if (ac_year_id.HasValue)
                    baseQuery = baseQuery.Where(g => g.ac_year_id == ac_year_id.Value);

            }


            if (!string.IsNullOrWhiteSpace(keyword))
            {
                baseQuery = baseQuery.Where(e =>
                    e.program_type.Contains(keyword) ||
                    e.gender.Contains(keyword));
            }
          
            // Now transform (after DB execution)
            var projectedQuery = baseQuery.Select(x => new
            {
                x.student_id,
                x.name,
                x.image,
                imageurl = !string.IsNullOrEmpty(x.image) ? $"{baseUrl}uploads/student/{x.image}" : null,
                x.gender,
                x.phone_no,
                x.admsn_no,
                x.program_name,
                x.program_type,
                x.prgm_id,
                x.participant_type,
                x.item_code,
                x.group_name
            });

            // Group by student
            var groupedData = projectedQuery
                .GroupBy(x => new { x.student_id, x.name, x.gender, x.phone_no, x.admsn_no, x.participant_type })
                .Select(g => new
                {
                    student_id = g.Key.student_id,
                    student_name = g.Key.name,
                    gender = g.Key.gender,
                    participant_type = g.Key.participant_type,
                    phone = g.Key.phone_no,
                    qid = g.Key.admsn_no,
                    image = g.Select(x => x.image).FirstOrDefault(),
                    image_url = g.Select(x => x.imageurl).FirstOrDefault(),
                    //  group_name=g.Select(x => x.group_name).FirstOrDefault(),        
                    solo_programs = g
                        .Where(x => x.program_type != null && x.program_type.ToLower() == "solo")
                        .Select(x => new { x.program_name, x.item_code })
                        .Distinct()
                        .ToList(),

                    group_programs = g
                        .Where(x => x.program_type != null && x.program_type.ToLower() == "group")
                        .Select(x => new { x.program_name, x.item_code, x.group_name })
                        .Distinct()
                        .ToList()
                });

            var totalCount = await groupedData.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var pagedData = await groupedData
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (pagedData == null || pagedData.Count == 0)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new
            {
                status = true,
                message = "Success",
                currentPage = page,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = totalPages,
                data = pagedData
            });
        }

        //

        //
        //get results from chief_pm announcement 
        [HttpGet]
        [Route("get_results_from_chief_pm")]
        public async Task<ActionResult> get_results_from_chief_pm(int? stage_id)
        {
            if (_context.tbl_prgm_participants == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var result = await (
                from pp in _context.tbl_prgm_participants
                join p in _context.tbl_program on pp.prgm_id equals p.id into programGroup
                from p in programGroup.DefaultIfEmpty()

                join s in _context.tbl_student on pp.student_id equals s.id into studentGroup
                from s in studentGroup.DefaultIfEmpty()

                where stage_id == null || p.stage_id == stage_id

                group new { pp, p, s } by new
                {
                    pp.prgm_id,
                    pp.position,
                    p.program_name,
                    p.item_code,
                    p.stage_id,
                    pp.program_status
                } into g
                select new
                {
                    prgm_id = g.Key.prgm_id,
                    position = g.Key.position,
                    program_name = g.Key.program_name,
                    item_code = g.Key.item_code,
                    stage_id = g.Key.stage_id,
                    program_status = g.Key.program_status,
                    name = g.Select(x => x.s.name).FirstOrDefault() ?? ""
                }
            ).ToListAsync();

            if (result == null || !result.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }

        //

        //student excel
        [HttpGet]
        [Route("students_excel")]
        public async Task<IActionResult> students_excel(DateTime? from_date, DateTime? to_date,int?ac_year_id)
        {
            // Base query - returns anonymous type
            var query = from p in _context.tbl_student.AsNoTracking()
                        join i in _context.tbl_institute on p.institute equals i.id into ins
                        from it in ins.DefaultIfEmpty()
                        join c in _context.tbl_class on p.class_id equals c.id into cla
                        from cl in cla.DefaultIfEmpty()
                        join d in _context.tbl_division on p.division_id equals d.id into div
                        from di in div.DefaultIfEmpty()
                        join ac in _context.tbl_academic_year on p.ac_year_id equals ac.id
                        where p.status == "active"  && p.ac_year_id==ac_year_id
                        select new
                        {
                            id = p.id,
                            name = p.name,
                            phone_no = p.phone_no,
                            password = p.password,
                            institute = p.institute,
                            institute_name = it.name,
                            code = it.code,
                            class_id = p.class_id,
                            @class = cl.@class,
                            division_id = p.division_id,
                            division = di.division,
                            admsn_no = p.admsn_no,
                            gender = p.gender,
                            email = p.email,
                            category = p.category,
                            addedon = p.addedon,
                            ac_year_id=p.ac_year_id,
                            ac.year
                        };

            // Apply filters if provided
            if (from_date.HasValue)
            {
                query = query.Where(r => r.addedon >= from_date.Value);
            }

            if (to_date.HasValue)
            {
                var toDateInclusive = to_date.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.addedon <= toDateInclusive);
            }
            if (ac_year_id.HasValue)
            {
                query = query.Where(x => x.ac_year_id.HasValue &&
                                         x.ac_year_id.Value == ac_year_id.Value);
            }
            var regList = await query.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Students");

                // Headers - ensure these match the data columns below
                var headers = new[]
                {
            "Student ID", "Name", "Phone Number", "Institute",
            "Email", "Category", "Q ID", "Gender","Academic Year"
        };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Rows
                for (int rowIndex = 0; rowIndex < regList.Count; rowIndex++)
                {
                    var student = regList[rowIndex];
                    int row = rowIndex + 2;

                    worksheet.Cell(row, 1).Value = student.id;
                    worksheet.Cell(row, 2).Value = student.name ?? string.Empty;
                    worksheet.Cell(row, 3).Value = student.phone_no ?? string.Empty;
                    worksheet.Cell(row, 4).Value = student.institute_name ?? string.Empty;
                    worksheet.Cell(row, 5).Value = student.email ?? string.Empty;
                    worksheet.Cell(row, 6).Value = student.category ?? string.Empty;
                    worksheet.Cell(row, 7).Value = student.admsn_no ?? string.Empty; // Q ID
                    worksheet.Cell(row, 8).Value = student.gender ?? string.Empty;
                    worksheet.Cell(row, 9).Value = student.year;

                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"students.xlsx");
                }
            }
        }

        //
        [HttpGet]
        [Route("is_student_exist")]
        public async Task<ActionResult> is_student_exist(int institute, string q_id, string? category, int ac_year_id)
        {
            var result = await _context.tbl_student
    .Where(x => x.institute == institute
             && x.admsn_no == q_id
             && x.status == "active" && x.ac_year_id == ac_year_id)
    .OrderByDescending(x => x.id)
    .ToListAsync();


            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "No data found" });
            }

            return Ok(new
            {
                status = true,
                message = "Success",
                data = result
            });
        }




        [HttpDelete]
        [Route("remove_student_programs")]
        public async Task<IActionResult> remove_student_programs([FromForm] int student_id, [FromForm] int program_ids)
        {
            var record = await _context.tbl_prgm_participants
                .FirstOrDefaultAsync(x => x.student_id == student_id && x.prgm_id == program_ids);

            if (record == null)
            {
                return Ok(new
                {
                    success = false,
                    message = "No matching data found"
                });
            }

            _context.tbl_prgm_participants.Remove(record);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Program deleted successfully"
            });
        }


        //get students registered for programs
        [HttpGet]
        [Route("student_registrations_excel")]
        public async Task<IActionResult> student_registrations_excel(int? institute_id, int? student_id, int? program_id, string? program_type, string? participant_type,int?ac_year_id)
        {
            // Query for non-group participants
            var soloQuery = from p in _context.tbl_prgm_participants
                            join s in _context.tbl_student on p.student_id equals s.id
                            join pr in _context.tbl_program on p.prgm_id equals pr.id
                            join i in _context.tbl_institute on s.institute equals i.id into instituteJoin
                            from i in instituteJoin.DefaultIfEmpty()
                            join ac in _context.tbl_academic_year on s.ac_year_id equals ac.id
                            where p.status == "active" && pr.program_type.ToLower() != "group"
                            select new
                            {
                                p.prgm_id,
                                p.student_id,
                                pr.program_name,
                                pr.item_code,
                                pr.program_type,
                                pr.participant_type,
                                s.institute,
                                institute_name = i.name,
                                s.name,
                                s.phone_no,
                                s.admsn_no,
                                s.gender,
                                no_of_group = (string?)null,   // keep same structure
                                group_name = (string?)null,
                                ac.year,
                                s.ac_year_id
                            };

            // Query for group members
            var groupQuery = from gm in _context.tbl_group_members
                             join s in _context.tbl_student on gm.student_id equals s.id
                             join pr in _context.tbl_program on gm.prgm_id equals pr.id
                             join i in _context.tbl_institute on s.institute equals i.id into instituteJoin
                             from i in instituteJoin.DefaultIfEmpty()
                             join ac in _context.tbl_academic_year on s.ac_year_id equals ac.id

                             where pr.program_type.ToLower() == "group"
                             select new
                             {
                                 prgm_id = gm.prgm_id,
                                 student_id = gm.student_id,
                                 pr.program_name,
                                 pr.item_code,
                                 pr.program_type,
                                 pr.participant_type,
                                 s.institute,
                                 institute_name = i.name,
                                 s.name,
                                 s.phone_no,
                                 s.admsn_no,
                                 s.gender,
                                 pr.no_of_group,
                                 gm.group_name,
                                 ac.year,
                                 s.ac_year_id


                             };

            // Combine both
            var query = soloQuery.Union(groupQuery);

            // Apply filters if provided
            if (institute_id.HasValue)
            {
                query = query.Where(g => g.institute == institute_id.Value);
            }

            if (program_id.HasValue)
            {
                query = query.Where(g => g.prgm_id == program_id.Value);
            }
            if (student_id.HasValue)
            {
                query = query.Where(g => g.student_id == student_id.Value);
            }

            if (!string.IsNullOrEmpty(participant_type))
                query = query.Where(g => g.participant_type == participant_type);

            if (!string.IsNullOrEmpty(program_type))
                query = query.Where(g => g.program_type == program_type);

            if (ac_year_id.HasValue)
            {
                query = query.Where(g => g.ac_year_id == ac_year_id.Value);
            }
            query = query.OrderBy(g => g.institute_name).ThenBy(g => g.program_name);

            var regList = await query.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Students");

                // Headers
                var headers = new[]
                {
            "Student Name", "Phone Number",
            "Institute", "Program Name", "Item Code", "Program Type",
            "Participant Type", "Q ID", "Gender","No of Group","Group Name",
            "Academic Year"
        };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Rows
                for (int rowIndex = 0; rowIndex < regList.Count; rowIndex++)
                {
                    var s = regList[rowIndex];
                    worksheet.Cell(rowIndex + 2, 1).Value = s.name;
                    worksheet.Cell(rowIndex + 2, 2).Value = s.phone_no;
                    worksheet.Cell(rowIndex + 2, 3).Value = s.institute_name;
                    worksheet.Cell(rowIndex + 2, 4).Value = s.program_name;
                    worksheet.Cell(rowIndex + 2, 5).Value = s.item_code;
                    worksheet.Cell(rowIndex + 2, 6).Value = s.program_type;
                    worksheet.Cell(rowIndex + 2, 7).Value = s.participant_type;
                    worksheet.Cell(rowIndex + 2, 8).Value = s.admsn_no;
                    worksheet.Cell(rowIndex + 2, 9).Value = s.gender;
                    worksheet.Cell(rowIndex + 2, 10).Value = s.no_of_group;
                    worksheet.Cell(rowIndex + 2, 11).Value = s.group_name;
                    worksheet.Cell(rowIndex + 2, 12).Value= s.year
;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"student_registrations.xlsx");
                }
            }
        }

        //

        //delete group members

        [HttpDelete]
        [Route("delete_group_members")]
        public async Task<IActionResult> delete_group_members([FromForm] int prgm_id, [FromForm] int student_id)
        {
            if (_context.tbl_prgm_participants == null)
            {
                return Ok(new { status = false, message = "Entity set 'tbl_prgm_participants' is null." });
            }

            // Fetch program type
            var program = await _context.tbl_program
                .Where(p => p.id == prgm_id)
                .Select(p => new { p.program_type })
                .FirstOrDefaultAsync();

            if (program == null)
                return Ok(new { status = false, message = "Program not found." });

            // ✅ Handle group program
            if (program.program_type.ToLower() == "group")
            {
                // Find if student is in tbl_prgm_participants (main participant)
                var mainParticipant = await _context.tbl_prgm_participants
                    .FirstOrDefaultAsync(p => p.prgm_id == prgm_id && p.student_id == student_id && p.status != "deleted");

                // Find group member details
                var groupMember = await _context.tbl_group_members
                    .FirstOrDefaultAsync(g => g.prgm_id == prgm_id && g.student_id == student_id);

                if (groupMember == null)
                {
                    return Ok(new { status = false, message = "Group member not found." });
                }

                // ✅ If the student is the main participant
                if (mainParticipant != null)
                {
                    // ✅ Only allow delete if program_status = "Pending"
                    if (mainParticipant != null && mainParticipant.program_status?.ToLower() != "pending")
                    {
                        return Ok(new { status = false, message = "Cannot delete. Only participants with Pending status can be removed." });
                    }


                    // Find another student from the same institute and group_name
                    var nextMember = await (from gm in _context.tbl_group_members
                                            join s in _context.tbl_student on gm.student_id equals s.id
                                            where gm.prgm_id == prgm_id
                                                  && gm.group_name == groupMember.group_name
                                                  && s.institute == (from s2 in _context.tbl_student
                                                                     where s2.id == student_id
                                                                     select s2.institute).FirstOrDefault()
                                                  && gm.student_id != student_id
                                            select new { gm.student_id }).FirstOrDefaultAsync();

                    if (nextMember != null)
                    {
                        // ✅ Update new student as main participant in tbl_prgm_participants
                        mainParticipant.student_id = nextMember.student_id;
                        _context.Entry(mainParticipant).State = EntityState.Modified;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // ✅ No replacement found → delete the main participant record
                        _context.tbl_prgm_participants.Remove(mainParticipant);
                        await _context.SaveChangesAsync();
                    }
                }

                // ✅ Remove student from tbl_group_members
                _context.tbl_group_members.Remove(groupMember);
                await _context.SaveChangesAsync();

                return Ok(new { status = true, message = "Student removed from group program successfully." });
            }

            // ✅ Handle non-group programs
            var eventItem = await _context.tbl_prgm_participants
                .FirstOrDefaultAsync(p => p.prgm_id == prgm_id && p.student_id == student_id && p.status != "deleted");

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found." });
            }

            // Check if points have been assigned
            bool pointsExist = await _context.tbl_prgm_point
                .AnyAsync(p => p.student_id == student_id && p.prgm_id == prgm_id);

            if (pointsExist)
            {
                return Ok(new { status = false, message = "Cannot delete since judge has already assigned points to this student for the program." });
            }



            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully." });
        }


        //


        //reports -studentwise results

        [HttpGet]
        [Route("student_wise_results_excel")]
      
        public async Task<IActionResult> student_wise_results_excel(int? institute_id, int? program_id, string? q_id,string? category,string? program_type)
        {

            // Step 1: Build query without student filter inside join
            var query = await (
        from pp in _context.tbl_prgm_participants
        join gm in _context.tbl_group_members
            on new { pp.prgm_id, pp.group_name } equals new { gm.prgm_id, gm.group_name } into gj
        from gm in gj.DefaultIfEmpty()
        join s in _context.tbl_student
            on (gm.student_id.HasValue ? gm.student_id.Value : pp.student_id) equals s.id into sj
        from s in sj.DefaultIfEmpty()
        join i in _context.tbl_institute
            on s.institute equals i.id into ij
        from i in ij.DefaultIfEmpty()
        join p in _context.tbl_program
            on pp.prgm_id equals p.id
        where pp.status == "active" 
        select new
        {
            StudentId = gm.student_id ?? pp.student_id,
            StudentName = s.name,
            q_id = s.admsn_no,
            InstituteId = s.institute,
            InstituteName = i.name,
            ProgramId = pp.prgm_id,
            pp.total_point,
            pp.average_point,
            pp.grade,
            pp.grade_point,
            pp.position,
            pp.position_point,
            p.program_name,
            p.program_type,
            p.item_code,
            p.participant_type
        }
    ).Distinct().ToListAsync();

            // Step 2: Apply filters AFTER the joins
            if (program_id.HasValue)
                query = query.Where(g => g.ProgramId == program_id.Value).ToList();

            if (institute_id.HasValue)
                query = query.Where(g => g.InstituteId == institute_id.Value).ToList();

            if (!string.IsNullOrEmpty(q_id))
                query = query.Where(g => g.q_id == q_id).ToList();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(g => g.participant_type == category).ToList();

            if (!string.IsNullOrEmpty(program_type))
                query = query.Where(g => g.program_type == program_type).ToList();

            if (query == null || query.Count == 0)
            {
                return Ok(new { status = false, message = "No data found!" });
            }

            // Step 3: Create Excel workbook
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Student Results");

                worksheet.Cell(1, 1).Value = "Student Name";
                worksheet.Cell(1, 2).Value = "QID";

                worksheet.Cell(1, 3).Value = "Institute Name";
                worksheet.Cell(1, 4).Value = "Program Name";
                worksheet.Cell(1, 5).Value = "Item Code";

                worksheet.Cell(1, 6).Value = "Program Type";
                worksheet.Cell(1, 7).Value = "Participant Type";
                worksheet.Cell(1, 8).Value = "Grade";
                worksheet.Cell(1, 9).Value = "Grade Point";
                worksheet.Cell(1, 10).Value = "Position";
                worksheet.Cell(1, 11).Value = "Position Point";

                worksheet.Cell(1, 12).Value = "Total Point";
                worksheet.Cell(1, 13).Value = "Average Point";

                int row = 2;
                foreach (var item in query)
                {
                    worksheet.Cell(row, 1).Value = item.StudentName;
                    worksheet.Cell(row, 2).Value = item.q_id;

                    worksheet.Cell(row, 3).Value = item.InstituteName;
                    worksheet.Cell(row, 4).Value = item.program_name;
                    worksheet.Cell(row, 5).Value = item.item_code;

                    worksheet.Cell(row, 6).Value = item.program_type;
                    worksheet.Cell(row, 7).Value = item.participant_type;
                    worksheet.Cell(row, 8).Value = item.grade;
                    worksheet.Cell(row, 9).Value = item.grade_point;
                    worksheet.Cell(row, 10).Value = item.position;
                    worksheet.Cell(row, 11).Value = item.position_point;

                    worksheet.Cell(row, 12).Value = item.total_point;
                    worksheet.Cell(row, 13).Value = item.average_point;
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"student_wise_results.xlsx");
                }
            }
        }



        //


        //reports -studentwise results

        [HttpGet]
        [Route("prgm_wise_results_excel")]

        public async Task<IActionResult> prgm_wise_results_excel(int? institute_id, int? program_id, string? item_code, int? ac_year_id)
        {

            // Step 1: Build query without student filter inside join
            var query = await (
        from pp in _context.tbl_prgm_participants
        join gm in _context.tbl_group_members
            on new { pp.prgm_id, pp.group_name } equals new { gm.prgm_id, gm.group_name } into gj
        from gm in gj.DefaultIfEmpty()
        join s in _context.tbl_student
            on (gm.student_id.HasValue ? gm.student_id.Value : pp.student_id) equals s.id into sj
        from s in sj.DefaultIfEmpty()
        join i in _context.tbl_institute
            on s.institute equals i.id into ij
        from i in ij.DefaultIfEmpty()
        join p in _context.tbl_program
            on pp.prgm_id equals p.id
        where pp.status == "active"
        select new
        {
            StudentId = gm.student_id ?? pp.student_id,
            StudentName = s.name,
            q_id = s.admsn_no,
            InstituteId = s.institute,
            InstituteName = i.name,
            ProgramId = pp.prgm_id,
            pp.total_point,
            pp.average_point,
            p.program_name,
            p.ac_year_id,
            p.item_code,
            p.program_type,
            p.participant_type
        }
    ).Distinct().ToListAsync();

            // Step 2: Apply filters AFTER the joins
            if (program_id.HasValue)
                query = query.Where(g => g.ProgramId == program_id.Value).ToList();

            if (institute_id.HasValue)
                query = query.Where(g => g.InstituteId == institute_id.Value).ToList();

            if (ac_year_id.HasValue)
                query = query.Where(g => g.ac_year_id == ac_year_id.Value).ToList();

            if (!string.IsNullOrEmpty(item_code))
                query = query.Where(g => g.item_code == item_code).ToList();

            if (query == null || query.Count == 0)
            {
                return Ok(new { status = false, message = "No data found!" });
            }

            string? fileItemCode = null; 
            
            if (program_id.HasValue)
            { 
                fileItemCode = query.Select(x => x.item_code).FirstOrDefault();
            }

            string fileName; 
            if (program_id.HasValue && !string.IsNullOrWhiteSpace(fileItemCode)) 
            { 
                fileName = $"program_wise_results_{fileItemCode}.xlsx"; 
            
            } 
            else 
            { 
                fileName = "program_wise_results.xlsx"; 
            }

            // Step 3: Create Excel workbook
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Student Results");

                worksheet.Cell(1, 1).Value = "Student ID";
                worksheet.Cell(1, 2).Value = "Student Name";
                worksheet.Cell(1, 3).Value = "Institute ID";
                worksheet.Cell(1, 4).Value = "Institute Name";
                worksheet.Cell(1, 5).Value = "Program ID";
                worksheet.Cell(1, 6).Value = "Total Point";
                worksheet.Cell(1, 7).Value = "Average Point";
                worksheet.Cell(1, 8).Value = "Program Name";
                worksheet.Cell(1, 9).Value = "Program Type";
                worksheet.Cell(1, 10).Value = "Participant Type";
                worksheet.Cell(1, 11).Value = "QID";
                worksheet.Cell(1, 12).Value = "Item Code";

                int row = 2;
                foreach (var item in query)
                {
                    worksheet.Cell(row, 1).Value = item.StudentId;
                    worksheet.Cell(row, 2).Value = item.StudentName;
                    worksheet.Cell(row, 3).Value = item.InstituteId;
                    worksheet.Cell(row, 4).Value = item.InstituteName;
                    worksheet.Cell(row, 5).Value = item.ProgramId;
                    worksheet.Cell(row, 6).Value = item.total_point;
                    worksheet.Cell(row, 7).Value = item.average_point;
                    worksheet.Cell(row, 8).Value = item.program_name;
                    worksheet.Cell(row, 9).Value = item.program_type;
                    worksheet.Cell(row, 10).Value = item.participant_type;
                    worksheet.Cell(row, 11).Value = item.q_id;
                    worksheet.Cell(row, 12).Value = item.item_code;
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    //return File(stream.ToArray(),
                    //    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    //    $"program_wise_results.xlsx");

                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }



        //


        //
        [HttpGet]
        [Route("get_student_points")]
        public async Task<ActionResult> get_student_points(int program_id)
        {
            var programDetails = await (from p in _context.tbl_program
                                        join st in _context.tbl_stage on p.stage_id equals st.id into stJoin
                                        from stage in stJoin.DefaultIfEmpty()
                                        where p.id == program_id
                                        select new
                                        {
                                            program_name = p.program_name,
                                            program_id = p.id,
                                            stage_name = stage != null ? stage.stage_name : "",
                                            gender = p.gender,
                                            program_type = p.program_type,
                                            participant_type = p.participant_type,
                                            status = p.status,
                                            p.item_code
                                        }).FirstOrDefaultAsync();

            if (programDetails == null)
            {
                return Ok(new { status = false, message = "Program not found" });
            }

            // Fetch judgement marks with participant info
            var rawData = await (from jp in _context.tbl_prgm_point
                                 join s in _context.tbl_student on jp.student_id equals s.id
                                 join i in _context.tbl_institute on s.institute equals i.id into ins
                                 from inst in ins.DefaultIfEmpty()
                                 join j in _context.tbl_judgement_criteria on jp.judgement_criteria_id equals j.id
                                 join jd in _context.tbl_judge on jp.judge_id equals jd.id
                                 join part in _context.tbl_prgm_participants on
                                    new { jp.student_id, jp.prgm_id } equals new { student_id = part.student_id, prgm_id = part.prgm_id }
                                 where jp.prgm_id == program_id
                                 select new
                                 {
                                     jp.student_id,
                                     s.name,
                                     s.institute,
                                     institute_name = inst.name,
                                     part.chess_no,
                                     jp.point,
                                     judge_name = jd.judge_name,
                                     part.average_point,
                                     part.grade,
                                     part.grade_point,
                                     part.position,
                                     part.position_point,
                                     part.total_point
                                 }).ToListAsync();

            if (rawData == null || rawData.Count == 0)
            {
                return Ok(new { status = false, message = "No data found" });
            }

            var grades = await _context.tbl_grade.ToListAsync();

            var grouped = rawData
               .GroupBy(r => new
               {
                   r.student_id,
                   r.name,
                   r.institute,
                   r.institute_name,
                   r.chess_no,
                   r.average_point,
                   r.grade,
                   r.grade_point,
                   r.position,
                   r.position_point,
                   r.total_point
               })
               .Select(g =>
               {
                   var judgementMarks = g
                       .GroupBy(x => x.judge_name)
                       .Select(jg => new
                       {
                           judge_name = jg.Key,
                           points = jg.Sum(y => y.point ?? 0)
                       }).ToList();

                   var grade = g.Key.grade ?? grades
                       .Where(gr => g.Key.average_point >= gr.from_point && g.Key.average_point <= gr.to_point)
                       .Select(gr => gr.grade)
                       .FirstOrDefault() ?? "N/A";

                   string prize = "";
                   if (!string.IsNullOrEmpty(g.Key.position))
                   {
                       prize = g.Key.position + " Prize";
                   }

                   var totalJudgePoint = g.Sum(x => x.point ?? 0);

                   return new
                   {
                       chest_no = g.Key.chess_no,
                       student_name = g.Key.name,
                       institute_name = g.Key.institute_name,
                       judgement_marks = judgementMarks,
                       average_marks = g.Key.average_point,
                       grade = grade,
                       grade_point = g.Key.grade_point,
                       prize = prize,
                       position = g.Key.position,
                       position_point = g.Key.position_point,
                       total_marks = g.Key.total_point,
                       total_judge_point = totalJudgePoint
                   };
               })
               .OrderBy(x => Convert.ToInt32(x.position ?? "9999"))
               .ThenByDescending(x => x.average_marks)
               .ToList();

            return Ok(new
            {
                status = true,
                message = "Success",
                program_id = programDetails.program_id,
                program_name = programDetails.program_name,
                stage_name = programDetails.stage_name,
                gender = programDetails.gender,
                program_type = programDetails.program_type,
                participant_type = programDetails.participant_type,
                programstatus = programDetails.status,
                item_code = programDetails.item_code,
                data = grouped
            });
        }

        //


        //student wise report excel for position 1,2,3
        [HttpGet]
        [Route("student_wise_results_excel_new")]

        //    public async Task<IActionResult> student_wise_results_excel_new(int? institute_id, int? program_id, string? q_id, string? category, string? program_type)
        //    {

        //        // Step 1: Build query without student filter inside join
        //        var query = await (
        //    from pp in _context.tbl_prgm_participants
        //    join gm in _context.tbl_group_members
        //        on new { pp.prgm_id, pp.group_name } equals new { gm.prgm_id, gm.group_name } into gj
        //    from gm in gj.DefaultIfEmpty()
        //    join s in _context.tbl_student
        //        on (gm.student_id.HasValue ? gm.student_id.Value : pp.student_id) equals s.id into sj
        //    from s in sj.DefaultIfEmpty()
        //    join i in _context.tbl_institute
        //        on s.institute equals i.id into ij
        //    from i in ij.DefaultIfEmpty()
        //    join p in _context.tbl_program
        //        on pp.prgm_id equals p.id

        //    join st in _context.tbl_stage
        //   on p.stage_id equals st.id into sta
        //    from stg in sta.DefaultIfEmpty()

        //    where pp.status == "active" &&
        //             (pp.position == "1" || pp.position == "2" || pp.position == "3") && pp.chess_no!=null
        //    orderby pp.prgm_id, Convert.ToInt32(pp.position)

        //    select new
        //    {
        //        StudentId = gm.student_id ?? pp.student_id,
        //        StudentName = s.name,
        //        q_id = s.admsn_no,
        //        InstituteId = s.institute,
        //        InstituteName = i.name,
        //        ProgramId = pp.prgm_id,
        //        stage_id=p.stage_id,
        //        stage=stg.stage_name,
        //        pp.chess_no,
        //        pp.total_point,
        //        pp.average_point,
        //        pp.grade,
        //        pp.grade_point,
        //        pp.position,
        //        pp.position_point,
        //        p.program_name,
        //        p.program_type,
        //        p.item_code,
        //        p.participant_type
        //    }
        //).Distinct().ToListAsync();

        //        // Step 2: Apply filters AFTER the joins
        //        if (program_id.HasValue)
        //            query = query.Where(g => g.ProgramId == program_id.Value).ToList();

        //        if (institute_id.HasValue)
        //            query = query.Where(g => g.InstituteId == institute_id.Value).ToList();

        //        if (!string.IsNullOrEmpty(q_id))
        //            query = query.Where(g => g.q_id == q_id).ToList();

        //        if (!string.IsNullOrEmpty(category))
        //            query = query.Where(g => g.participant_type == category).ToList();

        //        if (!string.IsNullOrEmpty(program_type))
        //            query = query.Where(g => g.program_type == program_type).ToList();

        //        if (query == null || query.Count == 0)
        //        {
        //            return Ok(new { status = false, message = "No data found!" });
        //        }

        //        // Step 3: Create Excel workbook
        //        using (var workbook = new ClosedXML.Excel.XLWorkbook())
        //        {
        //            var worksheet = workbook.Worksheets.Add("Student Results");

        //            worksheet.Cell(1, 1).Value = "Student Name";
        //            worksheet.Cell(1, 2).Value = "QID";

        //            worksheet.Cell(1, 3).Value = "Institute Name";
        //            worksheet.Cell(1, 4).Value = "Program Name";
        //            worksheet.Cell(1, 5).Value = "Stage";

        //            worksheet.Cell(1, 6).Value = "Item Code";

        //            worksheet.Cell(1, 7).Value = "Program Type";
        //            worksheet.Cell(1, 8).Value = "Participant Type";
        //            worksheet.Cell(1, 9).Value = "Grade";
        //            worksheet.Cell(1, 10).Value = "Grade Point";
        //            worksheet.Cell(1, 11).Value = "Position";
        //            worksheet.Cell(1, 12).Value = "Position Point";

        //            worksheet.Cell(1, 13).Value = "Total Point";
        //            worksheet.Cell(1, 14).Value = "Average Point";
        //            worksheet.Cell(1, 15).Value = "Chest Number";

        //            int row = 2;
        //            foreach (var item in query)
        //            {
        //                worksheet.Cell(row, 1).Value = item.StudentName;
        //                worksheet.Cell(row, 2).Value = item.q_id;

        //                worksheet.Cell(row, 3).Value = item.InstituteName;
        //                worksheet.Cell(row, 4).Value = item.program_name;
        //                worksheet.Cell(row, 5).Value = item.stage;

        //                worksheet.Cell(row, 6).Value = item.item_code;

        //                worksheet.Cell(row, 7).Value = item.program_type;
        //                worksheet.Cell(row, 8).Value = item.participant_type;
        //                worksheet.Cell(row, 9).Value = item.grade;
        //                worksheet.Cell(row, 10).Value = item.grade_point;
        //                worksheet.Cell(row, 11).Value = item.position;
        //                worksheet.Cell(row, 12).Value = item.position_point;

        //                worksheet.Cell(row, 13).Value = item.total_point;
        //                worksheet.Cell(row, 14).Value = item.average_point;
        //                worksheet.Cell(row, 15).Value = item.chess_no;

        //                row++;
        //            }

        //            worksheet.Columns().AdjustToContents();

        //            using (var stream = new MemoryStream())
        //            {
        //                workbook.SaveAs(stream);
        //                stream.Position = 0;
        //                return File(stream.ToArray(),
        //                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        //                    $"student_wise_results.xlsx");
        //            }
        //        }
        //    }
        public async Task<IActionResult> student_wise_results_excel_new(int? institute_id, int? program_id, string? q_id, string? category, string? program_type, int? ac_year_id)
        {
            IEnumerable<dynamic> query = Enumerable.Empty<dynamic>();

            // 🔹 Auto-detect program type if not passed
            if (string.IsNullOrEmpty(program_type) && program_id.HasValue)
            {
                program_type = await _context.tbl_program
                    .Where(p => p.id == program_id.Value)
                    .Select(p => p.program_type)
                    .FirstOrDefaultAsync();
            }

            if (string.IsNullOrEmpty(program_type))
            {
                return Ok(new { status = false, message = "Program type not found or not specified (solo/group)!" });
            }

            // 🔹 SOLO PROGRAM QUERY
            if (program_type.ToLower() == "solo")
            {
                var soloQuery = await (
                    from pp in _context.tbl_prgm_participants
                    join s in _context.tbl_student on pp.student_id equals s.id
                    join i in _context.tbl_institute on s.institute equals i.id into ij
                    from i in ij.DefaultIfEmpty()

                    join ac in _context.tbl_academic_year
                        on s.ac_year_id equals ac.id into acj
                    from ac in acj.DefaultIfEmpty()

                    join p in _context.tbl_program on pp.prgm_id equals p.id
                    join st in _context.tbl_stage on p.stage_id equals st.id into sta
                    from stg in sta.DefaultIfEmpty()

                    where pp.status == "active"
                        && (pp.position == "1" || pp.position == "2" || pp.position == "3")
                        && pp.chess_no != null
                        && pp.grade != null
                        && pp.grade_point != null
                        && p.program_type == "solo"
                        && (!ac_year_id.HasValue || s.ac_year_id == ac_year_id.Value)

                    orderby pp.prgm_id, Convert.ToInt32(pp.position)

                    select new
                    {
                        StudentId = pp.student_id,
                        StudentName = s.name,
                        q_id = s.admsn_no,
                        InstituteId = s.institute,
                        InstituteName = i.name,
                        ProgramId = pp.prgm_id,
                        stage_id = p.stage_id,
                        stage = stg.stage_name,
                        chess_no = pp.chess_no,
                        group_name = "",
                        total_point = pp.total_point,
                        average_point = pp.average_point,
                        grade = pp.grade,
                        grade_point = pp.grade_point,
                        position = pp.position,
                        position_point = pp.position_point,
                        program_name = p.program_name,
                        program_type = p.program_type,
                        item_code = p.item_code,
                        participant_type = p.participant_type,
                        ac_year_id = s.ac_year_id,
                        academic_year = ac.year
                    }
                ).Distinct().ToListAsync();

                query = soloQuery;
            }

            // 🔹 GROUP PROGRAM QUERY
            else if (program_type.ToLower() == "group")
            {
                var groupQuery = await (
                    from pp in _context.tbl_prgm_participants

                    join sBase in _context.tbl_student on pp.student_id equals sBase.id

                    join acBase in _context.tbl_academic_year
                        on sBase.ac_year_id equals acBase.id into acBasej
                    from acBase in acBasej.DefaultIfEmpty()

                    join p in _context.tbl_program on pp.prgm_id equals p.id
                    join stg in _context.tbl_stage on p.stage_id equals stg.id

                    where pp.status == "active"
                        && (pp.position == "1" || pp.position == "2" || pp.position == "3")
                        && pp.chess_no != null
                        && pp.grade != null
                        && pp.grade_point != null
                        && p.program_type == "group"
                        && (!ac_year_id.HasValue || sBase.ac_year_id == ac_year_id.Value)

                    select new
                    {
                        prgm_id = pp.prgm_id,
                        p.program_name,
                        p.program_type,
                        p.participant_type,
                        stage = stg.stage_name,
                        pp.group_name,
                        chess_no = pp.chess_no,
                        institute = sBase.institute,
                        pp.grade,
                        pp.grade_point,
                        pp.position,
                        pp.position_point,
                        pp.total_point,
                        pp.average_point,
                        p.item_code,
                        ac_year_id = sBase.ac_year_id,
                        academic_year = acBase.year
                    }

                    into baseInfo

                    join gm in _context.tbl_group_members
                        on new { baseInfo.prgm_id, baseInfo.group_name }
                        equals new { gm.prgm_id, gm.group_name }

                    join s in _context.tbl_student on gm.student_id equals s.id

                    join ac in _context.tbl_academic_year
                        on s.ac_year_id equals ac.id into acj
                    from ac in acj.DefaultIfEmpty()

                    join i in _context.tbl_institute on s.institute equals i.id

                    where s.institute == baseInfo.institute

                    orderby baseInfo.prgm_id,
                            baseInfo.group_name,
                            baseInfo.chess_no,
                            s.name

                    select new
                    {
                        StudentId = s.id,
                        StudentName = s.name,
                        q_id = s.admsn_no,
                        InstituteId = s.institute,
                        InstituteName = i.name,
                        ProgramId = baseInfo.prgm_id,
                        stage_id = (int?)null,
                        stage = baseInfo.stage,
                        chess_no = baseInfo.chess_no,
                        group_name = baseInfo.group_name,
                        total_point = baseInfo.total_point,
                        average_point = baseInfo.average_point,
                        grade = baseInfo.grade,
                        grade_point = baseInfo.grade_point,
                        position = baseInfo.position,
                        position_point = baseInfo.position_point,
                        program_name = baseInfo.program_name,
                        program_type = baseInfo.program_type,
                        item_code = baseInfo.item_code,
                        participant_type = baseInfo.participant_type,
                        ac_year_id = s.ac_year_id,
                        academic_year = ac.year
                    }

                ).Distinct().ToListAsync();

                query = groupQuery;
            }

            else if (program_type.ToLower() == "all")
            {
                var soloQuery = await (
                    from pp in _context.tbl_prgm_participants

                    join s in _context.tbl_student on pp.student_id equals s.id

                    join i in _context.tbl_institute on s.institute equals i.id into ij
                    from i in ij.DefaultIfEmpty()

                    join ac in _context.tbl_academic_year
                        on s.ac_year_id equals ac.id into acj
                    from ac in acj.DefaultIfEmpty()

                    join p in _context.tbl_program on pp.prgm_id equals p.id

                    join st in _context.tbl_stage on p.stage_id equals st.id into sta
                    from stg in sta.DefaultIfEmpty()

                    where pp.status == "active"
                        && (pp.position == "1" || pp.position == "2" || pp.position == "3")
                        && pp.chess_no != null
                        && pp.grade != null
                        && pp.grade_point != null
                        && p.program_type == "solo"
                        && (!ac_year_id.HasValue || s.ac_year_id == ac_year_id.Value)

                    orderby pp.prgm_id, Convert.ToInt32(pp.position)

                    select new
                    {
                        StudentId = (int?)pp.student_id,
                        StudentName = s.name,
                        q_id = s.admsn_no,
                        InstituteId = (int?)s.institute,
                        InstituteName = i.name,
                        ProgramId = (int?)pp.prgm_id,
                        stage_id = (int?)p.stage_id,
                        stage = stg.stage_name,
                        chess_no = pp.chess_no,
                        group_name = "",
                        total_point = pp.total_point,
                        average_point = pp.average_point,
                        grade = pp.grade,
                        grade_point = pp.grade_point,
                        position = pp.position,
                        position_point = pp.position_point,
                        program_name = p.program_name,
                        program_type = p.program_type,
                        item_code = p.item_code,
                        participant_type = p.participant_type,
                        ac_year_id = s.ac_year_id,
                        academic_year = ac.year
                    }

                ).Distinct().ToListAsync();

                var groupQuery = await (
                    from pp in _context.tbl_prgm_participants

                    join sBase in _context.tbl_student on pp.student_id equals sBase.id

                    join acBase in _context.tbl_academic_year
                        on sBase.ac_year_id equals acBase.id into acBasej
                    from acBase in acBasej.DefaultIfEmpty()

                    join p in _context.tbl_program on pp.prgm_id equals p.id
                    join stg in _context.tbl_stage on p.stage_id equals stg.id

                    where pp.status == "active"
                        && (pp.position == "1" || pp.position == "2" || pp.position == "3")
                        && pp.chess_no != null
                        && pp.grade != null
                        && pp.grade_point != null
                        && p.program_type == "group"
                        && (!ac_year_id.HasValue || sBase.ac_year_id == ac_year_id.Value)

                    select new
                    {
                        prgm_id = pp.prgm_id,
                        stage_id = p.stage_id,
                        p.program_name,
                        p.program_type,
                        p.participant_type,
                        stage = stg.stage_name,
                        pp.group_name,
                        chess_no = pp.chess_no,
                        institute = sBase.institute,
                        pp.grade,
                        pp.grade_point,
                        pp.position,
                        pp.position_point,
                        pp.total_point,
                        pp.average_point,
                        p.item_code,
                        ac_year_id = sBase.ac_year_id,
                        academic_year = acBase.year
                    }

                    into baseInfo

                    join gm in _context.tbl_group_members
                        on new { baseInfo.prgm_id, baseInfo.group_name }
                        equals new { gm.prgm_id, gm.group_name }

                    join s in _context.tbl_student on gm.student_id equals s.id

                    join ac in _context.tbl_academic_year
                        on s.ac_year_id equals ac.id into acj
                    from ac in acj.DefaultIfEmpty()

                    join i in _context.tbl_institute on s.institute equals i.id

                    where s.institute == baseInfo.institute

                    orderby baseInfo.prgm_id,
                            baseInfo.group_name,
                            baseInfo.chess_no,
                            s.name

                    select new
                    {
                        StudentId = (int?)s.id,
                        StudentName = s.name,
                        q_id = s.admsn_no,
                        InstituteId = (int?)s.institute,
                        InstituteName = i.name,
                        ProgramId = (int?)baseInfo.prgm_id,
                        stage_id = (int?)baseInfo.stage_id,
                        stage = baseInfo.stage,
                        chess_no = baseInfo.chess_no,
                        group_name = baseInfo.group_name,
                        total_point = baseInfo.total_point,
                        average_point = baseInfo.average_point,
                        grade = baseInfo.grade,
                        grade_point = baseInfo.grade_point,
                        position = baseInfo.position,
                        position_point = baseInfo.position_point,
                        program_name = baseInfo.program_name,
                        program_type = baseInfo.program_type,
                        item_code = baseInfo.item_code,
                        participant_type = baseInfo.participant_type,
                        ac_year_id = s.ac_year_id,
                        academic_year = ac.year
                    }

                ).Distinct().ToListAsync();

                query = soloQuery.Concat(groupQuery).ToList();
            }

            else
            {
                return Ok(new { status = false, message = "Invalid program type!" });
            }

            // 🔹 Apply filters AFTER join
            if (program_id.HasValue)
                query = query.Where(g => g.ProgramId == program_id.Value).ToList();

            if (institute_id.HasValue)
                query = query.Where(g => g.InstituteId == institute_id.Value).ToList();

            if (!string.IsNullOrEmpty(q_id))
                query = query.Where(g => g.q_id == q_id).ToList();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(g => g.participant_type == category).ToList();

            if (!query.Any())
                return Ok(new { status = false, message = "No data found!" });

            // 🔹 Helper for safe string conversion
            string Safe(object val) => val?.ToString() ?? "";

            // 🔹 Create Excel
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Student Results");

                string[] headers = new string[]
                {
            "Student Name",
            "QID",
            "Institute Name",
            "Program Name",
            "Stage",
            "Item Code",
            "Program Type",
            "Participant Type",
            "Grade",
            "Grade Point",
            "Position",
            "Position Point",
            "Total Point",
            "Average Point",
            "Chest Number",
            "Group Name",
            "Academic Year"
                };

                for (int col = 0; col < headers.Length; col++)
                    worksheet.Cell(1, col + 1).Value = headers[col];

                int row = 2;

                foreach (var item in query)
                {
                    worksheet.Cell(row, 1).Value = Safe(item.StudentName);
                    worksheet.Cell(row, 2).Value = Safe(item.q_id);
                    worksheet.Cell(row, 3).Value = Safe(item.InstituteName);
                    worksheet.Cell(row, 4).Value = Safe(item.program_name);
                    worksheet.Cell(row, 5).Value = Safe(item.stage);
                    worksheet.Cell(row, 6).Value = Safe(item.item_code);
                    worksheet.Cell(row, 7).Value = Safe(item.program_type);
                    worksheet.Cell(row, 8).Value = Safe(item.participant_type);
                    worksheet.Cell(row, 9).Value = Safe(item.grade);
                    worksheet.Cell(row, 10).Value = Safe(item.grade_point);
                    worksheet.Cell(row, 11).Value = Safe(item.position);
                    worksheet.Cell(row, 12).Value = Safe(item.position_point);
                    worksheet.Cell(row, 13).Value = Safe(item.total_point);
                    worksheet.Cell(row, 14).Value = Safe(item.average_point);
                    worksheet.Cell(row, 15).Value = Safe(item.chess_no);
                    worksheet.Cell(row, 16).Value = Safe(item.group_name);
                    worksheet.Cell(row, 17).Value = Safe(item.academic_year);

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    return File(
                        stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"student_wise_results.xlsx"
                    );
                }
            }
        }
        //

        //results excel group,solo
//        [HttpGet]
//        [Route("result_excel")]

//        public async Task<IActionResult> result_excel(int? institute_id, int? program_id, string? q_id, string? category, string? program_type,int?ac_year_id)
//        {
//            IEnumerable<dynamic> query = Enumerable.Empty<dynamic>();

//            // 🔹 Auto-detect program type if not passed
//            if (string.IsNullOrEmpty(program_type) && program_id.HasValue)
//            {
//                program_type = await _context.tbl_program
//                    .Where(p => p.id == program_id.Value)
//                    .Select(p => p.program_type)
//                    .FirstOrDefaultAsync();
//            }

//            if (string.IsNullOrEmpty(program_type))
//                return Ok(new { status = false, message = "Program type not found or not specified (solo/group/all)!" });

//            program_type = program_type.ToLower();

//            // =====================================
//            // 🔹 SOLO PROGRAMS
//            // =====================================
//            var soloQuery = await (
//       from parti in _context.tbl_prgm_participants
//       join prg in _context.tbl_program on parti.prgm_id equals prg.id
//       join stg in _context.tbl_stage on prg.stage_id equals stg.id into stageJoin
//       from stg in stageJoin.DefaultIfEmpty()
//       join s in _context.tbl_student on parti.student_id equals s.id
//       join i in _context.tbl_institute on s.institute equals i.id

//       join ac in _context.tbl_academic_year
//           on s.ac_year_id equals ac.id into acj
//       from ac in acj.DefaultIfEmpty()
//       where prg.program_type == "Solo" && (parti.position == "1" || parti.position == "2" || parti.position == "3")
//       select new
//       {
//           StudentId = s.id,
//           StudentName = s.name,
//           q_id = s.admsn_no,
//           InstituteId = s.institute,
//           InstituteName = i.name,
//           ProgramId = prg.id,
//           stage = stg != null ? stg.stage_name : "",
//           chest_no = parti.chess_no,
//           group_name = "",
//           total_point = parti.total_point,
//           average_point = parti.average_point,
//           grade = parti.grade,
//           grade_point = parti.grade_point,
//           position = parti.position,
//           position_point = parti.position_point,
//           program_name = prg.program_name,
//           program_type = prg.program_type,
//           item_code = prg.item_code,
//           participant_type = prg.participant_type,
//           ac_year_id = s.ac_year_id,
//           academic_year = ac.year,
//           // ✅ Subquery for solo_count
//           solo_count = _context.tbl_prgm_participants
//               .Where(p => p.student_id == s.id)
//               .Count(),

//           // ✅ Subquery for grand_total (Solo)
//           grand_total = _context.tbl_prgm_participants
//               .Join(_context.tbl_program, a => a.prgm_id, b => b.id, (a, b) => new { a, b })
//               .Where(x => x.a.student_id == s.id
//                        && x.b.program_type == "Solo"
//                        && (x.a.position == "1" || x.a.position == "2" || x.a.position == "3"))
//               .Sum(x => (decimal?)x.a.total_point) ?? 0
//       }
//   ).ToListAsync();

//            // =====================================
//            // 🔹 GROUP PROGRAMS
//            var groupQuery = await (
//        from pp in _context.tbl_prgm_participants
//        join sBase in _context.tbl_student on pp.student_id equals sBase.id
//        join p in _context.tbl_program on pp.prgm_id equals p.id
//        join stg in _context.tbl_stage on p.stage_id equals stg.id
//        join ac in _context.tbl_academic_year
//    on sBase.ac_year_id equals ac.id into acj
//        from ac in acj.DefaultIfEmpty()
//        where pp.status == "active"
//            && (pp.position == "1" || pp.position == "2" || pp.position == "3")
//            && pp.chess_no != null
//            && p.program_type == "Group"
//        select new
//        {
//            prgm_id = pp.prgm_id,
//            p.program_name,
//            p.program_type,
//            p.participant_type,
//            stage = stg.stage_name,
//            pp.group_name,
//            chess_no = pp.chess_no,
//            institute = sBase.institute,
//            pp.grade,
//            pp.grade_point,
//            pp.position,
//            pp.position_point,
//            pp.total_point,
//            pp.average_point,
//            p.item_code,
//            StudentId = sBase.id,
//            ac_year_id = sBase.ac_year_id,
//            academic_year = ac.year,
//            // 👇 Subquery for grand total like your SQL version
//            grand_total = _context.tbl_prgm_participants
//                .Join(_context.tbl_program, a => a.prgm_id, b => b.id, (a, b) => new { a, b })
//                .Where(x => x.a.student_id == sBase.id
//                         && x.b.program_type == "Group"
//                         && (x.a.position == "1" || x.a.position == "2" || x.a.position == "3"))
//                .Sum(x => (decimal?)x.a.total_point) ?? 0
//        } into baseInfo
//        join gm in _context.tbl_group_members
//            on new { baseInfo.prgm_id, baseInfo.group_name } equals new { gm.prgm_id, gm.group_name }
//        join s in _context.tbl_student on gm.student_id equals s.id
//        join i in _context.tbl_institute on s.institute equals i.id
//        where s.institute == baseInfo.institute
//        orderby baseInfo.prgm_id, baseInfo.group_name, baseInfo.chess_no, s.name
//        select new
//        {
//            StudentId = (int?)null,
//            StudentName = (string)null,
//            q_id = (string)null,
//            InstituteId = s.institute,
//            InstituteName = i.name,
//            ProgramId = baseInfo.prgm_id,
//            stage = baseInfo.stage,
//            chest_no = baseInfo.chess_no,
//            group_name = baseInfo.group_name,
//            total_point = baseInfo.total_point,
//            average_point = baseInfo.average_point,
//            grade = baseInfo.grade,
//            grade_point = baseInfo.grade_point,
//            position = baseInfo.position,
//            position_point = baseInfo.position_point,
//            program_name = baseInfo.program_name,
//            program_type = baseInfo.program_type,
//            item_code = baseInfo.item_code,
//            participant_type = baseInfo.participant_type,
//            grand_total = baseInfo.grand_total,// ✅ added
//            ac_year_id = baseInfo.ac_year_id,
//    academic_year = baseInfo.academic_year
//        }
//    ).Distinct().ToListAsync();


//            // =====================================
//            // 🔹 Combine Based on program_type
//            // =====================================
//            if (program_type == "solo")
//                query = soloQuery;
//            else if (program_type == "group")
//                query = groupQuery;
//            else if (program_type == "all")
//            {
//                // ✅ Convert both to ExpandoObject (common dynamic type)
//                var unifiedSolo = soloQuery.Select(x =>
//                {
//                    IDictionary<string, object> exp = new System.Dynamic.ExpandoObject();
//                    exp["StudentId"] = x.StudentId;
//                    exp["StudentName"] = x.StudentName;
//                    exp["q_id"] = x.q_id;
//                    exp["InstituteId"] = x.InstituteId;
//                    exp["InstituteName"] = x.InstituteName;
//                    exp["ProgramId"] = x.ProgramId;
//                    exp["stage"] = x.stage;
//                    exp["chest_no"] = x.chest_no;
//                    exp["group_name"] = x.group_name;
//                    exp["total_point"] = x.total_point;
//                    exp["average_point"] = x.average_point;
//                    exp["grade"] = x.grade;
//                    exp["grade_point"] = x.grade_point;
//                    exp["position"] = x.position;
//                    exp["position_point"] = x.position_point;
//                    exp["program_name"] = x.program_name;
//                    exp["program_type"] = x.program_type;
//                    exp["item_code"] = x.item_code;
//                    exp["participant_type"] = x.participant_type;
//                    exp["solo_count"] = x.solo_count;        // ✅ include solo_count
//                    exp["grand_total"] = x.grand_total;      // ✅ include grand_total
//                    exp["ac_year_id"] = x.ac_year_id;
//                    exp["academic_year"] = x.academic_year;
//                    return (dynamic)exp;
//                });

//                var unifiedGroup = groupQuery.Select(x =>
//                {
//                    IDictionary<string, object> exp = new System.Dynamic.ExpandoObject();
//                    exp["StudentId"] = x.StudentId;
//                    exp["StudentName"] = x.StudentName;
//                    exp["q_id"] = x.q_id;
//                    exp["InstituteId"] = x.InstituteId;
//                    exp["InstituteName"] = x.InstituteName;
//                    exp["ProgramId"] = x.ProgramId;
//                    exp["stage"] = x.stage;
//                    exp["chest_no"] = x.chest_no;
//                    exp["group_name"] = x.group_name;
//                    exp["total_point"] = x.total_point;
//                    exp["average_point"] = x.average_point;
//                    exp["grade"] = x.grade;
//                    exp["grade_point"] = x.grade_point;
//                    exp["position"] = x.position;
//                    exp["position_point"] = x.position_point;
//                    exp["program_name"] = x.program_name;
//                    exp["program_type"] = x.program_type;
//                    exp["item_code"] = x.item_code;
//                    exp["participant_type"] = x.participant_type;
//                    exp["solo_count"] = 0;                   //  group programs don't have solo_count
//                    exp["grand_total"] = x.grand_total;      //  include grand_total for group
//                    exp["ac_year_id"] = x.ac_year_id;
//                    exp["academic_year"] = x.academic_year;
//                    return (dynamic)exp;
//                });

//                query = unifiedSolo.Concat(unifiedGroup).ToList(); //  Safe concat

//            }
//            else
//                return Ok(new { status = false, message = "Invalid program type!" });

//            // =====================================
//            // 🔹 Apply Filters
//            // =====================================
//            if (program_id.HasValue)
//                query = query.Where(g => g.ProgramId == program_id.Value).ToList();

//            if (institute_id.HasValue)
//                query = query.Where(g => g.InstituteId == institute_id.Value).ToList();

//            if (!string.IsNullOrEmpty(q_id))
//                query = query.Where(g => g.q_id == q_id).ToList();

//            if (!string.IsNullOrEmpty(category))
//                query = query.Where(g => g.participant_type == category).ToList();

//            if (!query.Any())
//                return Ok(new { status = false, message = "No data found!" });
//            if (ac_year_id.HasValue)
//                query = query.Where(g => g.ac_year_id == ac_year_id.Value).ToList();
//            // =====================================
//            // 🔹 Excel Export
//            // =====================================
//            string Safe(object val) => val?.ToString() ?? "";

//            using (var workbook = new ClosedXML.Excel.XLWorkbook())
//            {
//                var worksheet = workbook.Worksheets.Add("Student Results");

//                string[] headers = new string[]
//                {
//            "Student Name", "QID", "Institute Name", "Program Name", "Stage",
//            "Item Code", "Program Type", "Participant Type", "Grade", "Grade Point",
//"Position", "Position Point", "Total Point", "Average Point",
//"Chest Number", "Group Name", "Grand Total", "Academic Year"                };

//                for (int col = 0; col < headers.Length; col++)
//                    worksheet.Cell(1, col + 1).Value = headers[col];

//                int row = 2;
//                foreach (var item in query)
//                {
//                    worksheet.Cell(row, 1).Value = Safe(item.StudentName);
//                    worksheet.Cell(row, 2).Value = Safe(item.q_id);
//                    worksheet.Cell(row, 3).Value = Safe(item.InstituteName);
//                    worksheet.Cell(row, 4).Value = Safe(item.program_name);
//                    worksheet.Cell(row, 5).Value = Safe(item.stage);
//                    worksheet.Cell(row, 6).Value = Safe(item.item_code);
//                    worksheet.Cell(row, 7).Value = Safe(item.program_type);
//                    worksheet.Cell(row, 8).Value = Safe(item.participant_type);
//                    worksheet.Cell(row, 9).Value = Safe(item.grade);
//                    worksheet.Cell(row, 10).Value = Safe(item.grade_point);
//                    worksheet.Cell(row, 11).Value = Safe(item.position);
//                    worksheet.Cell(row, 12).Value = Safe(item.position_point);
//                    worksheet.Cell(row, 13).Value = Safe(item.total_point);
//                    worksheet.Cell(row, 14).Value = Safe(item.average_point);
//                    worksheet.Cell(row, 15).Value = Safe(item.chest_no);
//                    worksheet.Cell(row, 16).Value = Safe(item.group_name);
//                    worksheet.Cell(row, 17).Value = Safe(item.grand_total);
//                    worksheet.Cell(row, 18).Value = Safe(item.academic_year);

//                    row++;
//                }

//                worksheet.Columns().AdjustToContents();

//                using (var stream = new MemoryStream())
//                {
//                    workbook.SaveAs(stream);
//                    stream.Position = 0;
//                    return File(stream.ToArray(),
//                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
//                        $"student_wise_results.xlsx");
//                }
//            }
//        }





        [HttpGet]
        [Route("result_excel")]

        public async Task<IActionResult> result_excel(int? institute_id, int? program_id, string? q_id, string? category, string? program_type, int? ac_year_id,string?item_code)
        {
            IEnumerable<dynamic> query = Enumerable.Empty<dynamic>();

            // 🔹 Auto-detect program type if not passed
            if (string.IsNullOrEmpty(program_type) && program_id.HasValue)
            {
                program_type = await _context.tbl_program
                    .Where(p => p.id == program_id.Value)
                    .Select(p => p.program_type)
                    .FirstOrDefaultAsync();
            }

            if (string.IsNullOrEmpty(program_type))
                return Ok(new { status = false, message = "Program type not found or not specified (solo/group/all)!" });

            program_type = program_type.ToLower();

            // =====================================
            // 🔹 SOLO PROGRAMS
            // =====================================
            var soloQuery = await (
       from parti in _context.tbl_prgm_participants
       join prg in _context.tbl_program on parti.prgm_id equals prg.id
       join stg in _context.tbl_stage on prg.stage_id equals stg.id into stageJoin
       from stg in stageJoin.DefaultIfEmpty()
       join s in _context.tbl_student on parti.student_id equals s.id
       join i in _context.tbl_institute on s.institute equals i.id

       join ac in _context.tbl_academic_year
           on s.ac_year_id equals ac.id into acj
       from ac in acj.DefaultIfEmpty()
       where prg.program_type == "Solo" && (parti.position == "1" || parti.position == "2" || parti.position == "3")
       select new
       {
           StudentId = s.id,
           StudentName = s.name,
           q_id = s.admsn_no,
           InstituteId = s.institute,
           InstituteName = i.name,
           ProgramId = prg.id,
           stage = stg != null ? stg.stage_name : "",
           chest_no = parti.chess_no,
           group_name = "",
           total_point = parti.total_point,
           average_point = parti.average_point,
           grade = parti.grade,
           grade_point = parti.grade_point,
           position = parti.position,
           position_point = parti.position_point,
           program_name = prg.program_name,
           program_type = prg.program_type,
           item_code = prg.item_code,
           participant_type = prg.participant_type,
           ac_year_id = s.ac_year_id,
           academic_year = ac.year,
           
           // ✅ Subquery for solo_count
           solo_count = _context.tbl_prgm_participants
               .Where(p => p.student_id == s.id)
               .Count(),

           // ✅ Subquery for grand_total (Solo)
           grand_total = _context.tbl_prgm_participants
               .Join(_context.tbl_program, a => a.prgm_id, b => b.id, (a, b) => new { a, b })
               .Where(x => x.a.student_id == s.id
                        && x.b.program_type == "Solo"
                        && (x.a.position == "1" || x.a.position == "2" || x.a.position == "3"))
               .Sum(x => (decimal?)x.a.total_point) ?? 0
       }
   ).ToListAsync();

            // =====================================
            // 🔹 GROUP PROGRAMS
            var groupQuery = await (
        from pp in _context.tbl_prgm_participants
        join sBase in _context.tbl_student on pp.student_id equals sBase.id
        join p in _context.tbl_program on pp.prgm_id equals p.id
        join stg in _context.tbl_stage on p.stage_id equals stg.id
        join ac in _context.tbl_academic_year
    on sBase.ac_year_id equals ac.id into acj
        from ac in acj.DefaultIfEmpty()
        where pp.status == "active"
            && (pp.position == "1" || pp.position == "2" || pp.position == "3")
            && pp.chess_no != null
            && p.program_type == "Group"
        select new
        {
            prgm_id = pp.prgm_id,
            p.program_name,
            p.program_type,
            p.participant_type,
            stage = stg.stage_name,
            pp.group_name,
            chess_no = pp.chess_no,
            institute = sBase.institute,
            pp.grade,
            pp.grade_point,
            pp.position,
            pp.position_point,
            pp.total_point,
            pp.average_point,
            p.item_code,
            StudentId = sBase.id,
            ac_year_id = sBase.ac_year_id,
            academic_year = ac.year,
            // 👇 Subquery for grand total like your SQL version
            grand_total = _context.tbl_prgm_participants
                .Join(_context.tbl_program, a => a.prgm_id, b => b.id, (a, b) => new { a, b })
                .Where(x => x.a.student_id == sBase.id
                         && x.b.program_type == "Group"
                         && (x.a.position == "1" || x.a.position == "2" || x.a.position == "3"))
                .Sum(x => (decimal?)x.a.total_point) ?? 0
        } into baseInfo
        join gm in _context.tbl_group_members
            on new { baseInfo.prgm_id, baseInfo.group_name } equals new { gm.prgm_id, gm.group_name }
        join s in _context.tbl_student on gm.student_id equals s.id
        join i in _context.tbl_institute on s.institute equals i.id
        where s.institute == baseInfo.institute
        orderby baseInfo.prgm_id, baseInfo.group_name, baseInfo.chess_no, s.name
        select new
        {
            StudentId = (int?)null,
            StudentName = (string)null,
            q_id = (string)null,
            InstituteId = s.institute,
            InstituteName = i.name,
            ProgramId = baseInfo.prgm_id,
            stage = baseInfo.stage,
            chest_no = baseInfo.chess_no,
            group_name = baseInfo.group_name,
            total_point = baseInfo.total_point,
            average_point = baseInfo.average_point,
            grade = baseInfo.grade,
            grade_point = baseInfo.grade_point,
            position = baseInfo.position,
            position_point = baseInfo.position_point,
            program_name = baseInfo.program_name,
            program_type = baseInfo.program_type,
            item_code = baseInfo.item_code,
            participant_type = baseInfo.participant_type,
            grand_total = baseInfo.grand_total,// ✅ added
            ac_year_id = baseInfo.ac_year_id,
            academic_year = baseInfo.academic_year
        }
    ).Distinct().ToListAsync();


            // =====================================
            // 🔹 Combine Based on program_type
            // =====================================
            if (program_type == "solo")
                query = soloQuery;
            else if (program_type == "group")
                query = groupQuery;
            else if (program_type == "all")
            {
                // ✅ Convert both to ExpandoObject (common dynamic type)
                var unifiedSolo = soloQuery.Select(x =>
                {
                    IDictionary<string, object> exp = new System.Dynamic.ExpandoObject();
                    exp["StudentId"] = x.StudentId;
                    exp["StudentName"] = x.StudentName;
                    exp["q_id"] = x.q_id;
                    exp["InstituteId"] = x.InstituteId;
                    exp["InstituteName"] = x.InstituteName;
                    exp["ProgramId"] = x.ProgramId;
                    exp["stage"] = x.stage;
                    exp["chest_no"] = x.chest_no;
                    exp["group_name"] = x.group_name;
                    exp["total_point"] = x.total_point;
                    exp["average_point"] = x.average_point;
                    exp["grade"] = x.grade;
                    exp["grade_point"] = x.grade_point;
                    exp["position"] = x.position;
                    exp["position_point"] = x.position_point;
                    exp["program_name"] = x.program_name;
                    exp["program_type"] = x.program_type;
                    exp["item_code"] = x.item_code;
                    exp["participant_type"] = x.participant_type;
                    exp["solo_count"] = x.solo_count;        // ✅ include solo_count
                    exp["grand_total"] = x.grand_total;      // ✅ include grand_total
                    exp["ac_year_id"] = x.ac_year_id;
                    exp["academic_year"] = x.academic_year;
                    return (dynamic)exp;
                });

                var unifiedGroup = groupQuery.Select(x =>
                {
                    IDictionary<string, object> exp = new System.Dynamic.ExpandoObject();
                    exp["StudentId"] = x.StudentId;
                    exp["StudentName"] = x.StudentName;
                    exp["q_id"] = x.q_id;
                    exp["InstituteId"] = x.InstituteId;
                    exp["InstituteName"] = x.InstituteName;
                    exp["ProgramId"] = x.ProgramId;
                    exp["stage"] = x.stage;
                    exp["chest_no"] = x.chest_no;
                    exp["group_name"] = x.group_name;
                    exp["total_point"] = x.total_point;
                    exp["average_point"] = x.average_point;
                    exp["grade"] = x.grade;
                    exp["grade_point"] = x.grade_point;
                    exp["position"] = x.position;
                    exp["position_point"] = x.position_point;
                    exp["program_name"] = x.program_name;
                    exp["program_type"] = x.program_type;
                    exp["item_code"] = x.item_code;
                    exp["participant_type"] = x.participant_type;
                    exp["solo_count"] = 0;                   //  group programs don't have solo_count
                    exp["grand_total"] = x.grand_total;      //  include grand_total for group
                    exp["ac_year_id"] = x.ac_year_id;
                    exp["academic_year"] = x.academic_year;
                    return (dynamic)exp;
                });

                query = unifiedSolo.Concat(unifiedGroup).ToList(); //  Safe concat

            }
            else
                return Ok(new { status = false, message = "Invalid program type!" });

            // =====================================
            // 🔹 Apply Filters
            // =====================================
            if (program_id.HasValue)
                query = query.Where(g => g.ProgramId == program_id.Value).ToList();

            if (institute_id.HasValue)
                query = query.Where(g => g.InstituteId == institute_id.Value).ToList();

            if (!string.IsNullOrEmpty(q_id))
                query = query.Where(g => g.q_id == q_id).ToList();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(g => g.participant_type == category).ToList();

            if (ac_year_id.HasValue)
                query = query.Where(g => g.ac_year_id == ac_year_id.Value).ToList();

            if (!string.IsNullOrEmpty(item_code))
                query = query.Where(g => g.item_code == item_code).ToList();

            if (!query.Any())
                return Ok(new { status = false, message = "No data found!" });
         
            // =====================================
            // 🔹 Excel Export
            // =====================================
            string Safe(object val) => val?.ToString() ?? "";

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Student Results");

                string[] headers = new string[]
                {
            "Student Name",  "Institute Name","QID", "Program Name", "Stage",
            "Item Code", "Program Type", "Participant Type", "Grade", "Grade Point",
"Position", "Position Point", "Total Point", "Average Point",
"Chest Number", "Group Name", "Grand Total", "Academic Year"                };

                for (int col = 0; col < headers.Length; col++)
                    worksheet.Cell(1, col + 1).Value = headers[col];

                int row = 2;
                foreach (var item in query)
                {
                    worksheet.Cell(row, 1).Value = Safe(item.StudentName);
                    worksheet.Cell(row, 2).Value = Safe(item.InstituteName);
                    worksheet.Cell(row, 3).Value = Safe(item.q_id);
                    worksheet.Cell(row, 4).Value = Safe(item.program_name);
                    worksheet.Cell(row, 6).Value = Safe(item.item_code);
                    worksheet.Cell(row, 7).Value = Safe(item.program_type);
                    worksheet.Cell(row, 8).Value = Safe(item.participant_type);
                    worksheet.Cell(row, 11).Value = Safe(item.position);
                    worksheet.Cell(row, 15).Value = Safe(item.chest_no);
                    worksheet.Cell(row, 18).Value = Safe(item.academic_year);
                    worksheet.Column(5).Hide();
                    worksheet.Column(9).Hide();
                    worksheet.Column(10).Hide();
                    worksheet.Column(12).Hide();;
                    worksheet.Column(13).Hide();
                    worksheet.Column(14).Hide();
                    worksheet.Column(16).Hide();
                    worksheet.Column(17).Hide();
                    //worksheet.Cell(row, 5).Value = Safe(item.stage);

                    //worksheet.Cell(row, 9).Value = Safe(item.grade);
                    //worksheet.Cell(row, 10).Value = Safe(item.grade_point);
                    //worksheet.Cell(row, 12).Value = Safe(item.position_point);
                    //worksheet.Cell(row, 13).Value = Safe(item.total_point);
                    //worksheet.Cell(row, 14).Value = Safe(item.average_point);
                    //worksheet.Cell(row, 16).Value = Safe(item.group_name);
                    //worksheet.Cell(row, 17).Value = Safe(item.grand_total);

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"student_wise_results.xlsx");
                }
            }
        }






        //

        //group result

        [HttpGet]
        [Route("group_prgm_result")]
        public async Task<IActionResult> group_prgm_result(int? institute_id, int? program_id, string? q_id, string? category, string? program_type,int?ac_year_id)
        {
            IEnumerable<dynamic> query = Enumerable.Empty<dynamic>();

          
            // 🔹 Fetch Group Program Results
            var groupQuery = await (
                from pp in _context.tbl_prgm_participants
                join sBase in _context.tbl_student on pp.student_id equals sBase.id
                join p in _context.tbl_program on pp.prgm_id equals p.id
                join stg in _context.tbl_stage on p.stage_id equals stg.id
                where pp.status == "active"
                    && pp.chess_no != null && pp.position=="1" || pp.position == "2" || pp.position == "3"      
                    && p.program_type == "group"
                select new
                {
                    prgm_id = pp.prgm_id,
                    p.program_name,
                    p.program_type,
                    p.participant_type,
                    stage = stg.stage_name,
                    pp.group_name,
                    pp.chess_no,
                    sBase.institute,
                    pp.grade,
                    pp.grade_point,
                    pp.position,
                    pp.position_point,
                    pp.total_point,
                    pp.average_point,
                    p.item_code
                } into baseInfo
                join gm in _context.tbl_group_members
                    on new { baseInfo.prgm_id, baseInfo.group_name } equals new { gm.prgm_id, gm.group_name }
                join s in _context.tbl_student on gm.student_id equals s.id
                join i in _context.tbl_institute on s.institute equals i.id
                join ac in _context.tbl_academic_year
    on s.ac_year_id equals ac.id into acj
                from ac in acj.DefaultIfEmpty()
                where s.institute == baseInfo.institute
                      && (string.IsNullOrEmpty(q_id) || s.admsn_no == q_id) // 🔹 dynamic filter
                orderby baseInfo.prgm_id, baseInfo.group_name, baseInfo.chess_no, s.name
                select new
                {
                    StudentId = s.id,
                    StudentName = s.name,
                    q_id = s.admsn_no,
                    InstituteId = s.institute,
                    InstituteName = i.name,
                    ProgramId = baseInfo.prgm_id,
                    baseInfo.stage,
                    baseInfo.chess_no,
                    baseInfo.group_name,
                    baseInfo.total_point,
                    baseInfo.average_point,
                    baseInfo.grade,
                    baseInfo.grade_point,
                    baseInfo.position,
                    baseInfo.position_point,
                    baseInfo.program_name,
                    baseInfo.program_type,
                    baseInfo.item_code,
                    baseInfo.participant_type,
                    ac_year_id = s.ac_year_id,
                    academic_year = ac.year
                }
            ).Distinct().ToListAsync();

            // 🔹 Assign to query (so your filters apply properly)
            query = groupQuery;

         

            // 🔹 Apply filters
            if (program_id.HasValue)
                query = query.Where(g => g.ProgramId == program_id.Value).ToList();

            if (institute_id.HasValue)
                query = query.Where(g => g.InstituteId == institute_id.Value).ToList();

            if (!string.IsNullOrEmpty(q_id))
                query = query.Where(g => g.q_id == q_id).ToList();

            if (!string.IsNullOrEmpty(category))
                query = query.Where(g => g.participant_type == category).ToList();

            if (!query.Any())
                return Ok(new { status = false, message = "No data found!" });
            if (ac_year_id.HasValue)
                query = query.Where(g => g.ac_year_id == ac_year_id.Value).ToList();
            // =====================================
            // 🔹 Excel Export
            // =====================================
            string Safe(object val) => val?.ToString() ?? "";

            string? fileItemCode = null;

            if (program_id.HasValue)
            {
                fileItemCode = query
                    .Select(x => x.item_code)
                    .FirstOrDefault();
            }
            string fileName;

            if (program_id.HasValue && !string.IsNullOrWhiteSpace(fileItemCode))
            {
                fileName = $"group_program_results_{fileItemCode}.xlsx";
            }
            else
            {
                fileName = "group_program_results.xlsx";
            }

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Student Results");

                string[] headers = new string[]
                {
            "Student Name", "QID", "Institute Name", "Program Name", "Stage",
            "Item Code", "Program Type", "Participant Type", "Grade", "Grade Point",
"Position", "Position Point", "Total Point", "Average Point",
"Chest Number", "Group Name", "Academic Year"                };

                for (int col = 0; col < headers.Length; col++)
                    worksheet.Cell(1, col + 1).Value = headers[col];

                int row = 2;
                foreach (var item in query)
                {
                    worksheet.Cell(row, 1).Value = Safe(item.StudentName);
                    worksheet.Cell(row, 2).Value = Safe(item.q_id);
                    worksheet.Cell(row, 3).Value = Safe(item.InstituteName);
                    worksheet.Cell(row, 4).Value = Safe(item.program_name);
                    worksheet.Cell(row, 5).Value = Safe(item.stage);
                    worksheet.Cell(row, 6).Value = Safe(item.item_code);
                    worksheet.Cell(row, 7).Value = Safe(item.program_type);
                    worksheet.Cell(row, 8).Value = Safe(item.participant_type);
                    worksheet.Cell(row, 9).Value = Safe(item.grade);
                    worksheet.Cell(row, 10).Value = Safe(item.grade_point);
                    worksheet.Cell(row, 11).Value = Safe(item.position);
                    worksheet.Cell(row, 12).Value = Safe(item.position_point);
                    worksheet.Cell(row, 13).Value = Safe(item.total_point);
                    worksheet.Cell(row, 14).Value = Safe(item.average_point);
                    worksheet.Cell(row, 15).Value = Safe(item.chess_no);
                    worksheet.Cell(row, 16).Value = Safe(item.group_name);
                    worksheet.Cell(row, 17).Value = Safe(item.academic_year);
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    //return File(stream.ToArray(),
                    //    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    //    $"group_program_results.xlsx");

                    return File(
          stream.ToArray(),
          "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
          fileName);
                }
            }
        }

        //
    }
}
