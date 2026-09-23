using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using FirebaseAdmin.Messaging;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Reflection.Metadata.Ecma335;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class judgesController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public judgesController(kalanjaliDbContext context)
        {
            _context = context;
        }


        [HttpPost]
        [Route("Postjudges")]
        public async Task<ActionResult> Postjudges([FromBody] judgemodel request)
        {
            if (_context.tbl_judge == null || _context.tbl_judge == null)
            {
                return Problem("Entity set '_context.tbl_judge' or '_context.tbl_judge' is null.");
            }

            var existingStudent = await _context.tbl_user
               .FirstOrDefaultAsync(s => s.phone_no == request.phone_no && s.password == request.password);

            if (existingStudent != null)
            {
                return Ok(new { status = false, message = "username and password already exists." });
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


            var division = new judgemodel
            {
                judge_name = request.judge_name,
                phone_no = request.phone_no,
                password = request.password,

                status = request.status ?? "active", 
                event_id = request.event_id,
                added_by = request.added_by,
                addedtype = request.addedtype,
                addedon = DateTime.Now,
            };

            _context.tbl_judge.Add(division);
            await _context.SaveChangesAsync(); 

            var user = new usermodel
            {
                type = "judge",
                name=division.judge_name,
                phone_no = division.phone_no,
                username=division.phone_no,
                password = division.password,
                addedon = DateTime.Now,
                status = "active",
                added_by = division.added_by,
                addedtype = division.addedtype,
                reference_id = division.id,
            };

            _context.tbl_user.Add(user);
            await _context.SaveChangesAsync();

            if (division.id == null || division.id <= 0)
            {
                return BadRequest(new { status = false, message = "Failed to insert judges program." });
            }

          

            return Ok(new { status = true, message = "Data added successfully", id = division.id });
        }


        [HttpPut]
        [Route("update_judges")]
        public async Task<ActionResult> update_judges([FromBody] judgemodel request)
        {
            var data = await _context.tbl_judge.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "Record not found" });
            }

            // Update tbl_judge fields
            if (!string.IsNullOrEmpty(request.judge_name)) data.judge_name = request.judge_name;
            if (!string.IsNullOrEmpty(request.phone_no)) data.phone_no = request.phone_no;

            if (!string.IsNullOrEmpty(request.password)) data.password = request.password;
            if (request.event_id.HasValue) data.event_id = request.event_id.Value;
            if (!string.IsNullOrEmpty(request.modified_type)) data.modified_type = request.modified_type;
            if (request.modified_by.HasValue) data.modified_by = request.modified_by.Value;
            data.modifiedon = DateTime.Now;

            // Update related tbl_user record
            var user = await _context.tbl_user
                .Where(u => u.reference_id == request.id && u.type == "judge")
                .FirstOrDefaultAsync();

            if (user != null)
            {
               // if (!string.IsNullOrEmpty(request.judge_name)) user.name = request.judge_name;
                if (!string.IsNullOrEmpty(request.phone_no)) user.phone_no = request.phone_no;
                if (!string.IsNullOrEmpty(request.phone_no)) user.username = request.phone_no;

                if (!string.IsNullOrEmpty(request.password)) user.password = request.password;
                if (request.modified_by.HasValue) data.modified_by = request.modified_by.Value;
                if (!string.IsNullOrEmpty(request.modified_type)) data.modified_type = request.modified_type;
                data.modifiedon = DateTime.Now;


            }

            // Save all changes once
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }



        [HttpGet]
        [Route("get_judges")]
        public async Task<ActionResult> get_judges(int? id)
        {
            if (_context.tbl_judge == null)
            {
                return NotFound(new { status = false, message = "Data not found" });
            }

            var query = from p in _context.tbl_judge
                        join e in _context.tbl_event on p.event_id equals e.id into evv
                        from ev in evv.DefaultIfEmpty()
                        where p.status == "active"
                        select new
                        {
                            p.id,
                            p.judge_name,
                            p.phone_no,
                            p.password,
                            p.event_id,
                            event_name = ev.event_name,
                            p.added_by,
                            p.addedtype,
                            p.addedon,

                            // Subarray for judge_programs
                            judge_programs = (from jp in _context.tbl_judge_prgm
                                              join p in _context.tbl_program on jp.program_id equals p.id into pr
                                              from prg in pr.DefaultIfEmpty()
                                              where jp.judge_id == p.id && jp.status == "active"

                                              select new
                                              {
                                                  jp.id,
                                                  jp.judge_id,
                                                  jp.program_id,
                                                  prg.program_name 
                                                 
                                              }).ToList()
                        };

            if (id.HasValue)
            {
                query = query.Where(g => g.id == id.Value);
            }

        
            var result = await query.ToListAsync();

            if (result == null || !result.Any())
            {
                return NotFound(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }

        [HttpDelete]
        [Route("Delete_judges")]
        public async Task<IActionResult> Delete_judges([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_judge == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_user' is null.");
            }

            var eventItem = await _context.tbl_judge.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Soft delete the program
            eventItem.status = "deleted";
            eventItem.deleted_by = deleted_by;
            eventItem.deleted_type = deleted_type;
            eventItem.deletedon = DateTime.Now;

            _context.Entry(eventItem).State = EntityState.Modified;


            var judge_prgms = _context.tbl_judge_prgm.Where(c => c.judge_id == id);
            _context.tbl_judge_prgm.RemoveRange(judge_prgms);


            var user = _context.tbl_user.Where(c => c.reference_id == id && c.type.ToLower() == "judge");
            //_context.tbl_user.RemoveRange(user);
            foreach (var usr in user)
            {
                usr.status = "deleted";
                usr.deleted_by = deleted_by;
                usr.deletedon = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }


        [HttpPost]
        [Route("add_judge_prgm")]
        public async Task<ActionResult> add_judge_prgm([FromBody] List<judges_prgmmodel> requestList)
        {
            if (_context.tbl_judge_prgm == null)
            {
                return Problem("Entity set '_context.tbl_judge_prgm' is null.");
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

            var now = DateTime.Now;
            var duplicateList = new List<object>();
            var newEntries = new List<judges_prgmmodel>();

            foreach (var request in requestList)
            {
                bool exists = await _context.tbl_judge_prgm
                    .AnyAsync(x => x.judge_id == request.judge_id && x.program_id == request.program_id && x.status!= "deleted");

                if (exists)
                {
                    duplicateList.Add(new
                    {
                        judge_id = request.judge_id,
                        program_id = request.program_id,
                        message = "Judge already assigned to this program"
                    });
                }
                else
                {
                    newEntries.Add(new judges_prgmmodel
                    {
                        judge_id = request.judge_id,
                        program_id = request.program_id,
                        addedon = now,
                        status = "active",
                        program_status = "pending"
                    });
                }
            }

            if (newEntries.Any())
            {
                _context.tbl_judge_prgm.AddRange(newEntries);
                await _context.SaveChangesAsync();
            }

            if (duplicateList.Any())
            {
                return Ok(new
                {
                    status = false,
                    message = "Judges were already added for the same program.",
                    duplicates = duplicateList
                });
            }

            return Ok(new { status = true, message = "Data added successfully." });
        }


        [HttpPut]
        [Route("update_judge_prgm")]
        public async Task<ActionResult> update_judge_prgm([FromBody] List<judges_prgmmodel> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return Ok(new { status = false, message = "No data provided for update" });
            }

            // ✅ Check for duplicates within the incoming list itself
            var duplicateInputPairs = requests
                .GroupBy(x => new { x.judge_id, x.program_id })
                .Where(g => g.Count() > 1)
                .Select(g => new { g.Key.judge_id, g.Key.program_id })
                .ToList();

            if (duplicateInputPairs.Any())
            {
                return Ok(new
                {
                    status = false,
                    message = "Duplicate judge program pairs found in the list.",
                    duplicates = duplicateInputPairs
                });
            }

            var judgeIds = requests.Select(r => r.judge_id).Distinct().ToList();

            // ✅ Fetch existing records for those judges
            var existingRecords = _context.tbl_judge_prgm
                                          .Where(x => judgeIds.Contains(x.judge_id))
                                          .ToList();

            // Remove old assignments for these judges
            if (existingRecords.Any())
            {
                _context.tbl_judge_prgm.RemoveRange(existingRecords);
                await _context.SaveChangesAsync();
            }

            // ✅ Check for duplicates against remaining data in DB (after removal)
            var existingPairs = await _context.tbl_judge_prgm
                .Select(x => new { x.judge_id, x.program_id })
                .ToListAsync();

            var duplicatesInDb = requests
                .Where(r => existingPairs.Any(e => e.judge_id == r.judge_id && e.program_id == r.program_id))
                .Select(r => new { r.judge_id, r.program_id })
                .ToList();

            if (duplicatesInDb.Any())
            {
                return Ok(new
                {
                    status = false,
                    message = "Judge program pairs already exist",
                    duplicates = duplicatesInDb
                });
            }

            // ✅ Add new valid entries
            var now = DateTime.Now;
            foreach (var item in requests)
            {
                item.status = "active";
                item.program_status = "pending";        
                item.addedon = now;
            }

            _context.tbl_judge_prgm.AddRange(requests);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("get_judge_prgm")]
        public async Task<ActionResult> get_judge_prgm(int? judge_id,int?ac_year_id)
        {
            if (_context.tbl_judge == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var query = from j in _context.tbl_judge
                        join d in _context.tbl_judge_prgm.Where(x => x.status == "active")
                            on j.id equals d.judge_id into judgePrg
                        from d in judgePrg.DefaultIfEmpty()
                        join p in _context.tbl_program
                            on d.program_id equals p.id into prg
                        from pr in prg.DefaultIfEmpty()
                        select new
                        {
                            id = d != null ? d.id : 0,
                            judge_id = j.id,
                            judge_name = j.judge_name,
                            program_id = d != null ? d.program_id : (int?)null,
                            program_name = pr != null ? pr.program_name : null,
                            item_code = pr != null ? pr.item_code : null,
                            program_type = pr != null ? pr.program_type : null,
                            participant_type = pr != null ? pr.participant_type : null,   
                            program_date = pr != null ? pr.date : (DateTime?)null,      
                            time = pr != null ? pr.time : null,         
                            addedon = d != null ? d.addedon : null,
                            phone_no = j.phone_no,
                            pr.ac_year_id
                        };

            if (judge_id.HasValue)
            {
                query = query.Where(g => g.judge_id == judge_id.Value);
            }
            if (ac_year_id.HasValue)
            {
                query = query.Where(g => g.ac_year_id == ac_year_id.Value);
            }

            var result = await query.ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }


        //[HttpDelete]
        //[Route("Delete_judge_prgm")]
        //public async Task<IActionResult> Delete_judge_prgm([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        //{
        //    if (_context.tbl_judge_prgm == null)
        //    {
        //        return Problem("Entity set 'AeDbContext.tbl_judge_prgm' is null.");
        //    }

        //    var eventItem = await _context.tbl_judge_prgm.FindAsync(id);



        //    //if added points not able to delete

        //    if (eventItem == null)
        //    {
        //        return Ok(new { status = false, message = "Data not found" });
        //    }

        //    //  Check if the judge has added marks for this program
        //    bool marksExist = await _context.tbl_prgm_point
        //        .AnyAsync(p => p.judge_id == eventItem.judge_id && p.prgm_id == eventItem.program_id);

        //    if (marksExist)
        //    {
        //        return Ok(new { status = false, message = "Cannot delete. Judge has already added marks for this program." });
        //    }


        //    eventItem.status = "deleted";


        //    _context.Entry(eventItem).State = EntityState.Modified;

        //    await _context.SaveChangesAsync();

        //    return Ok(new { status = true, message = "Data deleted successfully" });
        //}

        [HttpDelete]
        [Route("Delete_judge_prgm")]
        public async Task<IActionResult> Delete_judge_prgm([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_judge_prgm == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_judge_prgm' is null.");
            }

            var eventItem = await _context.tbl_judge_prgm.FindAsync(id);
            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Check if the judge has added marks for this program
            bool marksExist = await _context.tbl_prgm_point
                .AnyAsync(p => p.judge_id == eventItem.judge_id && p.prgm_id == eventItem.program_id);

            if (marksExist)
            {
                return Ok(new { status = false, message = "Cannot delete. Judge has already added marks for this program." });
            }

            // Get program status
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == eventItem.program_id);

            if (program == null)
            {
                return Ok(new { status = false, message = "Program not found." });
            }

            if (program.status == "pending")
            {
                // If program is pending → permanently delete
                _context.tbl_judge_prgm.Remove(eventItem);
                await _context.SaveChangesAsync();
                return Ok(new { status = true, message = "Data deleted successfully" });
            }
            else
            {
                // Otherwise → soft delete
                eventItem.status = "deleted";
                _context.Entry(eventItem).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return Ok(new { status = true, message = "Data deleted successfully" });
            }
        }


        //judge login get_judgement_marksheet
        [HttpGet]
        [Route("get_judgement_marksheet")]
        public async Task<ActionResult> get_judgement_marksheet(int? program_id)
        {

            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower() == "judge");

            int ref_id = user?.reference_id ?? 0;
            if (_context.tbl_program == null)
            {
                return NotFound(new { status = false, message = "Data not found" });
            }

            if (!program_id.HasValue)
            {
                return BadRequest(new { status = false, message = "Please provide program_id" });
            }

            // Get program and stage details
            var programDetails = await (from p in _context.tbl_program
                                        join s in _context.tbl_stage on p.stage_id equals s.id into st
                                        from stg in st.DefaultIfEmpty()
                                        where p.id == program_id.Value && p.delete_status == "active"
                                        select new
                                        {
                                            p.id,
                                            p.program_name,
                                            p.stage_id,
                                            stage_name = stg.stage_name,
                                            p.participant_type,
                                            p.program_type,
                                            p.time,
                                            p.item_code,
                                            p.gender
                                        }).FirstOrDefaultAsync();

            if (programDetails == null)
            {
                return NotFound(new { status = false, message = "Program not found" });
            }

            // Get judgement marksheet
            //var marksheet = await (from p in _context.tbl_prgm_participants
            //                       join s in _context.tbl_judge_prgm on p.prgm_id equals s.program_id into st
            //                       from stg in st.DefaultIfEmpty()

            //                       where p.prgm_id == program_id.Value  && stg.judge_id == ref_id
            //                       orderby p.position   //  Order by position ascending

            //                       select new
            //                       {
            //                           p.chess_no,
            //                           p.average_point,
            //                           p.grade,
            //                           p.total_point,
            //                           p.position,
            //                           stg.program_status
            //                       }).ToListAsync();

            var marksheetQuery = from p in _context.tbl_prgm_participants
                                 join s in _context.tbl_judge_prgm on p.prgm_id equals s.program_id into st
                                 from stg in st.DefaultIfEmpty()
                                 where p.prgm_id == program_id.Value && stg.judge_id == ref_id && p.status != "deleted" && !string.IsNullOrEmpty(p.chess_no)

                                 select new
                                 {
                                     p.chess_no,
                                     p.average_point,
                                     p.grade,
                                     p.total_point,
                                     p.position,
                                     stg.program_status
                                 };

            var marksheetRaw = await marksheetQuery.ToListAsync();

            // Deduplicate by chess_no + position (keep first program_status)
            var marksheet = marksheetRaw
                .GroupBy(m => new { m.chess_no, m.position })
                .Select(g => g.First())
                .OrderBy(m => int.TryParse(m.position, out var pos) ? pos : int.MaxValue)
                .ToList();




            return Ok(new
            {
                status = true,
                message = "Success",
                data = new
                {
                    program = programDetails,
                    marksheet = marksheet
                }
            });
        }

        // 

        //tabulation manager & chief program manager
        [HttpGet]
        [Route("marksheet_view")]
        public async Task<ActionResult> marksheet_view(int? program_id)
        {
            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower() == "tabulation_manager" || u.type.ToLower() == "chief_program_manager");
            int ref_id = user?.id ?? 0;

            if (_context.tbl_program == null)
                return NotFound(new { status = false, message = "Data not found" });

            if (!program_id.HasValue)
                return BadRequest(new { status = false, message = "Please provide program_id" });

            // Get program and stage details
            var programDetails = await (from p in _context.tbl_program
                                        join s in _context.tbl_stage on p.stage_id equals s.id into st
                                        from stg in st.DefaultIfEmpty()
                                        where p.id == program_id.Value && p.delete_status == "active" 
                                        select new
                                        {
                                            p.id,
                                            p.program_name,
                                            p.stage_id,
                                            stage_name = stg.stage_name,
                                            p.participant_type,
                                            p.program_type,
                                            p.time,
                                            p.item_code,
                                            p.gender
                                        }).FirstOrDefaultAsync();

            if (programDetails == null)
                return NotFound(new { status = false, message = "Program not found" });

            // Get all participant details
            var participantData = await _context.tbl_prgm_participants
                                        .Where(p => p.prgm_id == program_id && p.status!="deleted" && p.chess_no != null && p.chess_no != "" &&p.ispresent.ToLower()!= "absent")
                                        .Select(p => new
                                        {
                                            p.chess_no,
                                            p.position,
                                            p.average_point,
                                            p.grade,
                                            p.total_point
                                        })
                                        .ToListAsync();

            // Get judgement marksheet
            var marksheet = await (from p in _context.tbl_prgm_point
                                   join j in _context.tbl_judge on p.judge_id equals j.id
                                   where p.prgm_id == program_id
                                   group p by new { p.chess_no, j.judge_name } into g
                                   select new
                                   {
                                       chess_no = g.Key.chess_no,
                                       judge_name = g.Key.judge_name,
                                       total_points = g.Sum(x => x.point)
                                   }).ToListAsync();

            // Combine everything
            var result = participantData.Select(participant => new
            {
                chess_no = participant.chess_no,
                position = participant.position,
                marksheet = marksheet
                                .Where(m => m.chess_no == participant.chess_no)
                                .Select(m => new
                                {
                                    m.judge_name,
                                    m.total_points
                                }).ToList(),
                average_point = participant.average_point,
                grade = participant.grade,
                total_point = participant.total_point

            })
         //.OrderBy(p => p.position)   //  Order by position ascending
         .OrderBy(p => Convert.ToInt32(p.position))

         .ToList();

            return Ok(new
            {
                status = true,
                message = "Success",
                program_details = programDetails,  

                data = result
            });
        }

        //



        //update status as judge approved in app judge login
        [HttpPut]
        [Route("approve_judge_marks")]
        public async Task<ActionResult> approve_judge_marks([FromForm] int? program_id, [FromForm] int? judge_id, [FromForm] string? status)
        {
            if (program_id == null)
                return BadRequest(new { status = false, message = "program_id is required" });

            if (string.IsNullOrEmpty(status))
                return BadRequest(new { status = false, message = "Status is required" });

            // 🔹 Update judge program status
            var judgeProgram = await _context.tbl_judge_prgm
                .FirstOrDefaultAsync(p => p.program_id == program_id && p.judge_id == judge_id && p.status.ToLower() == "active");

            if (judgeProgram == null)
                return Ok(new { status = false, message = "Judge record not found or already updated" });

            judgeProgram.program_status = status;
            judgeProgram.status_updated = DateTime.Now;

            await _context.SaveChangesAsync();

            bool allApproved = await _context.tbl_judge_prgm
                .Where(p => p.program_id == program_id && p.status.ToLower() == "active")
                .AllAsync(p => p.program_status.ToLower() == "approved");

            if (allApproved)
            {
                var program = await _context.tbl_program
                    .FirstOrDefaultAsync(p => p.id == program_id);

                if (program != null)
                {
                    program.status = "Judges Approved";
                    program.status_updated = DateTime.Now;
                    _context.Entry(program).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    var recipients = await (from u in _context.tbl_user
                                            where u.type == "tabulation_manager" && !string.IsNullOrEmpty(u.fcm)
                                            select u.fcm).ToListAsync();

                    foreach (var fcmToken in recipients)
                    {
                        try
                        {
                            var message = new Message
                            {
                                Notification = new Notification
                                {
                                    Title = "Judges Approved",
                                    Body = $"All judges have approved the marks for program '{program.program_name}' (Item Code: {program.item_code}). The tabulation team can now proceed."
                                },
                                Data = new Dictionary<string, string>
                    {
                        { "program_id", program_id.ToString() },
                        { "status", "Judges Approved" }
                    },
                                Token = fcmToken
                            };

                            await FirebaseMessaging.DefaultInstance.SendAsync(message);
                        }
                        catch (Exception ex)
                        {
                       //     return Ok(new { status = true, message = "notification failed", error = ex.Message });
                        }
                    }
                }
            }

            return Ok(new
            {
                status = true,
                message = "Status updated successfully"
            });
        }

        //result approval from judges
        [HttpGet]
        [Route("result_approval_from_judges")]
        public async Task<ActionResult> result_approval_from_judges()
        {
            var enc_key = Request.Headers["XapiKey"].ToString();

            var user = _context.tbl_user
                  .Where(u => u.enc_key == enc_key)
                  .FirstOrDefault();

            int ref_id = 0;
            if (user != null)
            {
                ref_id = (int)user.reference_id;
            }

            try
            {
                //var total_programs = await _context.tbl_program
                //  .Where(tbl =>tbl.delete_status=="active" && tbl.status.ToLower()== "Tabulation Team Approved")
                //  .CountAsync();

                var total_programs = await (from p in _context.tbl_program
                                            join j in _context.tbl_judge_prgm
                                                on p.id equals j.program_id into pj
                                            from j in pj.DefaultIfEmpty() // LEFT JOIN
                                            where p.delete_status == "active" && j.judge_id == ref_id  && j.status == "active"
                                               && p.status.ToLower() == "Tabulation Team Approved"
                                            select p
                           ).CountAsync();


                //var pending = await _context.tbl_judge_prgm
                //  .Where(tbl => tbl.program_status != "approved" && tbl.status == "active" && tbl.judge_id == ref_id)
                //  .CountAsync();

                var pending = await _context.tbl_judge_prgm
    .Where(jp => jp.program_status != "approved"
                 && jp.status == "active"
                 && jp.judge_id == ref_id
                 && _context.tbl_program.Any(p => p.id == jp.program_id
                                               && p.delete_status == "active"
                                               && p.status.ToLower() == "Tabulation Team Approved"))
    .CountAsync();



                //var approved = await _context.tbl_judge_prgm
                //    .Where(tbl => tbl.status == "active" && tbl.program_status=="approved" && tbl.judge_id==ref_id)
                //    .CountAsync();


                var approved = await _context.tbl_judge_prgm
    .Where(jp => jp.status == "active"
                 && jp.program_status == "approved"
                 && jp.judge_id == ref_id
                 && _context.tbl_program.Any(p => p.id == jp.program_id
                                               && p.delete_status == "active"
                                               && p.status.ToLower() == "Tabulation Team Approved"))
    .CountAsync();


                return Ok(new
                {
                    status = true,
                    data = new
                    {
                        total_programs,
                        pending,
                        approved
                      
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "An error occurred while fetching program schedule.",
                    error = ex.Message
                });
            }
        }


        //

        //get list of judges approved in app
        [HttpGet]
        [Route("judge_approval_list")]
        public async Task<ActionResult> judge_approval_list(string? status,DateTime? date)
        {
            var enc_key = Request.Headers["XapiKey"].ToString();

            var user = _context.tbl_user
                  .Where(u => u.enc_key == enc_key)
                  .FirstOrDefault();

            if (user == null)
            {
                return Ok(new { status = false, message = "Invalid user" });
            }

            int ref_id = (int)user.reference_id;

            var query = from j in _context.tbl_judge_prgm
                        join p in _context.tbl_program on j.program_id equals p.id
                        join st in _context.tbl_stage on p.stage_id equals st.id into stageJoin
                        from s in stageJoin.DefaultIfEmpty()
                        where j.judge_id == ref_id && j.status == "active"    
                        select new
                        {
                            j.program_id,
                            p.program_name,
                            p.stage_id,
                            stage_name = s.stage_name,
                            p.time,
                            p.date,
                            p.participant_type,
                            p.program_type,
                            p.status,
                            j.program_status,
                            p.item_code,
                            p.gender
                        };

            // ✅ Apply filter based on passed status
            if (!string.IsNullOrEmpty(status))
            {
                string statusLower = status.Trim().ToLower();
                if (statusLower == "pending")
                {
                    query = query.Where(x => x.program_status.ToLower() == "pending" && x.status=="Tabulation Team Approved");
                }
                else if (statusLower == "approved")
                {
                    query = query.Where(x => x.program_status != null && x.program_status.ToLower() == "approved");
                }
            }

            if (date.HasValue)
                query = query.Where(g => g.date == date);

            var result = await query.ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "No programs found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }

        //


        //judges approved list in tabulation manager
        [HttpGet]
        [Route("judges_approved_list")]
        public async Task<ActionResult> judges_approved_list(string? status, DateTime? date,int?stage_id)
        {
          

            var enc_key = Request.Headers["XapiKey"].ToString();

            var user = _context.tbl_user
                  .FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower() == "tabulation_manager");

            if (user == null)
            {
                return Ok(new { status = false, message = "Invalid user" });
            }

            int ref_id = user.id; 

            var judges_approved = await _context.tbl_program
                .Where(tbl => tbl.delete_status == "active" && tbl.status.ToLower() == "Judges Approved")
                .CountAsync();

            var query = from p in _context.tbl_program
                        join st in _context.tbl_stage on p.stage_id equals st.id into stageJoin
                        from s in stageJoin.DefaultIfEmpty()
                        where p.delete_status == "active" && (date == null || p.date == date) && (stage_id == null || p.stage_id == stage_id)
                        select new
                        {
                            p.id,
                            p.program_name,
                            p.stage_id,
                            stage_name = s.stage_name,
                            p.program_type,
                            p.time,
                            p.date,
                            p.participant_type,
                            p.status,
                            p.item_code,p.gender
                        };

            // ✅ Apply filter based on passed status
            if (!string.IsNullOrEmpty(status))
            {
                string statusLower = status.Trim().ToLower();
                if (statusLower == "pending")
                {
                    query = query.Where(x => x.status == null || x.status.ToLower() == "Judges Approved");
                }
                else if (statusLower == "approved")
                {
                    query = query.Where(x => x.status != null && x.status.ToLower() == "Tabulation Manager Approved" || x.status.ToLower()== "Transmitted to Announcement Team" || x.status.ToLower() == "result published");
                }
            }

            var result = await query.ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "No programs found" });
            }

            return Ok(new
            {
                status = true,
                message = "Success",
                judges_approved = judges_approved,
              
                data = result
            });
        }

        //

        //tabulation manager approved list in chief program manager
        [HttpGet]
        [Route("tm_approved_list")]
        public async Task<ActionResult> tm_approved_list(string? status, DateTime? date,int? stage_id)
        {


            var enc_key = Request.Headers["XapiKey"].ToString();

            var user = _context.tbl_user
                  .FirstOrDefault(u => u.enc_key == enc_key);

            if (user == null)
            {
                return Ok(new { status = false, message = "Invalid user" });
            }

            int ref_id = user.id; // No need to cast to (int) since it's already int

          
            var query = from p in _context.tbl_program
                        join st in _context.tbl_stage on p.stage_id equals st.id into stageJoin
                        from s in stageJoin.DefaultIfEmpty()
                        where p.delete_status == "active" && (date == null || p.date == date) && (stage_id == null || p.stage_id==stage_id)
                        select new
                        {
                            p.id,
                            p.program_name,
                            p.stage_id,
                            stage_name = s.stage_name,
                            p.time,
                            p.date,
                            p.participant_type,
                            p.program_type,
                            p.status,
                            p.item_code,
                            p.gender

                        };

            // ✅ Apply filter based on passed status
            if (!string.IsNullOrEmpty(status))
            {
                string statusLower = status.Trim().ToLower();
                if (statusLower == "pending")
                {
                    query = query.Where(x => x.status == null || x.status.ToLower() == "tabulation manager approved");
                }
                else if (statusLower == "approved")
                {
                    query = query.Where(x =>
                        x.status != null &&
                        (
                            x.status.ToLower() == "transmitted to announcement team" ||
                            x.status.ToLower() == "result published" ||
                            x.status.ToLower() == "transferred to media"
                        )
                    );
                }
            }

            var result = await query.ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "No programs found" });
            }

            return Ok(new
            {
                status = true,
                message = "Success",

                data = result

            });
        }
        //
        [HttpGet]
        [Route("judges_prgrm_list")]
        public async Task<ActionResult> judges_prgrm_list(int?prgrm_id)
        {
            var roles = await (from jp in _context.tbl_judge_prgm
                               join j in _context.tbl_judge on jp.judge_id equals j.id
                               where jp.program_id==prgrm_id 
                               //&&j.ac_year_id== ac_year_id
                               select new
                               {
                               jp.program_id,
                               jp.judge_id,
                               j.judge_name,
                               }).ToListAsync();
        
            return Ok(new { status = true, data = roles });


        }

        //announcement team judgement pending
        [HttpGet]
        [Route("judgement_pending")]
        public async Task<ActionResult> judgement_pending(int? prgrm_id)
        {
            if (prgrm_id == null)
            {
                return Ok(new { status = false, message = "Program ID is required." });
            }

            // ✅ Get all judges assigned to this program
            var judgeList = await (from jp in _context.tbl_judge_prgm
                                   join j in _context.tbl_judge on jp.judge_id equals j.id
                                   where jp.program_id == prgrm_id && jp.status != "deleted"
                                   select new
                                   {
                                       jp.judge_id,
                                       j.judge_name
                                   }).ToListAsync();

            if (judgeList.Count == 0)
            {
                return Ok(new { status = false, message = "No judges found for this program." });
            }

            // ✅ Get all valid participant chess numbers
            var chessNos = await _context.tbl_prgm_participants
                                .Where(p => p.prgm_id == prgrm_id && p.status != "deleted" && p.chess_no != null && p.chess_no != "")
                                .Select(p => p.chess_no)
                                .Distinct()
                                .ToListAsync();

            int totalChessCount = chessNos.Count;

            // ✅ Build status list per judge
            var judgeStatusList = new List<object>();

            foreach (var judge in judgeList)
            {
                // Count how many participants this judge has marked
                var markedChessCount = await _context.tbl_prgm_point
                                            .Where(p => p.prgm_id == prgrm_id
                                                        && p.judge_id == judge.judge_id
                                                        && p.chess_no != null && p.chess_no != "")
                                            .Select(p => p.chess_no)
                                            .Distinct()
                                            .CountAsync();

                string status = markedChessCount == totalChessCount
                    ? "Judgement Completed"
                    : "Judgement Pending";

                judgeStatusList.Add(new
                {
                    judge_name = judge.judge_name,
                    judge_id = judge.judge_id,
                    status
                   
                });
            }

            var summary = new
            {
                judges = judgeStatusList
            };

            return Ok(new
            {
                status = true,
                message = "Success",
                data = summary
            });
        }

        //


    }
}
