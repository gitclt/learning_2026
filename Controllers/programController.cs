using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using FirebaseAdmin.Messaging;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.AccessControl;
using vkc_apinotification.Controllers;
using static kalanjali_api.Controllers.studentController;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class programController : ControllerBase
    {
        private readonly ILogger<programController> _logger;
        private readonly kalanjaliDbContext _context;
        private readonly MessageController _messageController;
        private static readonly object _chestLock = new object();
        public programController(
            kalanjaliDbContext context,
            MessageController messageController,
            ILogger<programController> logger)   // inject logger
        {
            _context = context;
            _messageController = messageController;
            _logger = logger;
        }


        [HttpPost]
        [Route("Postprogram")]
        public async Task<ActionResult> Postprogram([FromBody] programmodel request)
        {

            if (_context.tbl_program == null || _context.tbl_program == null)
            {
                return Problem("Entity set '_context.tbl_program' or '_context.tbl_program' is null.");
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

            //// Validate program date falls within event's from_date and to_date
            //var eventDateRange = await _context.tbl_event
            //    .Where(e => e.id == request.event_id)
            //    .Select(e => new { e.from_date, e.to_date })
            //    .FirstOrDefaultAsync();

            //if (eventDateRange == null)
            //{
            //    return Ok(new { status = false, message = "Invalid event ID." });
            //}

            //if (request.date < eventDateRange.from_date || request.date > eventDateRange.to_date)
            //{
            //    return Ok(new
            //    {
            //        status = false,
            //        message = $"Program date must be between {eventDateRange.from_date:yyyy-MM-dd} and {eventDateRange.to_date:yyyy-MM-dd}."
            //    });
            //}



            // Check for existing program at same date and time
            var isConflict = await _context.tbl_program
                .AnyAsync(p => p.date == request.date && p.time == request.time && p.stage_id == request.stage_id && p.delete_status != "deleted");

            if (isConflict)
            {
                return Ok(new
                {
                    status = false,
                    message = "A program is already scheduled at the same date and time."
                });
            }




            var division = new programmodel
            {
                // institute_id = request.institute_id,

                stage_id = request.stage_id,
                //  event_id = request.event_id,
                program_name = request.program_name,
                date = request.date,
                time = request.time,
                color_code = request.color_code,
                group_min_participants = request.group_min_participants,
                group_max_participants = request.group_max_participants,
                addedon = DateTime.Now,

                delete_status = "active",
                status = "pending",
                added_by = request.added_by,
                addedtype = request.addedtype,
                program_type = request.program_type,
                participant_type = request.participant_type,
                gender = request.gender,
                item_code = request.item_code,
                offstage_onstage = request.offstage_onstage,
                no_of_group = request.no_of_group,
                judgementcriteria = new List<prgm_judgement_criteria_model>(),
                ac_year_id=request.ac_year_id

            };

            _context.tbl_program.Add(division);
            await _context.SaveChangesAsync();

            if (division.id == null || division.id <= 0)
            {
                return BadRequest(new { status = false, message = "Failed to insert" });
            }

            if (request.judgementcriteria != null && request.judgementcriteria.Count > 0)
            {
                var subadminAssemblies = request.judgementcriteria.Select(assembly => new prgm_judgement_criteria_model
                {
                    prgm_id = division.id,
                    judgement_criteria = assembly.judgement_criteria,
                    addedon = DateTime.Now,
                    point = assembly.point,
                    name = assembly.name,
                }).ToList();

                _context.tbl_prgm_judgement_criteria.AddRange(subadminAssemblies);
                await _context.SaveChangesAsync();
            }


            return Ok(new { status = true, message = "Data added successfully", id = division.id });
        }


        [HttpPut]
        [Route("update_program")]
        public async Task<ActionResult> update_program([FromBody] programmodel request)
        {
            var data = await _context.tbl_program.FindAsync(request.id);

            if (data == null)
            {
                return Ok(new { status = false, message = "data not found" });
            }

            if (request.stage_id.HasValue)
            {
                data.stage_id = request.stage_id.Value;
            }
            //if (request.event_id.HasValue)
            //{
            //    data.event_id = request.event_id.Value;
            //}

            //if (request.no_of_regstn_per_institute.HasValue)
            //{
            //    data.no_of_regstn_per_institute = request.no_of_regstn_per_institute.Value;
            //}

            if (!string.IsNullOrEmpty(request.program_name))
            {
                data.program_name = request.program_name;
            }
            if (request.date.HasValue)
            {
                data.date = request.date.Value;
            }
            if (!string.IsNullOrEmpty(request.time))
            {
                data.time = request.time;
            }

            if (!string.IsNullOrEmpty(request.modified_type))
            {
                data.modified_type = request.modified_type;
            }
            if (request.modified_by.HasValue)
            {
                data.modified_by = request.modified_by.Value;
            }
            if (!string.IsNullOrEmpty(request.color_code))
            {
                data.color_code = request.color_code;
            }
            if (!string.IsNullOrEmpty(request.participant_type))
            {
                data.participant_type = request.participant_type;
            }

            if (!string.IsNullOrEmpty(request.gender))
            {
                data.gender = request.gender;
            }
            if (!string.IsNullOrEmpty(request.program_type))
            {
                data.program_type = request.program_type;
            }
            if (!string.IsNullOrEmpty(request.item_code))
            {
                data.item_code = request.item_code;
            }
            if (!string.IsNullOrEmpty(request.offstage_onstage))
            {
                data.offstage_onstage = request.offstage_onstage;
            }
            if (request.group_min_participants.HasValue)
            {
                data.group_min_participants = request.group_min_participants.Value;
            }
            if (request.group_max_participants.HasValue)
            {
                data.group_max_participants = request.group_max_participants.Value;
            }
            if (!string.IsNullOrEmpty(request.no_of_group))
            {
                data.no_of_group = request.no_of_group;
            }
            if (request.ac_year_id.HasValue)
            {
                data.ac_year_id = request.ac_year_id;
            }
            request.modifiedon = DateTime.Now;

            if (request.judgementcriteria != null && request.judgementcriteria.Count > 0)
            {
                var existingAssemblies = _context.tbl_prgm_judgement_criteria.Where(a => a.prgm_id == data.id);
                _context.tbl_prgm_judgement_criteria.RemoveRange(existingAssemblies);

                var newAssemblies = request.judgementcriteria.Select(a => new prgm_judgement_criteria_model
                {
                    prgm_id = data.id,
                    judgement_criteria = a.judgement_criteria,
                    name = a.name,
                    addedon = DateTime.Now,
                    point = a.point,
                }).ToList();

                await _context.tbl_prgm_judgement_criteria.AddRangeAsync(newAssemblies);
            }

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpGet]
        [Route("view_program")]
        //        public async Task<ActionResult> view_program(int? id, DateTime? date, int? stage_id, string? status, int? institute_id, string? participant_type, string? program_type, string? gender, string? keyword, string? item_code, int? ac_year_id, string? chest_no)
        //        {
        //            var enc_key = Request.Headers["XapiKey"].ToString();
        //            var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower() == "judge");

        //            // FIX: avoid direct (int) cast on a possibly-null reference_id
        //            int ref_id = 0;
        //            if (user != null)
        //            {
        //                ref_id = (int)(user.reference_id ?? 0);
        //            }

        //            if (_context.tbl_program == null)
        //                return NotFound(new { status = false, message = "Data not found" });

        //            var query = from p in _context.tbl_program
        //                        join s in _context.tbl_stage on p.stage_id equals s.id into sta
        //                        from st in sta.DefaultIfEmpty()
        //                        where p.delete_status == "active"
        //                        select new
        //                        {
        //                            p.id,
        //                            p.program_type,
        //                            p.participant_type,
        //                            p.gender,
        //                            p.stage_id,
        //                            stage_name = st != null ? st.stage_name : "",
        //                            p.program_name,
        //                            p.color_code,
        //                            p.date,
        //                            p.time,
        //                            p.added_by,
        //                            p.addedtype,
        //                            p.addedon,
        //                            p.status,
        //                            p.item_code,
        //                            p.offstage_onstage,
        //                            p.group_min_participants,
        //                            p.group_max_participants,
        //                            p.no_of_group,
        //                            p.ac_year_id,
        //                            participant_count = p.program_type.ToLower() == "group"
        //                                ? (from gm in _context.tbl_group_members
        //                                   join pp in _context.tbl_prgm_participants on gm.prgm_id equals pp.prgm_id
        //                                   where pp.prgm_id == p.id && pp.status.ToLower() != "deleted"
        //                                   select gm.id).Count()
        //                                : _context.tbl_prgm_participants
        //                                    .Count(pp => pp.prgm_id == p.id && pp.status.ToLower() != "deleted"),

        //                            judgement_criteria = _context.tbl_prgm_judgement_criteria
        //                                .Where(c => c.prgm_id == p.id)
        //                                .Join(_context.tbl_judgement_criteria,
        //                                      c => c.judgement_criteria,
        //                                      j => j.id,
        //                                      (c, j) => new
        //                                      {
        //                                          c.id,
        //                                          c.prgm_id,
        //                                          judgement_criteria = c.judgement_criteria,
        //                                          name = j != null ? j.name : "",
        //                                          point = c.point
        //                                      }).ToList(),

        //                            //program judges
        //                            judges = _context.tbl_judge_prgm
        //                                .Where(c => c.program_id == p.id && c.status == "active")
        //                                .Join(_context.tbl_judge,
        //                                      c => c.judge_id,
        //                                      j => j.id,
        //                                      (c, j) => new
        //                                      {
        //                                          c.id,
        //                                          c.judge_id,
        //                                          judge = j.judge_name
        //                                      })
        //                                .ToList(),

        //                            // FIX: both branches now return the SAME anonymous shape
        //                            // (same property names, same types, same order) so EF Core
        //                            // can translate the conditional expression. No more (object) cast needed.
        //                            prgm_student_detail = p.program_type.ToLower() == "group"

        //? (from gd in _context.tbl_group_members

        //   join s2 in _context.tbl_student
        //        on gd.student_id equals s2.id

        //   join i in _context.tbl_institute
        //        on s2.institute equals i.id

        //   join gp in _context.tbl_prgm_participants
        //        .Where(g => g.status.ToLower() != "deleted")
        //        on new { gd.prgm_id, chess_no = (string?)gd.chest_no }
        //        equals new { gp.prgm_id, chess_no = (string?)gp.chess_no }
        //        into gpJoin
        //   from sp in _context.tbl_prgm_participants

        //   join st in _context.tbl_student
        //        on sp.student_id equals st.id into std
        //   join pp in _context.tbl_prgm_point
        //        .Where(x => x.judge_id == ref_id)
        //        on new
        //        {
        //            prgm_id = gd.prgm_id,
        //            chess_no = gd.chest_no
        //        }
        //        equals new
        //        {
        //            prgm_id = pp.prgm_id,
        //            chess_no = pp.chess_no
        //        }
        //        into ppJoin
        //   from pp in ppJoin.DefaultIfEmpty()

        //   where gd.prgm_id == p.id
        //         && (!institute_id.HasValue || s2.institute == institute_id)
        //         && (string.IsNullOrWhiteSpace(chest_no)
        //             || gd.chest_no == chest_no)

        //   orderby s2.name

        //   select new
        //   {
        //       student_id = (int?)gd.student_id,
        //       student_name = s2.name,
        //       institute = (int?)s2.institute,
        //       institute_name = i.name,
        //       phone_no = s2.phone_no,
        //       admsn_no = s2.admsn_no,
        //       email = s2.email,
        //       chest_no = gd.chest_no,
        //       status = gd != null ? sp.program_status : null,
        //       group_name = gd.group_name,
        //       point = pp != null ? pp.point : null,
        //       judgement_criteria_id = pp != null ? pp.judgement_criteria_id : null
        //   }).ToList()

        //: (from sp in _context.tbl_prgm_participants

        //   join st in _context.tbl_student
        //        on sp.student_id equals st.id into std
        //   from student in std.DefaultIfEmpty()

        //   join i in _context.tbl_institute
        //        on student.institute equals i.id into inst
        //   from ins in inst.DefaultIfEmpty()

        //   join pp in _context.tbl_prgm_point
        //        .Where(x => x.judge_id == ref_id)
        //        on new
        //        {
        //            prgm_id = sp.prgm_id,
        //            chess_no = sp.chess_no
        //        }
        //        equals new
        //        {
        //            prgm_id = pp.prgm_id,
        //            chess_no = pp.chess_no
        //        }
        //        into ppJoin
        //   from pp in ppJoin.DefaultIfEmpty()

        //   where sp.prgm_id == p.id
        //         && sp.status.ToLower() != "deleted"
        //         && (!institute_id.HasValue ||
        //             (student != null && student.institute == institute_id))
        //         && (string.IsNullOrWhiteSpace(chest_no)
        //             || sp.chess_no == chest_no)

        //   orderby student != null ? student.name : ""

        //   select new
        //   {
        //       student_id = student != null ? (int?)student.id : null,
        //       student_name = student != null ? student.name : "",
        //       institute = student != null ? (int?)student.institute : null,
        //       institute_name = ins != null ? ins.name : "",
        //       phone_no = student != null ? student.phone_no : "",
        //       admsn_no = student != null ? student.admsn_no : "",
        //       email = student != null ? student.email : "",
        //       chest_no = sp.chess_no,
        //       status = sp.program_status,
        //       group_name = (string?)null,
        //       point = pp != null ? pp.point : null,
        //       judgement_criteria_id = pp != null ? pp.judgement_criteria_id : null
        //   }).ToList()
        //                        };

        //            if (id.HasValue)
        //                query = query.Where(g => g.id == id.Value);

        //            if (date.HasValue)
        //                query = query.Where(g => g.date == date);

        //            if (stage_id.HasValue)
        //                query = query.Where(g => g.stage_id == stage_id.Value);

        //            if (!string.IsNullOrWhiteSpace(status))
        //                query = query.Where(e => e.status == status);

        //            if (!string.IsNullOrWhiteSpace(participant_type))
        //                query = query.Where(e => e.participant_type == participant_type);

        //            if (!string.IsNullOrWhiteSpace(program_type))
        //                query = query.Where(e => e.program_type == program_type);

        //            if (!string.IsNullOrWhiteSpace(gender))
        //                query = query.Where(e => e.gender == gender);

        //            if (!string.IsNullOrWhiteSpace(item_code))
        //                query = query.Where(e => e.item_code == item_code);

        //            if (!string.IsNullOrWhiteSpace(keyword))
        //            {
        //                query = query.Where(e =>
        //                    e.program_name.Contains(keyword) || e.item_code.Contains(keyword));
        //            }
        //            if (ac_year_id.HasValue)
        //                query = query.Where(g => g.ac_year_id == ac_year_id.Value);

        //            var result = await query.ToListAsync();

        //            if (result == null || !result.Any())
        //                return Ok(new { status = false, message = "Data not found" });

        //            return Ok(new { status = true, message = "Success", data = result });
        //        }

        //    public async Task<ActionResult> view_program(
        //int? id,
        //DateTime? date,
        //int? stage_id,
        //string? status,
        //int? institute_id,
        //string? participant_type,
        //string? program_type,
        //string? gender,
        //string? keyword,
        //string? item_code,
        //int? ac_year_id,
        //string? chest_no)
        //    {
        //        // 1. GET JUDGE

        //        var enc_key = Request.Headers["XapiKey"].ToString();

        //        var user = await _context.tbl_user
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(u =>
        //                u.enc_key == enc_key &&
        //                u.type.ToLower() == "judge");

        //        int ref_id = 0;

        //        if (user != null)
        //        {
        //            ref_id = (int)(user.reference_id ?? 0);
        //        }


        //        // 2. CHECK PROGRAM TABLE

        //        if (_context.tbl_program == null)
        //        {
        //            return NotFound(new
        //            {
        //                status = false,
        //                message = "Data not found"
        //            });
        //        }


        //        // 3. GET PROGRAMS FIRST

        //        var query =
        //            from p in _context.tbl_program.AsNoTracking()

        //            join s in _context.tbl_stage.AsNoTracking()
        //                on p.stage_id equals s.id into sta

        //            from st in sta.DefaultIfEmpty()

        //            where p.delete_status == "active"

        //            select new
        //            {
        //                p.id,
        //                p.program_type,
        //                p.participant_type,
        //                p.gender,
        //                p.stage_id,

        //                stage_name =
        //                    st != null
        //                        ? st.stage_name
        //                        : "",

        //                p.program_name,
        //                p.color_code,
        //                p.date,
        //                p.time,
        //                p.added_by,
        //                p.addedtype,
        //                p.addedon,
        //                p.status,
        //                p.item_code,
        //                p.offstage_onstage,
        //                p.group_min_participants,
        //                p.group_max_participants,
        //                p.no_of_group,
        //                p.ac_year_id
        //            };


        //        // 4. APPLY PROGRAM FILTERS

        //        if (id.HasValue)
        //        {
        //            query = query.Where(g =>
        //                g.id == id.Value);
        //        }

        //        if (date.HasValue)
        //        {
        //            query = query.Where(g =>
        //                g.date == date);
        //        }

        //        if (stage_id.HasValue)
        //        {
        //            query = query.Where(g =>
        //                g.stage_id == stage_id.Value);
        //        }

        //        if (!string.IsNullOrWhiteSpace(status))
        //        {
        //            query = query.Where(e =>
        //                e.status == status);
        //        }

        //        if (!string.IsNullOrWhiteSpace(participant_type))
        //        {
        //            query = query.Where(e =>
        //                e.participant_type == participant_type);
        //        }

        //        if (!string.IsNullOrWhiteSpace(program_type))
        //        {
        //            query = query.Where(e =>
        //                e.program_type == program_type);
        //        }

        //        if (!string.IsNullOrWhiteSpace(gender))
        //        {
        //            query = query.Where(e =>
        //                e.gender == gender);
        //        }

        //        if (!string.IsNullOrWhiteSpace(item_code))
        //        {
        //            query = query.Where(e =>
        //                e.item_code == item_code);
        //        }

        //        if (!string.IsNullOrWhiteSpace(keyword))
        //        {
        //            query = query.Where(e =>
        //                e.program_name.Contains(keyword)
        //                ||
        //                e.item_code.Contains(keyword));
        //        }

        //        if (ac_year_id.HasValue)
        //        {
        //            query = query.Where(g =>
        //                g.ac_year_id == ac_year_id.Value);
        //        }


        //        // 5. EXECUTE ONLY PROGRAM QUERY

        //        var programs = await query.ToListAsync();


        //        // 6. NO DATA

        //        if (programs == null || !programs.Any())
        //        {
        //            return Ok(new
        //            {
        //                status = false,
        //                message = "Data not found"
        //            });
        //        }


        //        // 7. PROGRAM IDS

        //        var programIds = programs
        //            .Select(x => x.id)
        //            .ToList();


        //        // 8. LOAD JUDGEMENT CRITERIA SEPARATELY

        //        var judgementCriteriaData =
        //            await (
        //                from c in _context.tbl_prgm_judgement_criteria
        //                    .AsNoTracking()

        //                join j in _context.tbl_judgement_criteria
        //                    .AsNoTracking()
        //                    on c.judgement_criteria equals j.id

        //                where c.prgm_id.HasValue
        //                      && programIds.Contains(c.prgm_id.Value)

        //                select new
        //                {
        //                    c.id,
        //                    c.prgm_id,

        //                    judgement_criteria =
        //                        c.judgement_criteria,

        //                    name =
        //                        j != null
        //                            ? j.name
        //                            : "",

        //                    point = c.point
        //                }
        //            ).ToListAsync();


        //        // 9. GROUP JUDGEMENT CRITERIA BY PROGRAM

        //        var judgementCriteriaByProgram =
        //            judgementCriteriaData
        //                .GroupBy(x => x.prgm_id)
        //                .ToDictionary(
        //                    x => x.Key,
        //                    x => x.ToList()
        //                );


        //        // 10. LOAD JUDGES SEPARATELY

        //        var judgesData =
        //            await (
        //                from c in _context.tbl_judge_prgm
        //                    .AsNoTracking()

        //                join j in _context.tbl_judge
        //                    .AsNoTracking()
        //                    on c.judge_id equals j.id

        //                where c.program_id.HasValue
        //                      && programIds.Contains(c.program_id.Value)
        //                      && c.status == "active"

        //                select new
        //                {
        //                    c.id,
        //                    c.program_id,
        //                    c.judge_id,

        //                    judge =
        //                        j.judge_name
        //                }
        //            ).ToListAsync();


        //        // 11. GROUP JUDGES BY PROGRAM

        //        var judgesByProgram =
        //            judgesData
        //                .GroupBy(x => x.program_id)
        //                .ToDictionary(
        //                    x => x.Key,
        //                    x => x.ToList()
        //                );


        //        // 12. LOAD PARTICIPANTS FOR SELECTED PROGRAMS
        //        //
        //        // Used by:
        //        // - normal program details
        //        // - participant counts
        //        // - student loading
        //        //
        //        // IMPORTANT:
        //        // This is NOT used for the group "sp" cross-product.

        //        var participantsData =
        //            await _context.tbl_prgm_participants
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.prgm_id.HasValue &&
        //                    programIds.Contains(x.prgm_id.Value))
        //                .Select(x => new
        //                {
        //                    x.id,
        //                    x.prgm_id,
        //                    x.student_id,
        //                    x.chess_no,
        //                    x.status,
        //                    x.program_status
        //                })
        //                .ToListAsync();


        //        // 13. PARTICIPANTS BY PROGRAM

        //        var participantsByProgram =
        //            participantsData
        //                .GroupBy(x => x.prgm_id)
        //                .ToDictionary(
        //                    x => x.Key,
        //                    x => x.ToList()
        //                );


        //        // 14. LOAD ALL PARTICIPANTS
        //        //
        //        // VERY IMPORTANT
        //        //
        //        // Original code:
        //        //
        //        // from sp in _context.tbl_prgm_participants
        //        //
        //        // There is NO:
        //        //
        //        // sp.prgm_id == p.id
        //        //
        //        // Therefore ALL tbl_prgm_participants rows must be available
        //        // here to preserve the exact original group cross-product.

        //        var allParticipantsData =
        //            await _context.tbl_prgm_participants
        //                .AsNoTracking()
        //                .Select(x => new
        //                {
        //                    x.id,
        //                    x.prgm_id,
        //                    x.student_id,
        //                    x.chess_no,
        //                    x.status,
        //                    x.program_status
        //                })
        //                .ToListAsync();


        //        // 15. LOAD GROUP MEMBERS

        //        var groupMembersData =
        //            await _context.tbl_group_members
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.prgm_id.HasValue &&
        //                    programIds.Contains(x.prgm_id.Value))
        //                .Select(x => new
        //                {
        //                    x.id,
        //                    x.prgm_id,
        //                    x.student_id,
        //                    x.chest_no,
        //                    x.group_name
        //                })
        //                .ToListAsync();


        //        // 16. GROUP MEMBERS BY PROGRAM

        //        var groupMembersByProgram =
        //            groupMembersData
        //                .GroupBy(x => x.prgm_id)
        //                .ToDictionary(
        //                    x => x.Key,
        //                    x => x.ToList()
        //                );


        //        // 17. STUDENT IDS

        //        var studentIds =
        //            groupMembersData
        //                .Where(x => x.student_id.HasValue)
        //                .Select(x => x.student_id!.Value)
        //                .Concat(
        //                    participantsData
        //                        .Where(x => x.student_id.HasValue)
        //                        .Select(x => x.student_id!.Value)
        //                )
        //                .Distinct()
        //                .ToList();


        //        // 18. LOAD STUDENTS ONCE

        //        var studentsData =
        //            await _context.tbl_student
        //                .AsNoTracking()
        //                .Where(x => studentIds.Contains(x.id))
        //                .Select(x => new
        //                {
        //                    x.id,
        //                    x.name,
        //                    x.institute,
        //                    x.phone_no,
        //                    x.admsn_no,
        //                    x.email
        //                })
        //                .ToListAsync();


        //        // 19. STUDENTS BY ID

        //        var studentsById =
        //            studentsData.ToDictionary(
        //                x => x.id,
        //                x => x
        //            );


        //        // 20. INSTITUTE IDS

        //        var instituteIds =
        //            studentsData
        //                .Where(x => x.institute.HasValue)
        //                .Select(x => x.institute!.Value)
        //                .Distinct()
        //                .ToList();


        //        // 21. LOAD INSTITUTES ONCE

        //        var institutesData =
        //            await _context.tbl_institute
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.id.HasValue &&
        //                    instituteIds.Contains(x.id.Value))
        //                .Select(x => new
        //                {
        //                    x.id,
        //                    x.name
        //                })
        //                .ToListAsync();


        //        // 22. INSTITUTES BY ID

        //        var institutesById =
        //            institutesData.ToDictionary(
        //                x => x.id,
        //                x => x
        //            );


        //        // 23. LOAD JUDGE POINTS ONCE

        //        var judgePointsData =
        //            await _context.tbl_prgm_point
        //                .AsNoTracking()
        //                .Where(x =>
        //                    x.prgm_id.HasValue &&
        //                    programIds.Contains(x.prgm_id.Value) &&
        //                    x.judge_id == ref_id)
        //                .Select(x => new
        //                {
        //                    x.prgm_id,
        //                    x.chess_no,
        //                    x.point,
        //                    x.judgement_criteria_id
        //                })
        //                .ToListAsync();


        //        // 24. POINTS BY PROGRAM + CHEST NO

        //        var pointsByProgramChest =
        //            judgePointsData
        //                .GroupBy(x => new
        //                {
        //                    x.prgm_id,
        //                    x.chess_no
        //                })
        //                .ToDictionary(
        //                    x => (
        //                        x.Key.prgm_id,
        //                        x.Key.chess_no
        //                    ),
        //                    x => x.ToList()
        //                );


        //        // 25. PARTICIPANT COUNTS

        //        var participantCountByProgram =
        //            new Dictionary<int, int>();


        //        foreach (var program in programs)
        //        {
        //            if (program.program_type.ToLower() == "group")
        //            {
        //                // ORIGINAL GROUP COUNT:
        //                //
        //                // from gm in tbl_group_members
        //                // join pp in tbl_prgm_participants
        //                //      on gm.prgm_id equals pp.prgm_id
        //                // where pp.prgm_id == p.id
        //                //       && pp.status.ToLower() != "deleted"
        //                // select gm.id
        //                //
        //                // Since groupMembersByProgram and
        //                // participantsByProgram are already restricted
        //                // to the same program, this produces:
        //                //
        //                // group members count * active participants count

        //                var groupCount = 0;

        //                if (groupMembersByProgram.TryGetValue(
        //                        program.id,
        //                        out var groupMembers))
        //                {
        //                    if (participantsByProgram.TryGetValue(
        //                            program.id,
        //                            out var programParticipants))
        //                    {
        //                        var activeParticipants =
        //                            programParticipants
        //                                .Count(x =>
        //                                    x.status.ToLower() != "deleted");

        //                        groupCount =
        //                            groupMembers.Count *
        //                            activeParticipants;
        //                    }
        //                }

        //                participantCountByProgram[program.id] =
        //                    groupCount;
        //            }
        //            else
        //            {
        //                // NORMAL PROGRAM COUNT

        //                var normalCount = 0;

        //                if (participantsByProgram.TryGetValue(
        //                        program.id,
        //                        out var programParticipants))
        //                {
        //                    normalCount =
        //                        programParticipants.Count(x =>
        //                            x.status.ToLower() != "deleted");
        //                }

        //                participantCountByProgram[program.id] =
        //                    normalCount;
        //            }
        //        }


        //        // 26. BUILD GROUP STUDENT DETAILS

        //        var groupStudentDetailsByProgram =
        //            new Dictionary<int, List<object>>();


        //        foreach (var program in programs)
        //        {
        //            if (program.program_type.ToLower() != "group")
        //                continue;


        //            var details =
        //                new List<object>();


        //            if (!groupMembersByProgram.TryGetValue(
        //                    program.id,
        //                    out var groupMembers))
        //            {
        //                groupStudentDetailsByProgram[program.id] =
        //                    details;

        //                continue;
        //            }


        //            // ========================================================
        //            // IMPORTANT:
        //            //
        //            // This MUST be all participants.
        //            //
        //            // Original:
        //            //
        //            // from sp in _context.tbl_prgm_participants
        //            //
        //            // There is no program filter.
        //            // ========================================================

        //            var allParticipants =
        //                allParticipantsData;


        //            foreach (var gd in groupMembers)
        //            {
        //                // ====================================================
        //                // ORIGINAL:
        //                //
        //                // join s2 in tbl_student
        //                //      on gd.student_id equals s2.id
        //                //
        //                // This is INNER JOIN.
        //                //
        //                // Therefore if student does not exist,
        //                // original query does NOT return this gd.
        //                // ====================================================

        //                if (!gd.student_id.HasValue)
        //                    continue;


        //                if (!studentsById.TryGetValue(
        //                        gd.student_id.Value,
        //                        out var student))
        //                {
        //                    continue;
        //                }


        //                // ====================================================
        //                // ORIGINAL:
        //                //
        //                // join i in tbl_institute
        //                //      on s2.institute equals i.id
        //                //
        //                // This is also INNER JOIN.
        //                // ====================================================

        //                if (!student.institute.HasValue)
        //                    continue;


        //                if (!institutesById.TryGetValue(
        //                        student.institute.Value,
        //                        out var instituteData))
        //                {
        //                    continue;
        //                }


        //                // ====================================================
        //                // INSTITUTE FILTER
        //                // ====================================================

        //                if (institute_id.HasValue &&
        //                    student.institute != institute_id)
        //                {
        //                    continue;
        //                }


        //                // ====================================================
        //                // CHEST NO FILTER
        //                // ====================================================

        //                if (!string.IsNullOrWhiteSpace(chest_no) &&
        //                    gd.chest_no != chest_no)
        //                {
        //                    continue;
        //                }


        //                // ====================================================
        //                // ORIGINAL:
        //                //
        //                // from sp in _context.tbl_prgm_participants
        //                //
        //                // No join condition.
        //                //
        //                // Therefore CROSS PRODUCT.
        //                //
        //                // Also IMPORTANT:
        //                // There is NO sp.status != deleted filter here.
        //                // ====================================================

        //                foreach (var sp in allParticipants)
        //                {
        //                    // =================================================
        //                    // POINT LOOKUP
        //                    //
        //                    // Original LEFT JOIN:
        //                    //
        //                    // gd.prgm_id + gd.chest_no
        //                    // =
        //                    // pp.prgm_id + pp.chess_no
        //                    //
        //                    // judge_id == ref_id
        //                    // =================================================

        //                    if (pointsByProgramChest.TryGetValue(
        //                            (gd.prgm_id, gd.chest_no),
        //                            out var pointList)
        //                        &&
        //                        pointList.Count > 0)
        //                    {
        //                        // =============================================
        //                        // Original LEFT JOIN can produce multiple
        //                        // point rows.
        //                        //
        //                        // Preserve every point row.
        //                        // =============================================

        //                        foreach (var pp in pointList)
        //                        {
        //                            details.Add(new
        //                            {
        //                                student_id =
        //                                    (int?)gd.student_id,

        //                                student_name =
        //                                    student.name,

        //                                institute =
        //                                    (int?)student.institute,

        //                                institute_name =
        //                                    instituteData.name,

        //                                phone_no =
        //                                    student.phone_no,

        //                                admsn_no =
        //                                    student.admsn_no,

        //                                email =
        //                                    student.email,

        //                                chest_no =
        //                                    gd.chest_no,

        //                                status =
        //                                    sp.program_status,

        //                                group_name =
        //                                    gd.group_name,

        //                                point =
        //                                    pp.point,

        //                                judgement_criteria_id =
        //                                    pp.judgement_criteria_id
        //                            });
        //                        }
        //                    }
        //                    else
        //                    {
        //                        // =============================================
        //                        // Original LEFT JOIN:
        //                        //
        //                        // pp == null
        //                        //
        //                        // Therefore point and criteria are null.
        //                        // =============================================

        //                        details.Add(new
        //                        {
        //                            student_id =
        //                                (int?)gd.student_id,

        //                            student_name =
        //                                student.name,

        //                            institute =
        //                                (int?)student.institute,

        //                            institute_name =
        //                                instituteData.name,

        //                            phone_no =
        //                                student.phone_no,

        //                            admsn_no =
        //                                student.admsn_no,

        //                            email =
        //                                student.email,

        //                            chest_no =
        //                                gd.chest_no,

        //                            status =
        //                                sp.program_status,

        //                            group_name =
        //                                gd.group_name,

        //                            point =
        //                                (decimal?)null,

        //                            judgement_criteria_id =
        //                                (int?)null
        //                        });
        //                    }
        //                }
        //            }


        //            // ========================================================
        //            // ORIGINAL:
        //            //
        //            // orderby s2.name
        //            //
        //            // Since we already have student name available,
        //            // sort by the exact same value.
        //            // ========================================================

        //            details =
        //                details
        //                    .OrderBy(x =>
        //                    {
        //                        var property =
        //                            x.GetType()
        //                                .GetProperty("student_name");

        //                        return property?
        //                            .GetValue(x)?
        //                            .ToString()
        //                            ?? "";
        //                    })
        //                    .ToList();


        //            groupStudentDetailsByProgram[program.id] =
        //                details;
        //        }


        //        // ============================================================
        //        // 27. BUILD NORMAL STUDENT DETAILS
        //        // ============================================================

        //        var normalStudentDetailsByProgram =
        //            new Dictionary<int, List<object>>();


        //        foreach (var program in programs)
        //        {
        //            if (program.program_type.ToLower() == "group")
        //                continue;


        //            var details =
        //                new List<object>();


        //            if (!participantsByProgram.TryGetValue(
        //                    program.id,
        //                    out var programParticipants))
        //            {
        //                normalStudentDetailsByProgram[program.id] =
        //                    details;

        //                continue;
        //            }


        //            foreach (var sp in programParticipants)
        //            {
        //                // ====================================================
        //                // ORIGINAL:
        //                //
        //                // sp.status.ToLower() != "deleted"
        //                // ====================================================

        //                if (sp.status.ToLower() == "deleted")
        //                    continue;


        //                // ====================================================
        //                // ORIGINAL LEFT JOIN student
        //                // ====================================================

        //                studentsById.TryGetValue(
        //                    sp.student_id ?? 0,
        //                    out var student);


        //                // ====================================================
        //                // INSTITUTE FILTER
        //                // ====================================================

        //                if (institute_id.HasValue)
        //                {
        //                    if (student == null ||
        //                        student.institute != institute_id)
        //                    {
        //                        continue;
        //                    }
        //                }


        //                // ====================================================
        //                // CHEST NO FILTER
        //                // ====================================================

        //                if (!string.IsNullOrWhiteSpace(chest_no))
        //                {
        //                    if (sp.chess_no != chest_no)
        //                    {
        //                        continue;
        //                    }
        //                }


        //                // ====================================================
        //                // STUDENT VALUES
        //                // ====================================================

        //                int? studentId =
        //                    student != null
        //                        ? (int?)student.id
        //                        : null;


        //                string studentName =
        //                    student != null
        //                        ? student.name
        //                        : "";


        //                int? institute =
        //                    student != null
        //                        ? student.institute
        //                        : null;


        //                string instituteName = "";


        //                if (student != null &&
        //                    student.institute.HasValue &&
        //                    institutesById.TryGetValue(
        //                        student.institute.Value,
        //                        out var instituteData))
        //                {
        //                    instituteName =
        //                        instituteData.name;
        //                }


        //                string phoneNo =
        //                    student != null
        //                        ? student.phone_no
        //                        : "";


        //                string admsnNo =
        //                    student != null
        //                        ? student.admsn_no
        //                        : "";


        //                string email =
        //                    student != null
        //                        ? student.email
        //                        : "";


        //                // ====================================================
        //                // POINTS
        //                // ====================================================

        //                if (pointsByProgramChest.TryGetValue(
        //                        (sp.prgm_id, sp.chess_no),
        //                        out var pointList)
        //                    &&
        //                    pointList.Count > 0)
        //                {
        //                    foreach (var pp in pointList)
        //                    {
        //                        details.Add(new
        //                        {
        //                            student_id =
        //                                studentId,

        //                            student_name =
        //                                studentName,

        //                            institute =
        //                                institute,

        //                            institute_name =
        //                                instituteName,

        //                            phone_no =
        //                                phoneNo,

        //                            admsn_no =
        //                                admsnNo,

        //                            email =
        //                                email,

        //                            chest_no =
        //                                sp.chess_no,

        //                            status =
        //                                sp.program_status,

        //                            group_name =
        //                                (string?)null,

        //                            point =
        //                                pp.point,

        //                            judgement_criteria_id =
        //                                pp.judgement_criteria_id
        //                        });
        //                    }
        //                }
        //                else
        //                {
        //                    details.Add(new
        //                    {
        //                        student_id =
        //                            studentId,

        //                        student_name =
        //                            studentName,

        //                        institute =
        //                            institute,

        //                        institute_name =
        //                            instituteName,

        //                        phone_no =
        //                            phoneNo,

        //                        admsn_no =
        //                            admsnNo,

        //                        email =
        //                            email,

        //                        chest_no =
        //                            sp.chess_no,

        //                        status =
        //                            sp.program_status,

        //                        group_name =
        //                            (string?)null,

        //                        point =
        //                            (decimal?)null,

        //                        judgement_criteria_id =
        //                            (int?)null
        //                    });
        //                }
        //            }


        //            // ========================================================
        //            // ORIGINAL:
        //            //
        //            // orderby student.name
        //            // ========================================================

        //            details =
        //                details
        //                    .OrderBy(x =>
        //                    {
        //                        var property =
        //                            x.GetType()
        //                                .GetProperty("student_name");

        //                        return property?
        //                            .GetValue(x)?
        //                            .ToString()
        //                            ?? "";
        //                    })
        //                    .ToList();


        //            normalStudentDetailsByProgram[program.id] =
        //                details;
        //        }


        //        // ============================================================
        //        // 28. FINAL JSON RESULT
        //        // ============================================================

        //        var result =
        //            programs.Select(p =>
        //            {
        //                // ====================================================
        //                // JUDGEMENT CRITERIA
        //                // ====================================================

        //                IEnumerable<object> judgementCriteria =
        //                    judgementCriteriaByProgram.TryGetValue(
        //                        p.id,
        //                        out var criteriaList)
        //                        ? criteriaList.Cast<object>()
        //                        : Enumerable.Empty<object>();


        //                // ====================================================
        //                // JUDGES
        //                // ====================================================

        //                IEnumerable<object> judges =
        //                    judgesByProgram.TryGetValue(
        //                        p.id,
        //                        out var judgeList)
        //                        ? judgeList.Cast<object>()
        //                        : Enumerable.Empty<object>();


        //                // ====================================================
        //                // STUDENT DETAILS
        //                // ====================================================

        //                List<object> studentDetails;


        //                if (p.program_type.ToLower() == "group")
        //                {
        //                    studentDetails =
        //                        groupStudentDetailsByProgram.TryGetValue(
        //                            p.id,
        //                            out var groupDetails)
        //                            ? groupDetails
        //                            : new List<object>();
        //                }
        //                else
        //                {
        //                    studentDetails =
        //                        normalStudentDetailsByProgram.TryGetValue(
        //                            p.id,
        //                            out var normalDetails)
        //                            ? normalDetails
        //                            : new List<object>();
        //                }


        //                // ====================================================
        //                // PARTICIPANT COUNT
        //                // ====================================================

        //                var participantCount =
        //                    participantCountByProgram.TryGetValue(
        //                        p.id,
        //                        out var count)
        //                        ? count
        //                        : 0;


        //                // ====================================================
        //                // SAME JSON STRUCTURE
        //                // ====================================================

        //                return new
        //                {
        //                    p.id,
        //                    p.program_type,
        //                    p.participant_type,
        //                    p.gender,
        //                    p.stage_id,
        //                    p.stage_name,
        //                    p.program_name,
        //                    p.color_code,
        //                    p.date,
        //                    p.time,
        //                    p.added_by,
        //                    p.addedtype,
        //                    p.addedon,
        //                    p.status,
        //                    p.item_code,
        //                    p.offstage_onstage,
        //                    p.group_min_participants,
        //                    p.group_max_participants,
        //                    p.no_of_group,
        //                    p.ac_year_id,

        //                    participant_count =
        //                        participantCount,

        //                    judgement_criteria =
        //                        judgementCriteria,

        //                    judges =
        //                        judges,

        //                    prgm_student_detail =
        //                        studentDetails
        //                };
        //            })
        //            .ToList();


        //        // ============================================================
        //        // 29. SUCCESS
        //        // ============================================================

        //        return Ok(new
        //        {
        //            status = true,
        //            message = "Success",
        //            data = result
        //        });
        //    }

        public async Task<ActionResult> view_program(
int? id,
DateTime? date,
int? stage_id,
string? status,
int? institute_id,
string? participant_type,
string? program_type,
string? gender,
string? keyword,
string? item_code,
int? ac_year_id,
string? chest_no)
        {
            // 1. GET JUDGE

            var enc_key = Request.Headers["XapiKey"].ToString();

            var user = await _context.tbl_user
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.enc_key == enc_key &&
                    u.type.ToLower() == "judge");

            int ref_id = 0;

            if (user != null)
            {
                ref_id = (int)(user.reference_id ?? 0);
            }


            // 2. CHECK PROGRAM TABLE

            if (_context.tbl_program == null)
            {
                return NotFound(new
                {
                    status = false,
                    message = "Data not found"
                });
            }


            // 3. GET PROGRAMS FIRST

            var query =
                from p in _context.tbl_program.AsNoTracking()

                join s in _context.tbl_stage.AsNoTracking()
                    on p.stage_id equals s.id into sta

                from st in sta.DefaultIfEmpty()

                where p.delete_status == "active"

                select new
                {
                    p.id,
                    p.program_type,
                    p.participant_type,
                    p.gender,
                    p.stage_id,

                    stage_name =
                        st != null
                            ? st.stage_name
                            : "",

                    p.program_name,
                    p.color_code,
                    p.date,
                    p.time,
                    p.added_by,
                    p.addedtype,
                    p.addedon,
                    p.status,
                    p.item_code,
                    p.offstage_onstage,
                    p.group_min_participants,
                    p.group_max_participants,
                    p.no_of_group,
                    p.ac_year_id
                };


            // 4. APPLY PROGRAM FILTERS

            if (id.HasValue)
            {
                query = query.Where(g =>
                    g.id == id.Value);
            }

            if (date.HasValue)
            {
                query = query.Where(g =>
                    g.date == date);
            }

            if (stage_id.HasValue)
            {
                query = query.Where(g =>
                    g.stage_id == stage_id.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(e =>
                    e.status == status);
            }

            if (!string.IsNullOrWhiteSpace(participant_type))
            {
                query = query.Where(e =>
                    e.participant_type == participant_type);
            }

            if (!string.IsNullOrWhiteSpace(program_type))
            {
                query = query.Where(e =>
                    e.program_type == program_type);
            }

            if (!string.IsNullOrWhiteSpace(gender))
            {
                query = query.Where(e =>
                    e.gender == gender);
            }

            if (!string.IsNullOrWhiteSpace(item_code))
            {
                query = query.Where(e =>
                    e.item_code == item_code);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(e =>
                    e.program_name.Contains(keyword)
                    ||
                    e.item_code.Contains(keyword));
            }

            if (ac_year_id.HasValue)
            {
                query = query.Where(g =>
                    g.ac_year_id == ac_year_id.Value);
            }


            // 5. EXECUTE ONLY PROGRAM QUERY

            var programs = await query.ToListAsync();


            // 6. NO DATA

            if (programs == null || !programs.Any())
            {
                return Ok(new
                {
                    status = false,
                    message = "Data not found"
                });
            }


            // 7. PROGRAM IDS

            var programIds = programs
                .Select(x => x.id)
                .ToList();


            // 8. LOAD JUDGEMENT CRITERIA SEPARATELY

            var judgementCriteriaData =
                await (
                    from c in _context.tbl_prgm_judgement_criteria
                        .AsNoTracking()

                    join j in _context.tbl_judgement_criteria
                        .AsNoTracking()
                        on c.judgement_criteria equals j.id

                    where c.prgm_id.HasValue
                          && programIds.Contains(c.prgm_id.Value)

                    select new
                    {
                        c.id,
                        c.prgm_id,

                        judgement_criteria =
                            c.judgement_criteria,

                        name =
                            j != null
                                ? j.name
                                : "",

                        point = c.point
                    }
                ).ToListAsync();


            // 9. GROUP JUDGEMENT CRITERIA BY PROGRAM

            var judgementCriteriaByProgram =
                judgementCriteriaData
                    .GroupBy(x => x.prgm_id)
                    .ToDictionary(
                        x => x.Key,
                        x => x.ToList()
                    );


            // 10. LOAD JUDGES SEPARATELY

            var judgesData =
                await (
                    from c in _context.tbl_judge_prgm
                        .AsNoTracking()

                    join j in _context.tbl_judge
                        .AsNoTracking()
                        on c.judge_id equals j.id

                    where c.program_id.HasValue
                          && programIds.Contains(c.program_id.Value)
                          && c.status == "active"

                    select new
                    {
                        c.id,
                        c.program_id,
                        c.judge_id,

                        judge =
                            j.judge_name
                    }
                ).ToListAsync();


            // 11. GROUP JUDGES BY PROGRAM

            var judgesByProgram =
                judgesData
                    .GroupBy(x => x.program_id)
                    .ToDictionary(
                        x => x.Key,
                        x => x.ToList()
                    );


            // 12. LOAD PARTICIPANTS FOR SELECTED PROGRAMS
            //
            // Used by:
            // - normal program details
            // - participant counts
            // - student loading
            //
            // IMPORTANT:
            // This is NOT used for the group "sp" cross-product.

            var participantsData =
                await _context.tbl_prgm_participants
                    .AsNoTracking()
                    .Where(x =>
                        x.prgm_id.HasValue &&
                        programIds.Contains(x.prgm_id.Value))
                    .Select(x => new
                    {
                        x.id,
                        x.prgm_id,
                        x.student_id,
                        x.chess_no,
                        x.status,
                        x.group_name,
                        x.program_status,
                        x.average_point,
                        x.position_point,
                        x.total_point
                    })
                    .ToListAsync();


            // 13. PARTICIPANTS BY PROGRAM

            var participantsByProgram =
                participantsData
                    .GroupBy(x => x.prgm_id)
                    .ToDictionary(
                        x => x.Key,
                        x => x.ToList()
                    );


            // ============================================================
            // 14. STUDENT IDS - ONLY FROM tbl_prgm_participants
            // ============================================================

            var studentIds = participantsData
                .Where(x => x.student_id.HasValue)
                .Select(x => x.student_id!.Value)
                .Distinct()
                .ToList();


            // ============================================================
            // 15. LOAD STUDENTS ONCE
            // ============================================================

            var studentsData =
                await _context.tbl_student
                    .AsNoTracking()
                    .Where(x => studentIds.Contains(x.id))
                    .Select(x => new
                    {
                        x.id,
                        x.name,
                        x.institute,
                        x.phone_no,
                        x.admsn_no,
                        x.email
                    })
                    .ToListAsync();


            // ============================================================
            // 16. STUDENTS BY ID
            // ============================================================

            var studentsById =
                studentsData.ToDictionary(
                    x => x.id,
                    x => x
                );


            // ============================================================
            // 17. INSTITUTE IDS
            // ============================================================

            var instituteIds =
                studentsData
                    .Where(x => x.institute.HasValue)
                    .Select(x => x.institute!.Value)
                    .Distinct()
                    .ToList();


            // ============================================================
            // 18. LOAD INSTITUTES ONCE
            // ============================================================

            var institutesData =
                await _context.tbl_institute
                    .AsNoTracking()
                    .Where(x =>
                        x.id.HasValue &&
                        instituteIds.Contains(x.id.Value))
                    .Select(x => new
                    {
                        x.id,
                        x.name
                    })
                    .ToListAsync();


            // ============================================================
            // 19. INSTITUTES BY ID
            // ============================================================

            var institutesById =
                institutesData.ToDictionary(
                    x => x.id,
                    x => x
                );


            // ============================================================
            // 20. LOAD JUDGE POINTS ONCE
            // ============================================================

            var judgePointsData =
                await _context.tbl_prgm_point
                    .AsNoTracking()
                    .Where(x =>
                        x.prgm_id.HasValue &&
                        programIds.Contains(x.prgm_id.Value) &&
                        x.judge_id == ref_id)
                    .Select(x => new
                    {
                        x.prgm_id,
                        x.chess_no,
                        x.point,
                        x.judgement_criteria_id
                    })
                    .ToListAsync();


            // ============================================================
            // 21. POINTS BY PROGRAM + CHEST NO
            // ============================================================

            var pointsByProgramChest =
                judgePointsData
                    .GroupBy(x => new
                    {
                        x.prgm_id,
                        x.chess_no
                    })
                    .ToDictionary(
                        x => (
                            x.Key.prgm_id,
                            x.Key.chess_no
                        ),
                        x => x.ToList()
                    );


            // ============================================================
            // 22. PARTICIPANT COUNT
            // ONLY FROM tbl_prgm_participants
            // ============================================================

            var participantCountByProgram =
                participantsData
                    .GroupBy(x => x.prgm_id)
                    .ToDictionary(
                        x => x.Key!.Value,
                        x => x.Count(p =>
                            p.status.ToLower() != "deleted")
                    );


            // ============================================================
            // 23. BUILD STUDENT DETAILS
            // ONLY FROM tbl_prgm_participants
            // ============================================================

            var studentDetailsByProgram =
                new Dictionary<int, List<object>>();


            foreach (var program in programs)
            {
                var details =
                    new List<object>();


                if (!participantsByProgram.TryGetValue(
                        program.id,
                        out var programParticipants))
                {
                    studentDetailsByProgram[program.id] = details;
                    continue;
                }


                foreach (var sp in programParticipants)
                {
                    // Skip deleted participants
                    if (sp.status.ToLower() == "deleted")
                        continue;


                    // Student
                    studentsById.TryGetValue(
                        sp.student_id ?? 0,
                        out var student);


                    // Institute filter
                    if (institute_id.HasValue)
                    {
                        if (student == null ||
                            student.institute != institute_id)
                        {
                            continue;
                        }
                    }


                    // Chest number filter
                    if (!string.IsNullOrWhiteSpace(chest_no))
                    {
                        if (sp.chess_no != chest_no)
                        {
                            continue;
                        }
                    }


                    // Student values
                    int? studentId =
                        student != null
                            ? (int?)student.id
                            : null;


                    string studentName =
                        student != null
                            ? student.name
                            : "";


                    int? institute =
                        student != null
                            ? student.institute
                            : null;


                    string instituteName = "";


                    if (student != null &&
                        student.institute.HasValue &&
                        institutesById.TryGetValue(
                            student.institute.Value,
                            out var instituteData))
                    {
                        instituteName =
                            instituteData.name;
                    }


                    string phoneNo =
                        student != null
                            ? student.phone_no
                            : "";


                    string admsnNo =
                        student != null
                            ? student.admsn_no
                            : "";


                    string email =
                        student != null
                            ? student.email
                            : "";
                   

                    // Points
                    if (pointsByProgramChest.TryGetValue(
                            (sp.prgm_id, sp.chess_no),
                            out var pointList)
                        &&
                        pointList.Count > 0)
                    {
                        foreach (var pp in pointList)
                        {
                            details.Add(new
                            {
                                student_id = studentId,
                                student_name = studentName,
                                institute = institute,
                                institute_name = instituteName,
                                phone_no = phoneNo,
                                admsn_no = admsnNo,
                                email = email,
                                chest_no = sp.chess_no,
                                status = sp.program_status,

                                // No tbl_group_members
                                group_name = (string?)null,

                                point = pp.point,
                                judgement_criteria_id =
                                    pp.judgement_criteria_id
                            });
                        }
                    }
                    else
                    {
                        details.Add(new
                        {
                            student_id = studentId,
                            student_name = studentName,
                            institute = institute,
                            institute_name = instituteName,
                            phone_no = phoneNo,
                            admsn_no = admsnNo,
                            email = email,
                            chest_no = sp.chess_no,
                            status = sp.program_status,

                            // No tbl_group_members
                            group_name = sp.group_name,
                            point = sp.average_point,

                            judgement_criteria_id = (int?)null
                        });
                    }
                }


                // Order by student name
                details =
                    details
                        .OrderBy(x =>
                        {
                            var property =
                                x.GetType()
                                    .GetProperty("student_name");

                            return property?
                                .GetValue(x)?
                                .ToString()
                                ?? "";
                        })
                        .ToList();


                studentDetailsByProgram[program.id] =
                    details;
            }


            // ============================================================
            // 28. FINAL JSON RESULT
            // ============================================================

            var result =
                programs.Select(p =>
                {
                    // ====================================================
                    // JUDGEMENT CRITERIA
                    // ====================================================

                    IEnumerable<object> judgementCriteria =
                        judgementCriteriaByProgram.TryGetValue(
                            p.id,
                            out var criteriaList)
                            ? criteriaList.Cast<object>()
                            : Enumerable.Empty<object>();


                    // ====================================================
                    // JUDGES
                    // ====================================================

                    IEnumerable<object> judges =
                        judgesByProgram.TryGetValue(
                            p.id,
                            out var judgeList)
                            ? judgeList.Cast<object>()
                            : Enumerable.Empty<object>();


                    // ====================================================
                    // STUDENT DETAILS
                    // ====================================================



                    List<object> studentDetails =
      studentDetailsByProgram.TryGetValue(
          p.id,
          out var details)
          ? details
          : new List<object>();

                    // ====================================================
                    // PARTICIPANT COUNT
                    // ====================================================

                    var participantCount =
                        participantCountByProgram.TryGetValue(
                            p.id,
                            out var count)
                            ? count
                            : 0;


                    // ====================================================
                    // SAME JSON STRUCTURE
                    // ====================================================

                    return new
                    {
                        p.id,
                        p.program_type,
                        p.participant_type,
                        p.gender,
                        p.stage_id,
                        p.stage_name,
                        p.program_name,
                        p.color_code,
                        p.date,
                        p.time,
                        p.added_by,
                        p.addedtype,
                        p.addedon,
                        p.status,
                        p.item_code,
                        p.offstage_onstage,
                        p.group_min_participants,
                        p.group_max_participants,
                        p.no_of_group,
                        p.ac_year_id,

                        participant_count =
                            participantCount,

                        judgement_criteria =
                            judgementCriteria,

                        judges =
                            judges,

                        prgm_student_detail =
                            studentDetails
                    };
                })
                .ToList();


            // ============================================================
            // 29. SUCCESS
            // ============================================================

            return Ok(new
            {
                status = true,
                message = "Success",
                data = result
            });
        }


        [HttpGet]
        [Route("prgm_shedule_detail")]

        public async Task<ActionResult> prgm_shedule_detail(
    int? program_id,
    int? institute_id = null,
    int? stage_id = null,
    DateTime? date = null,
    string? status = null,
    string? keyword = null,
    string? item_code = null,
    string? admsn_no = null,
    int? ac_year_id = null)
        {
            var eventData = await _context.tbl_event
               .FirstOrDefaultAsync(e => e.status == "active"&&e.ac_year_id==ac_year_id);
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            // ✅ Validation: Require either program_id or item_code
            if ((program_id == null || program_id <= 0) && string.IsNullOrEmpty(item_code))
            {
                return Ok(new { status = false, message = "Program ID or Item Code is required" });
            }

            if (_context.tbl_prgm_participants == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            List<object> queryResult;

            // If program_id not provided, resolve it using item_code
            if (program_id == null && !string.IsNullOrEmpty(item_code))
            {
                program_id = await _context.tbl_program
                    .Where(p => p.item_code == item_code)
                    .Select(p => (int?)p.id)
                    .FirstOrDefaultAsync();

                if (program_id == null)
                {
                    return Ok(new { status = false, message = "Program not found" });
                }
            }

            var programType = await _context.tbl_program
                .Where(p => p.id == program_id)
                .Select(p => p.program_type)
                .FirstOrDefaultAsync();

            if (programType?.ToLower() == "group")
            {
                queryResult = await (
                    from pp in _context.tbl_prgm_participants
                        //join pp in _context.tbl_prgm_participants
                        //    on new { gm.prgm_id, gm.student_id } equals new { pp.prgm_id, pp.student_id } into ppJoin
                        // from pp in ppJoin.DefaultIfEmpty()


                    join prog in _context.tbl_program on pp.prgm_id equals prog.id
                    join s in _context.tbl_stage on prog.stage_id equals s.id into sta
                    from stg in sta.DefaultIfEmpty()
                    join stud in _context.tbl_student on pp.student_id equals stud.id
                    join institute in _context.tbl_institute on stud.institute equals institute.id into inst
                    from institute in inst.DefaultIfEmpty()
                    join cl in _context.tbl_class on stud.class_id equals cl.id into classJoin
                    from cl in classJoin.DefaultIfEmpty()
                    join di in _context.tbl_division on stud.division_id equals di.id into divisionJoin
                    from di in divisionJoin.DefaultIfEmpty()
                    where pp.prgm_id == program_id
                          && (institute_id == null || stud.institute == institute_id)
                          && (stage_id == null || prog.stage_id == stage_id)
                          && (date == null || prog.date == date)
                          && (status == null || prog.status == status)
                          && (admsn_no == null || stud.admsn_no == admsn_no)
                          && (item_code == null || prog.item_code == item_code)
                                && (ac_year_id == null || prog.ac_year_id == ac_year_id)
                    orderby institute.name, pp.group_name  //  ordering


                    select new
                    {
                        program = prog.program_name,
                        programstatus = prog.status,
                        no_of_group = prog.no_of_group,
                        stage_id = prog.stage_id,
                        stage = stg.stage_name,
                        participant_type = prog.participant_type,
                        gender = prog.gender,
                        program_type = prog.program_type,
                        date = prog.date,
                        time = prog.time,
                        student_id = pp.student_id,
                        chess_no = pp != null ? pp.chess_no : null,
                        name = stud.name,
                        institute_id = stud.institute,
                        institute_name = institute != null ? institute.name : null,
                        admsn_no = stud.admsn_no,
                        email = stud.email,
                        phone_no = stud.phone_no,
                        class_id = stud.class_id,
                        division_id = stud.division_id,
                        image = stud.image,
                        imageurl = !string.IsNullOrEmpty(stud.image) ? $"{baseUrl}uploads/student/{stud.image}" : null,
                        class_name = cl != null ? cl.@class : null,
                        division_name = di != null ? di.division : null,
                        id = pp.prgm_id,
                        status = pp != null ? pp.program_status : "pending",
                        token_no = pp != null ? pp.token_no : null,
                        item_code = prog.item_code,
                        group_name = pp != null ? pp.group_name : null,
                        total_points = pp != null ? pp.average_point ?? 0 : 0,
                        ac_year_id = stud.ac_year_id

                    }
                ).Cast<object>().ToListAsync();
            }
            else
            {
                queryResult = await (
                    from prp in _context.tbl_prgm_participants
                    join prog in _context.tbl_program on prp.prgm_id equals prog.id
                    join s in _context.tbl_stage on prog.stage_id equals s.id into sta
                    from stg in sta.DefaultIfEmpty()
                    join stud in _context.tbl_student on prp.student_id equals stud.id
                    join institute in _context.tbl_institute on stud.institute equals institute.id into inst
                    from institute in inst.DefaultIfEmpty()
                    join cl in _context.tbl_class on stud.class_id equals cl.id into classJoin
                    from cl in classJoin.DefaultIfEmpty()
                    join di in _context.tbl_division on stud.division_id equals di.id into divisionJoin
                    from di in divisionJoin.DefaultIfEmpty()
                    where prp.prgm_id == program_id && prp.status != "deleted"
                        && (institute_id == null || stud.institute == institute_id)
                        && (stage_id == null || prog.stage_id == stage_id)
                        && (date == null || prog.date == date)
                        && (admsn_no == null || stud.admsn_no == admsn_no)
                        && (item_code == null || prog.item_code == item_code)
                        && (status == null || prog.status == status)
                              && (ac_year_id == null || stud.ac_year_id == ac_year_id)
                    select (object)new
                    {
                        programstatus = prog.status,
                        program = prog.program_name,
                        no_of_group = prog.no_of_group,
                        stage_id = prog.stage_id,
                        stage = stg.stage_name,
                        participant_type = prog.participant_type,
                        gender = prog.gender,
                        program_type = prog.program_type,
                        date = prog.date,
                        time = prog.time,
                        student_id = prp.student_id,
                        chess_no = prp.chess_no,
                        name = stud.name,
                        institute_id = stud.institute,
                        institute_name = institute != null ? institute.name : null,
                        admsn_no = stud.admsn_no,
                        email = stud.email,
                        phone_no = stud.phone_no,
                        class_id = stud.class_id,
                        division_id = stud.division_id,
                        image = stud.image,
                        imageurl = !string.IsNullOrEmpty(stud.image) ? $"{baseUrl}uploads/student/{stud.image}" : null,
                        class_name = cl != null ? cl.@class : null,
                        division_name = di != null ? di.division : null,
                        id = prp.prgm_id,
                        status = prp.program_status,
                        token_no = prp.token_no,
                        item_code = prog.item_code,
                        group_name = (string?)null,
                        total_points = prp.average_point ?? 0,
                        ac_year_id= stud.ac_year_id
                    }
                ).ToListAsync();
            }

            // keyword filtering
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.ToLower();
                queryResult = queryResult
                    .Select(p => (dynamic)p)
                    .Where(x =>
                        (!string.IsNullOrEmpty(x.name) && x.name.ToLower().Contains(keyword)) ||
                        (!string.IsNullOrEmpty(x.admsn_no) && x.admsn_no.ToLower().Contains(keyword))
                    )
                    .Cast<object>()
                    .ToList();
            }

            var result = queryResult
                .Select(p => (dynamic)p)
                .GroupBy(x => new { x.id, x.student_id })
                .Select(g => g.First())
                .Select(x => new
                {
                    event_name = eventData.event_name,
                    program = x.program,
                    programstatus = x.programstatus,
                    no_of_group = x.no_of_group,
                    stage_id = x.stage_id,
                    stage = x.stage,
                    participant_type = x.participant_type,
                    gender = x.gender,
                    program_type = x.program_type,
                    date = x.date,
                    time = x.time,
                    chess_no = x.chess_no,
                    name = x.name,
                    admsn_no = x.admsn_no,
                    email = x.email,
                    phone_no = x.phone_no,
                    class_id = x.class_id,
                    division_id = x.division_id,
                    class_name = x.class_name,
                    division_name = x.division_name,
                    institute_id = x.institute_id,
                    institute_name = x.institute_name,
                    token_no = x.token_no,
                    item_code = x.item_code,
                    group_name = x.group_name,
                    image = x.image,
                    imageurl = !string.IsNullOrEmpty(x.image) ? $"{baseUrl}uploads/student/{x.image}" : null,
                    total_points = x.total_points,
                    id = x.id,
                    student_id = x.student_id,
                    status = x.status,
                    ac_year_id=x.ac_year_id
                })
                .ToList();
            var verificationPendingCount = result.Count(x =>
                string.IsNullOrEmpty(x.status?.ToString()) ||
                x.status.ToString().ToLower() == "pending");
            if (!result.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success",count= verificationPendingCount, data = result });
        }



        [HttpDelete]
        [Route("Delete_program")]
        public async Task<IActionResult> Delete_program([FromForm] int id, [FromForm] int? deleted_by, [FromForm] string? deleted_type)
        {
            if (_context.tbl_program == null)
            {
                return Problem("Entity set 'AeDbContext.tbl_program' is null.");
            }

            var eventItem = await _context.tbl_program.FindAsync(id);

            if (eventItem == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            // Soft delete the program
            eventItem.delete_status = "deleted";
            eventItem.deleted_by = deleted_by;
            eventItem.deleted_type = deleted_type;
            eventItem.deletedon = DateTime.Now;

            _context.Entry(eventItem).State = EntityState.Modified;

            // Remove related judgement criteria
            var judgement = _context.tbl_prgm_judgement_criteria.Where(c => c.prgm_id == id);
            _context.tbl_prgm_judgement_criteria.RemoveRange(judgement);

            // Remove related participants
            var participants = _context.tbl_prgm_participants.Where(p => p.prgm_id == id);
            _context.tbl_prgm_participants.RemoveRange(participants);

            // Remove related points
            var points = _context.tbl_prgm_point.Where(p => p.prgm_id == id);
            _context.tbl_prgm_point.RemoveRange(points);
            //remove related group members
            var groupMembers = _context.tbl_group_members.Where(g => g.prgm_id == id);
            _context.tbl_group_members.RemoveRange(groupMembers);

            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data deleted successfully" });
        }

        //prgm participants


        //   [HttpPost]
        //   [Route("addprgmparticipants")]
        //   public async Task<ActionResult> addprgmparticipants([FromBody] List<prgm_participants> requestList, int? institute_id)
        //   {
        //       if (_context.tbl_prgm_participants == null)
        //           return Problem("Entity set 'tbl_prgm_participants' is null.");

        //       if (!ModelState.IsValid)
        //       {
        //           var errorMessages = ModelState.Values
        //               .SelectMany(v => v.Errors)
        //               .Select(e => e.ErrorMessage)
        //               .ToList();

        //           return Ok(new { status = false, message = string.Join(", ", errorMessages) });
        //       }

        //       var groupedParticipants = new Dictionary<int, List<prgm_participants>>();

        //       // Group participants by institute
        //       foreach (var item in requestList)
        //       {
        //           var student = await _context.tbl_student
        //               .Where(s => s.id == item.student_id)
        //               .Select(s => new { s.institute, s.gender })
        //               .FirstOrDefaultAsync();

        //           if (student?.institute == null)
        //           {
        //               return Ok(new { status = false, message = $"Student not found or has no institute: {item.student_id}" });
        //           }

        //           if (!groupedParticipants.ContainsKey(student.institute.Value))
        //           {
        //               groupedParticipants[student.institute.Value] = new List<prgm_participants>();
        //           }

        //           groupedParticipants[student.institute.Value].Add(item);
        //       }

        //       // Process each institute group
        //       foreach (var group in groupedParticipants)
        //       {
        //           int institute = group.Key;
        //           var participants = group.Value;
        //           var prgmId = participants.First().prgm_id;

        //           // Get program details
        //           var program = await _context.tbl_program
        //               .Where(p => p.id == prgmId && p.delete_status != "deleted")
        //               .Select(p => new {
        //                   p.program_type,
        //                   p.gender,
        //                   p.group_min_participants,
        //                   p.group_max_participants,
        //                   p.status,
        //               })
        //               .FirstOrDefaultAsync();

        //           if (program == null)
        //               return Ok(new { status = false, message = $"Program not found for prgm_id: {prgmId}" });

        //           //
        //           if (program.status.ToLower() != "pending")
        //           {
        //               return Ok(new
        //               {
        //                   status = false,
        //                   message = $"Cannot add participants. Program status is '{program.status}'. Only programs with 'pending' status allow participant additions."
        //               });
        //           }
        //           //


        //           // Validate participants
        //           foreach (var participant in participants)
        //           {
        //               //gender
        //               var studentGender = await _context.tbl_student
        //                   .Where(s => s.id == participant.student_id)
        //                   .Select(s => s.gender)
        //                   .FirstOrDefaultAsync();

        //               if (studentGender == null)
        //                   return Ok(new { status = false, message = $"Student not found: {participant.student_id}" });

        //               string programGender = program.gender?.Trim().ToLower().Replace("s", "");
        //               string studentGenderNorm = studentGender?.Trim().ToLower();

        //               if (!string.IsNullOrEmpty(programGender) &&
        //programGender != "common" &&
        //!string.IsNullOrEmpty(studentGenderNorm) &&
        //programGender != studentGenderNorm)
        //               {
        //                   return Ok(new
        //                   {
        //                       status = false,
        //                       message = $"This program is only for {program.gender} students."
        //                   });
        //               }
        //               //
        //               bool exists = await _context.tbl_prgm_participants.AnyAsync(x =>
        //                   x.student_id == participant.student_id &&
        //                   x.prgm_id == participant.prgm_id &&
        //                   x.status != "deleted");

        //               if (exists)
        //               {
        //                   return Ok(new
        //                   {
        //                       status = false,
        //                       message = $"Student {participant.student_id} is already registered for this program."
        //                   });
        //               }

        //               bool existsInGroup = await _context.tbl_group_members.AnyAsync(g =>
        //                   g.prgm_id == participant.prgm_id &&
        //                   g.student_id == participant.student_id);

        //               if (existsInGroup)
        //               {
        //                   return Ok(new
        //                   {
        //                       status = false,
        //                       message = $"Student {participant.student_id} is already a group member for this program."
        //                   });
        //               }
        //           }

        //           int existingCount = await (from p in _context.tbl_prgm_participants
        //                                      join s in _context.tbl_student on p.student_id equals s.id
        //                                      where p.prgm_id == prgmId &&
        //                                            s.institute == institute &&
        //                                            p.status != "deleted"
        //                                      select p).CountAsync();

        //           int newParticipantCount = participants.Count;
        //           int totalCount = existingCount + newParticipantCount;

        //           var programType = program.program_type.ToLower();

        //           if (programType == "group" || programType == "solo")
        //           {
        //               // Shared validation logic
        //               //if (existingCount == 0 && newParticipantCount < program.group_min_participants)
        //               //{
        //               //    return Ok(new
        //               //    {
        //               //        status = false,
        //               //        message = $"Minimum {program.group_min_participants} participants are required for initial registration from institute {institute}."
        //               //    });
        //               //}

        //               if (totalCount > program.group_max_participants)
        //               {
        //                   return Ok(new
        //                   {
        //                       status = false,
        //                       message = $"Maximum {program.group_max_participants} participants allowed from institute {institute}. Current: {existingCount}, Adding: {newParticipantCount}"
        //                   });
        //               }
        //           }

        //           if (programType == "group")
        //           {
        //               if (existingCount == 0)
        //               {
        //                   // First-time group registration: add leader to tbl_prgm_participants
        //                   var leader = participants.First();

        //                   _context.tbl_prgm_participants.Add(new prgm_participants
        //                   {
        //                       prgm_id = leader.prgm_id,
        //                       student_id = leader.student_id,
        //                       addedon = DateTime.Now,
        //                       status = "active",
        //                       program_status = "pending",
        //                       chess_no = leader.chess_no
        //                   });

        //                   // Add all members to group table
        //                   foreach (var member in participants)
        //                   {
        //                       _context.tbl_group_members.Add(new group_members
        //                       {
        //                           prgm_id = member.prgm_id,
        //                           student_id = member.student_id
        //                       });
        //                   }
        //               }
        //               else
        //               {
        //                   // Adding new members to existing group
        //                   foreach (var member in participants)
        //                   {
        //                       _context.tbl_group_members.Add(new group_members
        //                       {
        //                           prgm_id = member.prgm_id,
        //                           student_id = member.student_id
        //                       });
        //                   }
        //               }
        //           }
        //           else
        //           {
        //               // Solo participant logic
        //               foreach (var p in participants)
        //               {
        //                   _context.tbl_prgm_participants.Add(new prgm_participants
        //                   {
        //                       prgm_id = p.prgm_id,
        //                       student_id = p.student_id,
        //                       addedon = DateTime.Now,
        //                       status = "active",
        //                       program_status = "pending",
        //                       chess_no = p.chess_no
        //                   });
        //               }
        //           }

        //           await _context.SaveChangesAsync();
        //       }

        //       return Ok(new { status = true, message = "Data added successfully." });
        //   }

        [HttpPost]
        [Route("addprgmparticipants")]
        public async Task<ActionResult> addprgmparticipants([FromBody] List<prgm_participants> requestList)
        {
            if (_context.tbl_prgm_participants == null)
                return Problem("Entity set 'tbl_prgm_participants' is null.");

            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Ok(new { status = false, message = string.Join(", ", errorMessages) });
            }

            var groupedParticipants = new Dictionary<int, List<prgm_participants>>();

            // Group participants by institute
            foreach (var item in requestList)
            {
                var student = await _context.tbl_student
                    .Where(s => s.id == item.student_id)
                    .Select(s => new { s.institute, s.gender, s.category })
                    .FirstOrDefaultAsync();

                if (student?.institute == null)
                {
                    return Ok(new { status = false, message = $"Student not found or has no institute: {item.student_id}" });
                }

                if (!groupedParticipants.ContainsKey(student.institute.Value))
                {
                    groupedParticipants[student.institute.Value] = new List<prgm_participants>();
                }

                groupedParticipants[student.institute.Value].Add(item);
            }

            // --- PARTICIPATION LIMIT VALIDATION PER STUDENT ---
            foreach (var studentGroup in requestList.GroupBy(x => x.student_id))
            {
                int studentId = studentGroup.Key ?? 0; // handle nulls if necessary

                int requestedSoloPrograms = 0;
                int requestedGroupPrograms = 0;

                // Count requested programs in current batch
                foreach (var item in studentGroup)
                {
                    var programType = await _context.tbl_program
                        .Where(p => p.id == item.prgm_id && p.delete_status != "deleted")
                        .Select(p => p.program_type)
                        .FirstOrDefaultAsync();

                    if (programType != null)
                    {
                        if (programType.ToLower() == "solo") requestedSoloPrograms++;
                        else if (programType.ToLower() == "group") requestedGroupPrograms++;
                    }
                }

                // Count existing solo registrations in DB
                int existingSolo = await _context.tbl_prgm_participants
                    .Where(p => p.student_id == studentId && p.status != "deleted")
                    .Join(_context.tbl_program,
                          pp => pp.prgm_id,
                          pr => pr.id,
                          (pp, pr) => pr.program_type)
                    .CountAsync(t => t.ToLower() == "solo");

                // Count existing group registrations via tbl_group_members
                int existingGroup = await _context.tbl_group_members
                    .Where(gm => gm.student_id == studentId)
                    .Join(_context.tbl_program,
                          gm => gm.prgm_id,
                          pr => pr.id,
                          (gm, pr) => pr.program_type)
                    .CountAsync(t => t.ToLower() == "group");

                // Validation
                if (requestedSoloPrograms + existingSolo > 3)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "A student can only participate in up to 3 solo programs."
                    });
                }

                if (requestedGroupPrograms + existingGroup > 2)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "A student can only participate in up to 2 group programs."
                    });
                }
            }

            // Collects "add N more students" messages for groups still below the minimum
            var pendingMessages = new List<string>();

            // Process each institute group
            foreach (var group in groupedParticipants)
            {
                int institute = group.Key;
                var participants = group.Value;
                var prgmId = participants.First().prgm_id;
                var group_name = participants.First().group_name;

                // Get program details
                var program = await _context.tbl_program
                    .Where(p => p.id == prgmId && p.delete_status != "deleted")
                    .Select(p => new
                    {
                        p.program_type,
                        p.gender,
                        p.group_min_participants,
                        p.group_max_participants,
                        p.status,
                        p.participant_type
                    })
                    .FirstOrDefaultAsync();

                if (program == null)
                    return Ok(new { status = false, message = $"Program not found for prgm_id: {prgmId}" });

                // GROUP NAME VALIDATION
                if (program.program_type != null &&
                    program.program_type.Trim().ToLower() == "group")
                {
                    if (string.IsNullOrWhiteSpace(group_name))
                    {
                        return Ok(new
                        {
                            status = false,
                            message = "Group name is required for group programs."
                        });
                    }
                }

                if (program.status.ToLower() != "pending")
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Cannot add participants. Program status is '{program.status}'. Only programs with 'pending' status allow participant additions."
                    });
                }

                // Validate participants
                foreach (var participant in participants)
                {
                    var studentGender = await _context.tbl_student
                        .Where(s => s.id == participant.student_id)
                        .Select(s => new { s.gender, s.category })
                        .FirstOrDefaultAsync();

                    if (studentGender == null)
                        return Ok(new { status = false, message = $"Student not found: {participant.student_id}" });

                    string programGender = program.gender?.Trim().ToLower().Replace("s", "");
                    string studentGenderNorm = studentGender.gender?.Trim().ToLower();

                    if (!string.IsNullOrEmpty(programGender) &&
                        programGender != "common" &&
                        !string.IsNullOrEmpty(studentGenderNorm) &&
                        programGender != studentGenderNorm)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"This program is only for {program.gender} students."
                        });
                    }

                    if (!string.Equals(studentGender.category?.Trim(), program.participant_type?.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"Student {participant.student_id} category '{studentGender.category}' does not match program participant type '{program.participant_type}'."
                        });
                    }

                    bool exists = await _context.tbl_prgm_participants.AnyAsync(x =>
                        x.student_id == participant.student_id &&
                        x.prgm_id == participant.prgm_id &&
                        x.status != "deleted");

                    if (exists)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = "Student is already registered for this program."
                        });
                    }

                    bool existsInGroup = await _context.tbl_group_members.AnyAsync(g =>
                        g.prgm_id == participant.prgm_id &&
                        g.student_id == participant.student_id);

                    if (existsInGroup)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"Student {participant.student_id} is already a group member for this program."
                        });
                    }
                }

                int existingCount = await (from p in _context.tbl_prgm_participants
                                           join s in _context.tbl_student on p.student_id equals s.id
                                           where p.prgm_id == prgmId &&
                                                 s.institute == institute &&
                                                 p.status != "deleted"
                                           select p).CountAsync();

                int newParticipantCount = participants.Count;
                int totalCount = existingCount + newParticipantCount;

                var programType = program.program_type.ToLower();

                if (programType == "solo")
                {
                    if (totalCount > program.group_max_participants)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"Maximum {program.group_max_participants} participants allowed from institute {institute}. Current: {existingCount}, Adding: {newParticipantCount}"
                        });
                    }
                }

                if (programType == "group")
                {
                    // Fetch program details
                    var programDetails = await _context.tbl_program
                        .Where(p => p.id == prgmId)
                        .Select(p => new
                        {
                            p.group_max_participants,
                            p.no_of_group
                        })
                        .FirstOrDefaultAsync();

                    if (programDetails == null)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = $"Program not found for ID {prgmId}"
                        });
                    }

                    int noOfGroup = Convert.ToInt32(programDetails.no_of_group);
                    int maxParticipants = Convert.ToInt32(programDetails.group_max_participants);

                    if (noOfGroup > 1)
                    {
                        // Count current members in this group_name for this institute
                        var currentCount = await (from gm in _context.tbl_group_members
                                                  join s in _context.tbl_student on gm.student_id equals s.id
                                                  where gm.prgm_id == prgmId
                                                        && gm.group_name == group_name
                                                        && s.institute == institute
                                                  select gm.id).CountAsync();

                        // Validate total count
                        if (currentCount >= maxParticipants)
                        {
                            return Ok(new
                            {
                                status = false,
                                message = $"Cannot add members '{group_name}' already has {currentCount} participants from institute."
                            });
                        }
                    }

                    // Add all members to tbl_group_members
                    foreach (var member in participants)
                    {
                        var studentData = await _context.tbl_student
                            .Where(s => s.id == member.student_id)
                            .Select(s => new { s.institute })
                            .FirstOrDefaultAsync();

                        _context.tbl_group_members.Add(new group_members
                        {
                            prgm_id = member.prgm_id,
                            student_id = member.student_id,
                            verify_status = "pending",
                            group_name = group_name,
                            instit_id = studentData != null ? studentData.institute : null
                        });
                    }

                    // Add ONE entry in tbl_prgm_participants per (program + group_name + institute)
                    bool groupInstituteExists = await (from p in _context.tbl_prgm_participants
                                                       join s in _context.tbl_student on p.student_id equals s.id
                                                       where p.prgm_id == prgmId
                                                             && p.group_name == group_name
                                                             && s.institute == institute
                                                             && p.status != "deleted"
                                                       select p).AnyAsync();

                    if (!groupInstituteExists && participants.Any())
                    {
                        var leader = participants.First(); // first student as representative
                        _context.tbl_prgm_participants.Add(new prgm_participants
                        {
                            prgm_id = leader.prgm_id,
                            student_id = leader.student_id,
                            addedon = DateTime.Now,
                            status = "active",
                            program_status = "pending",
                            chess_no = leader.chess_no,
                            group_name = group_name,
                            ac_year_id = leader.ac_year_id
                        });
                    }

                    await _context.SaveChangesAsync();

                    // --- MINIMUM GROUP MEMBER CHECK ---
                    // Runs after the save, so the count already includes the newly added members.
                    int minParticipants = Convert.ToInt32(program.group_min_participants);

                    int currentGroupMemberCount = await (
                        from gm in _context.tbl_group_members
                        join s2 in _context.tbl_student on gm.student_id equals s2.id
                        where gm.prgm_id == prgmId
                              && gm.group_name == group_name
                              && s2.institute == institute
                        select gm.id
                    ).CountAsync();

                    int remaining = minParticipants - currentGroupMemberCount;

                    if (remaining > 0)
                    {
                        pendingMessages.Add(
                            $"You need to add {remaining} more student{(remaining > 1 ? "s" : "")} to group '{group_name}'");
                    }
                }
                else
                {
                    // Solo participant logic
                    foreach (var p in participants)
                    {
                        _context.tbl_prgm_participants.Add(new prgm_participants
                        {
                            prgm_id = p.prgm_id,
                            student_id = p.student_id,
                            addedon = DateTime.Now,
                            status = "active",
                            program_status = "pending",
                            chess_no = p.chess_no,
                            ac_year_id = p.ac_year_id
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }

            // Participants are saved successfully; this is only a heads-up
            if (pendingMessages.Any())
            {
                return Ok(new { status = true, message = string.Join(". ", pendingMessages) });
            }

            return Ok(new { status = true, message = "Data added successfully." });
        }
        //
        [HttpPost]
       [Route("addprgmpoints")]

        // public async Task<ActionResult> addprgmpoints([FromBody] List<prgm_pointmodel> requestList)
        // {
        //     if (_context.tbl_prgm_point == null)
        //         return Problem("Entity set '_context.tbl_prgm_point' is null.");

        //     if (!ModelState.IsValid)
        //     {
        //         var errorMessages = ModelState.Values
        //             .SelectMany(v => v.Errors)
        //             .Select(e => e.ErrorMessage)
        //             .ToList();

        //         return Ok(new { status = false, message = string.Join(", ", errorMessages) });
        //     }

        //     int prgmId = requestList.First().prgm_id.GetValueOrDefault();
        //     var errors = new List<string>();

        //     foreach (var request in requestList)
        //     {
        //         var maxAllowedPoint = await _context.tbl_prgm_judgement_criteria
        //             .Where(j => j.prgm_id == request.prgm_id && j.judgement_criteria == request.judgement_criteria_id)
        //             .Select(j => j.point)
        //             .FirstOrDefaultAsync();

        //         if (maxAllowedPoint == 0)
        //         {
        //             errors.Add($"Criteria ID {request.judgement_criteria_id}: Not set or has 0 max points.");
        //             continue;
        //         }

        //         if (request.point > maxAllowedPoint)
        //         {
        //             errors.Add($"Criteria ID {request.judgement_criteria_id}: Entered {request.point} exceeds max {maxAllowedPoint}.");
        //             continue;
        //         }

        //         // Check if judge has already entered marks for this student, program, and criteria
        //         bool alreadyExists = await _context.tbl_prgm_point.AnyAsync(p =>
        //             p.prgm_id == request.prgm_id &&
        //             p.student_id == request.student_id &&
        //             p.judge_id == request.judge_id &&
        //             p.judgement_criteria_id == request.judgement_criteria_id);

        //         if (alreadyExists)
        //         {
        //             errors.Add($"Duplicate entry found: Judge  has already entered marks for Student , Program , Criteria.");
        //             continue;
        //         }

        //         _context.tbl_prgm_point.Add(new prgm_pointmodel
        //         {
        //             prgm_id = request.prgm_id,
        //             student_id = request.student_id,
        //             judge_id = request.judge_id,
        //             chess_no = request.chess_no,
        //             addedon = DateTime.Now,
        //             judgement_criteria_id = request.judgement_criteria_id,
        //             point = request.point,
        //             comments = request.comments
        //         });
        //     }

        //     if (errors.Any())
        //     {
        //         return Ok(new { status = false, message = "Some entries failed validation.", errors = errors });
        //     }

        //     await _context.SaveChangesAsync();

        //     // Get program details
        //     var program = await _context.tbl_program
        //         .Where(p => p.id == prgmId)
        //         .FirstOrDefaultAsync();

        //     if (program == null)
        //         return Ok(new { status = false, message = "Program not found." });

        //     int noOfJudges = await _context.tbl_judge_prgm
        //         .Where(j => j.program_id == prgmId && j.status!= "deleted")
        //         .Select(j => j.judge_id)
        //         .Distinct()
        //         .CountAsync();

        //     // Get participants of the program
        //     var participants = await _context.tbl_prgm_participants
        //         .Where(p => p.prgm_id == prgmId && p.chess_no != null && p.ispresent == null && p.status != "deleted")
        //         .ToListAsync();

        //     foreach (var participant in participants)
        //     {
        //         int studentId = participant.student_id ?? 0;

        //         var pointsQuery = _context.tbl_prgm_point
        //             .Where(p => p.prgm_id == prgmId && p.student_id == studentId);

        //         var totalPoints = await pointsQuery.SumAsync(p => (int?)p.point) ?? 0;
        //         var judgeCount = await pointsQuery.Select(p => p.judge_id).Distinct().CountAsync();
        //         //var averagePoints = judgeCount > 0 ? totalPoints / judgeCount : 0;

        //         var averagePoints = judgeCount > 0
        //? Math.Round((double)totalPoints / judgeCount, 2)
        //: 0;

        //         participant.average_point = averagePoints;

        //         //  Fetch actual program type for this participant
        //         var participantProgramType = await _context.tbl_program
        //             .Where(p => p.id == participant.prgm_id)
        //             .Select(p => p.program_type)
        //             .FirstOrDefaultAsync();

        //         int gradeTypeId = (participantProgramType != null && participantProgramType.ToLower().Trim() == "group") ? 2 : 1;

        //         //  Fetch correct grade for this average point and gradetype
        //         var grade = await _context.tbl_grade
        //             .Where(g => g.gradetype_id == gradeTypeId &&
        //                         averagePoints >= g.from_point &&
        //                         averagePoints <= g.to_point)
        //             .OrderByDescending(g => g.point)
        //             .FirstOrDefaultAsync();

        //         if (grade != null)
        //         {
        //             participant.grade = grade.grade;
        //             participant.grade_point = grade.point;
        //         }

        //         _context.Entry(participant).State = EntityState.Modified;
        //     }

        //     await _context.SaveChangesAsync();

        //     // Check if all judges have submitted scores for each student
        //     bool allScored = true;

        //     foreach (var student in participants)
        //     {
        //         var distinctJudgeCount = await _context.tbl_prgm_point
        //             .Where(p => p.prgm_id == prgmId && p.student_id == student.student_id)
        //             .Select(p => p.judge_id)
        //             .Distinct()
        //             .CountAsync();

        //         if (distinctJudgeCount < noOfJudges)
        //         {
        //             allScored = false;
        //             break;
        //         }
        //     }

        //     if (allScored)
        //     {
        //         // Update program status
        //         program.status = "Completed";
        //         _context.Entry(program).State = EntityState.Modified;

        //         // Update each participant's program_status
        //         foreach (var p in participants)
        //         {
        //             p.program_status = "Judgement Completed";
        //             _context.Entry(p).State = EntityState.Modified;
        //         }

        //         await _context.SaveChangesAsync();

        //         // Call rank update method
        //         await UpdateRanksAndPointsForProgram(prgmId);
        //     }

        //     return Ok(new { status = true, message = "Data added successfully" });
        // }

   
        public async Task<ActionResult> addprgmpoints([FromBody] List<prgm_pointmodel> requestList)
        {
            if (_context.tbl_prgm_point == null)
                return Problem("Entity set '_context.tbl_prgm_point' is null.");

            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Ok(new { status = false, message = string.Join(", ", errorMessages) });
            }

            int prgmId = requestList.First().prgm_id.GetValueOrDefault();
            var errors = new List<string>();

            foreach (var request in requestList)
            {
                var maxAllowedPoint = await _context.tbl_prgm_judgement_criteria
                    .Where(j => j.prgm_id == request.prgm_id && j.judgement_criteria == request.judgement_criteria_id)
                    .Select(j => j.point)
                    .FirstOrDefaultAsync();

                if (maxAllowedPoint == 0)
                {
                    errors.Add($"Criteria ID {request.judgement_criteria_id}: Not set or has 0 max points.");
                    continue;
                }

                if (request.point > maxAllowedPoint)
                {
                    errors.Add($"Criteria ID {request.judgement_criteria_id}: Entered {request.point} exceeds max {maxAllowedPoint}.");
                    continue;
                }


                // Check if the student is marked absent
                bool isAbsent = await _context.tbl_prgm_participants.AnyAsync(p =>
                    p.prgm_id == request.prgm_id &&
                    p.student_id == request.student_id &&
                    p.ispresent != null &&
                    p.ispresent.ToLower() == "absent");

                if (isAbsent)
                {
                    errors.Add($"Chest No {request.chess_no}: This student has already been marked as absent. Marks cannot be entered.");
                    continue;
                }
                // Check if judge has already entered marks for this student, program, and criteria
                bool alreadyExists = await _context.tbl_prgm_point.AnyAsync(p =>
                    p.prgm_id == request.prgm_id &&
                    p.student_id == request.student_id &&
                    p.judge_id == request.judge_id &&
                    p.judgement_criteria_id == request.judgement_criteria_id);

                if (alreadyExists)
                {
                    errors.Add($"Duplicate entry found: Judge  has already entered marks for Student , Program , Criteria.");
                    continue;
                }

                _context.tbl_prgm_point.Add(new prgm_pointmodel
                {
                    prgm_id = request.prgm_id,
                    student_id = request.student_id,
                    judge_id = request.judge_id,
                    chess_no = request.chess_no,
                    addedon = DateTime.Now,
                    judgement_criteria_id = request.judgement_criteria_id,
                    point = request.point,
                    comments = request.comments
                });
            }

            if (errors.Any())
            {
                return Ok(new { status = false, message = "Some entries failed validation.", errors = errors });
            }

            await _context.SaveChangesAsync();

            // Get program details
            var program = await _context.tbl_program
                .Where(p => p.id == prgmId)
                .FirstOrDefaultAsync();

            if (program == null)
                return Ok(new { status = false, message = "Program not found." });
            int? acYearId = program.ac_year_id;

            int noOfJudges = await _context.tbl_judge_prgm
                .Where(j => j.program_id == prgmId && j.status != "deleted")
                .Select(j => j.judge_id)
                .Distinct()
                .CountAsync();

            // Get participants of the program
            var participants = await _context.tbl_prgm_participants
                .Where(p => p.prgm_id == prgmId && p.chess_no != null && p.ispresent == null && p.status != "deleted")
                .ToListAsync();

            foreach (var participant in participants)
            {
                int studentId = participant.student_id ?? 0;

                var pointsQuery = _context.tbl_prgm_point
                    .Where(p => p.prgm_id == prgmId && p.student_id == studentId);

                var totalPoints = await pointsQuery.SumAsync(p => (int?)p.point) ?? 0;
                var judgeCount = await pointsQuery.Select(p => p.judge_id).Distinct().CountAsync();
                //var averagePoints = judgeCount > 0 ? totalPoints / judgeCount : 0;

                var averagePoints = judgeCount > 0
       ? Math.Round((double)totalPoints / judgeCount, 2)
       : 0;
                participant.average_point = averagePoints;

                //  Fetch actual program type for this participant
                var participantProgramType = await _context.tbl_program
                    .Where(p => p.id == participant.prgm_id)
                    .Select(p => p.program_type)
                    .FirstOrDefaultAsync();

                int gradeTypeId = (participantProgramType != null && participantProgramType.ToLower().Trim() == "group") ? 2 : 1;

                //  Fetch correct grade for this average point and gradetype
                //     var grade = await _context.tbl_grade
                //.Where(g => g.ac_year_id == acYearId &&
                //            g.gradetype_id == gradeTypeId &&
                //            averagePoints >= g.from_point &&
                //            averagePoints <= g.to_point)
                //.OrderByDescending(g => g.point)
                //.FirstOrDefaultAsync();

                //     if (grade != null)
                //     {
                //         participant.grade = grade.grade;
                //         participant.grade_point = grade.point;
                //     }

                //     _context.Entry(participant).State = EntityState.Modified;
                // If average is below 40, don't assign any grade or grade point
                if (averagePoints < 40)
                {
                    participant.grade = null;      // or "" if grade column is string and not nullable
                    participant.grade_point = 0;   // or null if nullable
                }
                else
                {
                    var grade = await _context.tbl_grade
                        .Where(g => g.ac_year_id == acYearId &&
                                    g.gradetype_id == gradeTypeId &&
                                    averagePoints >= g.from_point &&
                                    averagePoints <= g.to_point)
                        .OrderByDescending(g => g.point)
                        .FirstOrDefaultAsync();


                    if (grade != null)
                    {
                        participant.grade = grade.grade;
                        participant.grade_point = grade.point;
                    }
                    else
                    {
                        participant.grade = null;
                        participant.grade_point = 0;
                    }
                }

                _context.Entry(participant).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            // Check if all judges have submitted scores for each student
            bool allScored = true;

            foreach (var student in participants)
            {
                var distinctJudgeCount = await _context.tbl_prgm_point
                    .Where(p => p.prgm_id == prgmId && p.student_id == student.student_id)
                    .Select(p => p.judge_id)
                    .Distinct()
                    .CountAsync();

                if (distinctJudgeCount < noOfJudges)
                {
                    allScored = false;
                    break;
                }
            }

            if (allScored)
            {
                // Update program status
                program.status = "Completed";
                _context.Entry(program).State = EntityState.Modified;

                // Update each participant's program_status
                foreach (var p in participants)
                {
                    p.program_status = "Judgement Completed";
                    _context.Entry(p).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync();

                // Call rank update method
                await UpdateRanksAndPointsForProgram(prgmId);
            }

            return Ok(new { status = true, message = "Data added successfully" });
        }



        //status updation in prgm_participants

        [HttpPut]
        [Route("update_status")]
        public async Task<ActionResult> update_status([FromForm] int? program_id, [FromForm] int? student_id, [FromForm] string? status, [FromForm] int? user_id)
        {
            if (program_id == null)
                return BadRequest(new { status = false, message = "program_id is required" });

            if (string.IsNullOrEmpty(status))
                return BadRequest(new { status = false, message = "Status is required" });

            var participants = await _context.tbl_prgm_participants
                .Where(p => p.prgm_id == program_id && p.student_id == student_id && p.status.ToLower() != "deleted")
                .ToListAsync();

            // Get the program
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == program_id && p.delete_status.ToLower() == "active");


            if (participants == null || !participants.Any())
                return NotFound(new { status = false, message = "No active participants found for this program." });

            foreach (var participant in participants)
            {
                //  "judgement sheet prepared" only after "chest number generated"
                if (status.ToLower() == "judgement sheet prepared")
                {
                    if (participant.program_status?.ToLower() != "Chest Number Generated")
                    {
                        return BadRequest(new
                        {
                            status = false,
                            message = "Cannot update to 'judgement sheet prepared' unless the current status is 'chest number generated'."
                        });
                    }
                }

                participant.program_status = status;
                participant.user_id = user_id;
                participant.status_updated = DateTime.Now;
            }

            //  program.status = status;


            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Status updated successfully"
            });
        }


        //generate chest_no
        [HttpPut]
        [Route("generate_chestno")]
        //public async Task<ActionResult> generate_chestno([FromForm] int? program_id, [FromForm] int? student_id, [FromForm] string? chest_no)
        //{
        //    if (program_id == null)
        //    {
        //        return Ok(new { status = false, message = "program is required" });
        //    }

        //    if (student_id == null)
        //    {
        //        return Ok(new { status = false, message = "student is required" });
        //    }

        //    if (string.IsNullOrEmpty(chest_no))
        //    {
        //        return Ok(new { status = false, message = "chest number is required" });
        //    }

        //    // Check if the chest number already exists for the same program
        //    bool chestExists = await _context.tbl_prgm_participants
        //        .AnyAsync(p => p.chess_no == chest_no && p.status != "deleted");

        //    if (chestExists)
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = $"Chest number '{chest_no}' is already assigned.Please choose a different one."
        //        });
        //    }

        //    var participant = await _context.tbl_prgm_participants
        //        .FirstOrDefaultAsync(p => p.prgm_id == program_id && p.student_id == student_id && p.status != "deleted");

        //    if (participant == null)
        //    {
        //        return Ok(new { status = false, message = "Participant not found for the given program and student" });
        //    }
        //    // Check program_status
        //    if (participant.program_status != "Verification Completed")
        //    //string expected = StatusEnum.GetStudentLabel(StatusEnum.StudentStatus.StudentIDVerification);
        //    //if (string.IsNullOrWhiteSpace(participant.program_status) ||
        //    //    !string.Equals(participant.program_status.Trim(), expected, StringComparison.OrdinalIgnoreCase))


        //    {
        //        return Ok(new { status = false, message = "Student not verified" });
        //    }



        //    //  Check if chest number already exists
        //    if (!string.IsNullOrEmpty(participant.chess_no))
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Chest number already assigned for this student in the selected program."
        //        });
        //    }

        //    // Assign chest number
        //    participant.chess_no = chest_no;

        //    participant.program_status = "Chest Number Generated";
        //    //  participant.program_status = StatusEnum.StudentStatus.ChestNumberGenerated.ToString();


        //    await _context.SaveChangesAsync();

        //    return Ok(new
        //    {
        //        status = true,
        //        message = "Chest number generated successfully"
        //    });
        //}



        //public async Task<ActionResult> generate_chestno([FromForm] int? program_id, [FromForm] int? student_id, [FromForm] string? chest_no)
        //{
        //    if (program_id == null)
        //    {
        //        return Ok(new { status = false, message = "program is required" });
        //    }

        //    if (student_id == null)
        //    {
        //        return Ok(new { status = false, message = "student is required" });
        //    }

        //    if (string.IsNullOrEmpty(chest_no))
        //    {
        //        return Ok(new { status = false, message = "chest number is required" });
        //    }

        //    // Check if the chest number already exists for the same program
        //    bool chestExists = await _context.tbl_prgm_participants
        //        .AnyAsync(p => p.chess_no == chest_no && p.status != "deleted");

        //    if (chestExists)
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = $"Chest number '{chest_no}' is already assigned.Please choose a different one."
        //        });
        //    }

        //    var participant = await _context.tbl_prgm_participants
        //        .FirstOrDefaultAsync(p => p.prgm_id == program_id && p.student_id == student_id && p.status != "deleted");

        //    if (participant == null)
        //    {
        //        return Ok(new { status = false, message = "Participant not found for the given program and student" });
        //    }
        //    // Check program_status
        //    if (participant.program_status != "Verification Completed")
        //    //string expected = StatusEnum.GetStudentLabel(StatusEnum.StudentStatus.StudentIDVerification);
        //    //if (string.IsNullOrWhiteSpace(participant.program_status) ||
        //    //    !string.Equals(participant.program_status.Trim(), expected, StringComparison.OrdinalIgnoreCase))


        //    {
        //        return Ok(new { status = false, message = "Student not verified" });
        //    }



        //    //  Check if chest number already exists
        //    if (!string.IsNullOrEmpty(participant.chess_no))
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Chest number already assigned for this student in the selected program."
        //        });
        //    }

        //    // Assign chest number
        //    participant.chess_no = chest_no;

        //    participant.program_status = "Chest Number Generated";
        //    //  participant.program_status = StatusEnum.StudentStatus.ChestNumberGenerated.ToString();


        //    // Step 1: get institute_id from tbl_student
        //    var student = await _context.tbl_student
        //        .FirstOrDefaultAsync(s => s.id == participant.student_id);

        //    if (student == null)
        //    {
        //        return Ok(new { status = false, message = "Student not found" });
        //    }

        //    var instituteId = student.institute;
        //    var groupName = participant.group_name;

        //    // Step 2: get group members
        //    var groupMembers = await _context.tbl_group_members
        //        .Where(g => g.group_name == groupName
        //                 && g.instit_id == instituteId
        //                 && g.prgm_id == program_id)
        //        .ToListAsync();

        //    // Step 3: update chest number
        //    foreach (var member in groupMembers)
        //    {
        //        member.chest_no = chest_no;
        //    }

        //    await _context.SaveChangesAsync();

        //    return Ok(new
        //    {
        //        status = true,
        //        message = "Chest number generated successfully"
        //    });
        //}

        //latest code
        //public async Task<ActionResult> generate_chestno([FromForm] int? program_id, [FromForm] int? student_id)
        //{
        //    if (program_id == null)
        //    {
        //        return Ok(new { status = false, message = "program is required" });
        //    }

        //    if (student_id == null)
        //    {
        //        return Ok(new { status = false, message = "student is required" });
        //    }

        //    var participant = await _context.tbl_prgm_participants
        //        .FirstOrDefaultAsync(p => p.prgm_id == program_id
        //                               && p.student_id == student_id
        //                               && p.status != "deleted");

        //    if (participant == null)
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Participant not found for the given program and student"
        //        });
        //    }

        //    // Check program status
        //    if (participant.program_status != "Verification Completed")
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Student not verified"
        //        });
        //    }

        //    // Check if chest number already assigned
        //    if (!string.IsNullOrEmpty(participant.chess_no))
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Chest number already assigned for this student in the selected program."
        //        });
        //    }

        //    int? acYearId = participant.ac_year_id;

        //    // Get chest numbers from participants table
        //    // Get chest numbers from participants table
        //    var participantNos = await _context.tbl_prgm_participants
        //        .Where(p => p.status != "deleted"
        //                 && p.ac_year_id == acYearId
        //                 && !string.IsNullOrEmpty(p.chess_no))
        //        .Select(p => p.chess_no)
        //        .ToListAsync();

        //    // Get chest numbers from group members table for same academic year
        //    var groupNos = await (
        //        from gm in _context.tbl_group_members
        //        join pp in _context.tbl_prgm_participants
        //            on new { gm.prgm_id, gm.group_name }
        //            equals new { pp.prgm_id, pp.group_name }
        //        where pp.ac_year_id == acYearId
        //           && !string.IsNullOrEmpty(gm.chest_no)
        //        select gm.chest_no
        //    ).Distinct().ToListAsync();

        //    // Combine both tables
        //    var allChestNos = participantNos
        //        .Concat(groupNos)
        //        .Where(x => int.TryParse(x, out _))
        //        .Select(int.Parse)
        //        .Distinct()
        //        .ToList();

        //    int nextNumber = allChestNos.Any()
        //        ? allChestNos.Max() + 1
        //        : 1;

        //    string chest_no = nextNumber.ToString("D3");
        //    // Assign chest number
        //    participant.chess_no = chest_no;
        //    participant.program_status = "Chest Number Generated";

        //    // Get student details
        //    var student = await _context.tbl_student
        //        .FirstOrDefaultAsync(s => s.id == participant.student_id);

        //    if (student == null)
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Student not found"
        //        });
        //    }

        //    var instituteId = student.institute;
        //    var groupName = participant.group_name;

        //    // Update chest number in group members table
        //    var groupMembers = await _context.tbl_group_members
        //        .Where(g => g.group_name == groupName
        //                 && g.instit_id == instituteId
        //                 && g.prgm_id == program_id)
        //        .ToListAsync();

        //    foreach (var member in groupMembers)
        //    {
        //        member.chest_no = chest_no;
        //    }

        //    await _context.SaveChangesAsync();

        //    return Ok(new
        //    {
        //        status = true,
        //        chest_no = chest_no,
        //        message = "Chest number generated successfully"
        //    });
        //}[HttpPost]

        ///latest code
    //    public async Task<ActionResult> generate_chestno([FromForm] int? program_id, [FromForm] int? student_id)
    //    {
    //        if (program_id == null)
    //        {
    //            return Ok(new { status = false, message = "program is required" });
    //        }

    //        if (student_id == null)
    //        {
    //            return Ok(new { status = false, message = "student is required" });
    //        }

    //        var participant = await _context.tbl_prgm_participants
    //            .FirstOrDefaultAsync(p => p.prgm_id == program_id
    //                                   && p.student_id == student_id
    //                                   && p.status != "deleted");

    //        if (participant == null)
    //        {
    //            return Ok(new
    //            {
    //                status = false,
    //                message = "Participant not found for the given program and student"
    //            });
    //        }

    //        if (participant.program_status != "Verification Completed")
    //        {
    //            return Ok(new
    //            {
    //                status = false,
    //                message = "Student not verified"
    //            });
    //        }

    //        if (!string.IsNullOrEmpty(participant.chess_no))
    //        {
    //            return Ok(new
    //            {
    //                status = false,
    //                message = "Chest number already assigned for this student in the selected program."
    //            });
    //        }

    //        int? acYearId = participant.ac_year_id;

    //        var program = await _context.tbl_program
    //            .FirstOrDefaultAsync(p => p.id == program_id);

    //        if (program == null)
    //        {
    //            return Ok(new
    //            {
    //                status = false,
    //                message = "Program not found"
    //            });
    //        }

    //        int stageId = program.stage_id ?? 1;

    //        // Stage 1 => 1001-1999
    //        // Stage 2 => 2001-2999
    //        // Stage 3 => 3001-3999
    //        int rangeStart = (stageId * 1000) + 1;
    //        int rangeEnd = (stageId * 1000) + 999;

    //        // Get all used chest numbers for same Academic Year + Stage
    //        string chest_no = "";

    //        lock (_chestLock)
    //        {

    //            var usedChestNos = (
    //                from pp in _context.tbl_prgm_participants
    //                join pr in _context.tbl_program
    //                    on pp.prgm_id equals pr.id
    //                where pp.status != "deleted"
    //                   && pp.ac_year_id == acYearId
    //                   && pr.stage_id == stageId
    //                   && !string.IsNullOrEmpty(pp.chess_no)
    //                select pp.chess_no
    //            ).ToList();

    //            var usedNumbers = usedChestNos
    //                .Where(x => int.TryParse(x, out _))
    //                .Select(int.Parse)
    //                .Where(x => x >= rangeStart && x <= rangeEnd)
    //                .Distinct()
    //                .ToHashSet();

    //            //int nextNumber = rangeStart;

    //            //while (usedNumbers.Contains(nextNumber))
    //            //{
    //            //    nextNumber++;
    //            //}

    //            int nextNumber = usedNumbers.Any()
    //? usedNumbers.Max() + 1
    //: rangeStart;
    //            if (nextNumber > rangeEnd)
    //            {
    //                throw new Exception($"Chest number limit reached for stage {stageId}");
    //            }

    //            chest_no = nextNumber.ToString();

    //            participant.chess_no = chest_no;
    //            participant.program_status = "Chest Number Generated";

    //            _context.SaveChanges();
    //        }
       

    //        var student = await _context.tbl_student
    //            .FirstOrDefaultAsync(s => s.id == participant.student_id);

    //        if (student == null)
    //        {
    //            return Ok(new
    //            {
    //                status = false,
    //                message = "Student not found"
    //            });
    //        }

    //        var instituteId = student.institute;
    //        var groupName = participant.group_name;

    //        var groupMembers = await _context.tbl_group_members
    //            .Where(g => g.group_name == groupName
    //                     && g.instit_id == instituteId
    //                     && g.prgm_id == program_id)
    //            .ToListAsync();

    //        foreach (var member in groupMembers)
    //        {
    //            member.chest_no = chest_no;
    //        }

    //        await _context.SaveChangesAsync();

    //        return Ok(new
    //        {
    //            status = true,
    //            chest_no = chest_no,
    //            message = "Chest number generated successfully"
    //        });
    //    }


        public async Task<ActionResult> generate_chestno([FromForm] int? program_id, [FromForm] int? student_id)
        {
            if (program_id == null)
            {
                return Ok(new { status = false, message = "program is required" });
            }

            if (student_id == null)
            {
                return Ok(new { status = false, message = "student is required" });
            }

            var participant = await _context.tbl_prgm_participants
                .FirstOrDefaultAsync(p => p.prgm_id == program_id
                                       && p.student_id == student_id
                                       && p.status != "deleted");

            if (participant == null)
            {
                return Ok(new
                {
                    status = false,
                    message = "Participant not found for the given program and student"
                });
            }

            if (participant.program_status != "Verification Completed")
            {
                return Ok(new
                {
                    status = false,
                    message = "Student not verified"
                });
            }

            if (!string.IsNullOrEmpty(participant.chess_no))
            {
                return Ok(new
                {
                    status = false,
                    message = "Chest number already assigned for this student in the selected program."
                });
            }

            int? acYearId = participant.ac_year_id;

            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == program_id);

            if (program == null)
            {
                return Ok(new
                {
                    status = false,
                    message = "Program not found"
                });
            }

            //int stageId = program.stage_id ?? 1;

            //// Stage 1 => 1001-1999
            //// Stage 2 => 2001-2999
            //// Stage 3 => 3001-3999
            //int rangeStart = (stageId * 1000) + 1;
            //int rangeEnd = (stageId * 1000) + 999;


            var stage = await _context.tbl_stage
    .FirstOrDefaultAsync(s => s.id == program.stage_id);

            if (stage == null)
            {
                return Ok(new
                {
                    status = false,
                    message = "Stage not found"
                });
            }

            int rangeStart = stage.chest_start;
            int rangeEnd = stage.chest_end;
            // Get all used chest numbers for same Academic Year + Stage
            string chest_no = "";

            lock (_chestLock)
            {

                var usedChestNos = (
                    from pp in _context.tbl_prgm_participants
                    join pr in _context.tbl_program
                        on pp.prgm_id equals pr.id
                    where pp.status != "deleted"
                       && pr.ac_year_id == acYearId
&& pr.stage_id == program.stage_id
&& !string.IsNullOrEmpty(pp.chess_no)
                    select pp.chess_no
                ).ToList();

                var usedNumbers = usedChestNos
                    .Where(x => int.TryParse(x, out _))
                    .Select(int.Parse)
                    .Where(x => x >= rangeStart && x <= rangeEnd)
                    .Distinct()
                    .ToHashSet();

                //int nextNumber = rangeStart;

                //while (usedNumbers.Contains(nextNumber))
                //{
                //    nextNumber++;
                //}

                int nextNumber = usedNumbers.Any()
    ? usedNumbers.Max() + 1
    : rangeStart;
                if (nextNumber > rangeEnd)
                {
                    throw new Exception($"Chest number limit reached for stage { stage.stage_name}");
                }

                chest_no = nextNumber.ToString();

                participant.chess_no = chest_no;
                participant.program_status = "Chest Number Generated";

                _context.SaveChanges();
            }


            var student = await _context.tbl_student
                .FirstOrDefaultAsync(s => s.id == participant.student_id);

            if (student == null)
            {
                return Ok(new
                {
                    status = false,
                    message = "Student not found"
                });
            }

            var instituteId = student.institute;
            var groupName = participant.group_name;

            var groupMembers = await _context.tbl_group_members
                .Where(g => g.group_name == groupName
                         && g.instit_id == instituteId
                         && g.prgm_id == program_id)
                .ToListAsync();

            foreach (var member in groupMembers)
            {
                member.chest_no = chest_no;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                chest_no = chest_no,
                message = "Chest number generated successfully"
            });
        }

        [HttpGet]
        [Route("get_programpoints")]

        public async Task<IActionResult> get_programpoints(int program_id)
        {
            // 1. Get program info
            var programInfo = await (from p in _context.tbl_program
                                     join st in _context.tbl_stage on p.stage_id equals st.id into stageJoin
                                     from stage in stageJoin.DefaultIfEmpty()
                                     where p.id == program_id
                                     select new
                                     {
                                         program_id = p.id,
                                         program_name = p.program_name,
                                         stage_name = stage != null ? stage.stage_name : "",
                                         p.participant_type,
                                         p.item_code,
                                         p.gender,
                                         p.program_type,
                                         p.ac_year_id
                                         //  total_points = p.total_points
                                     }).FirstOrDefaultAsync();

            if (programInfo == null)
            {
                return Ok(new { status = false, message = "Program not found" });
            }

            // 2. Get all judgment point entries
            var rawData = await (from p in _context.tbl_prgm_participants
                                 join pp in _context.tbl_prgm_judgement_criteria on p.prgm_id equals pp.prgm_id
                                 join s in _context.tbl_student on p.student_id equals s.id
                                 join jc in _context.tbl_judgement_criteria on pp.judgement_criteria equals jc.id
                                 where p.prgm_id == program_id && p.status != "deleted" && !string.IsNullOrEmpty(p.chess_no)

                                 select new
                                 {
                                     p.student_id,
                                     p.chess_no,
                                     student_name = s.name,
                                     criteria_name = jc.name,
                                     pp.point
                                 }).ToListAsync();

            // 3. Group by participant and build pivoted data
            var groupedData = rawData
                .GroupBy(x => new { x.student_id, x.chess_no, x.student_name })
                .Select(g => new
                {
                    chess_no = g.Key.chess_no,
                    student_name = g.Key.student_name,
                    // Dynamically pivot criteria
                    criteria = g
                        .GroupBy(c => c.criteria_name)
                        .ToDictionary(cg => cg.Key, cg => cg.Sum(x => x.point)),
                    points = g.Sum(x => x.point) // Total Points
                }).ToList();


            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            //eventdetails
            var eventdetails = await (from e in _context.tbl_event
                                      where e.status == "active" &&e.ac_year_id==programInfo.ac_year_id
                                      select new
                                      {
                                          e.image,
                                          imageurl = !string.IsNullOrEmpty(e.image) ? $"{baseUrl}uploads/event/{e.image}" : null,
                                          e.image1,
                                          imageurl1 = !string.IsNullOrEmpty(e.image1) ? $"{baseUrl}uploads/event/{e.image1}" : null,
                                      }).FirstOrDefaultAsync(); //  fetch single record


            // 4. Return formatted response
            return Ok(new
            {
                status = true,
                message = "Success",
                program_id = programInfo.program_id,
                program_name = programInfo.program_name,
                stage_name = programInfo.stage_name,
                participant_type = programInfo.participant_type,
                item_code = programInfo.item_code,
                gender = programInfo.gender,
                program_type = programInfo.program_type,
                image = eventdetails.image,
                imageurl = eventdetails.imageurl,
                image1 = eventdetails.image1,
                imageurl1 = eventdetails.imageurl1,
                //  total_points = programInfo.total_points,
                data = groupedData
            });
        }


        //tabulation team
        [HttpGet]
        [Route("get_prgm_student_details")]
        public async Task<ActionResult> get_prgm_student_details(int program_id)
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
               .Where(g => int.TryParse(g.Key.position, out int pos) && pos <= 3) // ✅ Exclude position > 3
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

                   string[] prizeTitles = { "First Prize", "Second Prize", "Third Prize" };
                   string prize = int.TryParse(g.Key.position, out int pos) && pos >= 1 && pos <= 3
                       ? prizeTitles[pos - 1]
                       : "";

                   //  Total judge points for a chest_no
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
                       position_point = g.Key.position_point,
                       total_marks = g.Key.total_point,
                       total_judge_point = totalJudgePoint
                   };
               })
               .OrderByDescending(x => x.average_marks)
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


        //announcement team

        [HttpGet]
        [Route("get_prgm_result")]

        public async Task<ActionResult> get_prgm_result(int? stage_id, int? program_id, string? status,int?ac_year_id)
        {
            if (program_id == null)
            {
                return Ok(new { status = false, message = "Program ID is required" });
            }
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            // Fetch Program and Stage Info
            var programDetails = await (from p in _context.tbl_program
                                        join st in _context.tbl_stage on p.stage_id equals st.id into stJoin
                                        from stage in stJoin.DefaultIfEmpty()
                                        where p.id == program_id && 
                                        (!stage_id.HasValue || p.stage_id == stage_id.Value) && 
                                        (string.IsNullOrEmpty(status) || p.status == status) &&p.ac_year_id==ac_year_id
                                        select new
                                        {
                                            program_name = p.program_name,
                                            status = p.status,
                                            program_id = p.id,
                                            p.program_type,
                                            p.participant_type,
                                            p.gender,
                                            p.stage_id,
                                            stage_name = stage != null ? stage.stage_name : "",
                                            p.item_code
                                        }).FirstOrDefaultAsync();

            if (programDetails == null)
            {
                return Ok(new { status = false, message = "Program not found" });
            }


            // Fetch participants and their marks
            var result = await (from p in _context.tbl_prgm_participants
                                join s in _context.tbl_student on p.student_id equals s.id
                                join i in _context.tbl_institute on s.institute equals i.id into ins
                                from inst in ins.DefaultIfEmpty()
                                where p.prgm_id == program_id 
                                && p.ac_year_id==ac_year_id
                                select new
                                {
                                    p.student_id,
                                    student_name = s.name,
                                    admsn_no = s.admsn_no,
                                    s.institute,
                                    institute_name = inst != null ? inst.name : "",
                                    chest_no = p.chess_no,
                                    grade_point = p.grade_point ?? 0,
                                    position_point = p.position_point ?? 0,
                                    total_point = p.total_point ?? 0,
                                    position = p.position,
                                    average_point = p.average_point,
                                    imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
                                }).ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "No data found" });
            }

           // return Ok(result);

            // Fetch grades to map
            var grades = await _context.tbl_grade.ToListAsync();

            // Assign grade and prize based on saved position
            //var finalResult = result.Select(r =>
            var finalResult = result
    .Where(r => int.TryParse(r.position, out int pos) && pos >= 1 && pos <= 3) //  Filter positions 1 to 3
    .Select(r =>
    {
        string[] prizeTitles = { "First Prize", "Second Prize", "Third Prize" };
        int pos = int.TryParse(r.position, out int parsedPos) ? parsedPos : 0;
        string prize = (pos > 0 && pos <= prizeTitles.Length) ? prizeTitles[pos - 1] : "";

        var grade = grades
            .Where(g => r.average_point >= g.from_point && r.average_point <= g.to_point)
            .Select(g => g.grade)
            .FirstOrDefault() ?? "N/A";

        return new
        {
            r.student_id,
            r.chest_no,
            r.student_name,
            r.admsn_no,
            r.institute,
            r.institute_name,
            average_marks = r.average_point,
            grade,
            position = r.position,
            prize,
            total_marks = r.total_point,
            r.imageurl
        };
    })
            .OrderBy(r => int.TryParse(r.position, out var pos) ? pos : int.MaxValue)
            .ToList();

            // Final JSON
            return Ok(new
            {
                status = true,
                message = "Success",
                program_id = programDetails.program_id,
                program_name = programDetails.program_name,
                stage_name = programDetails.stage_name,
                participant_type = programDetails.participant_type,
                program_type = programDetails.program_type,
                gender = programDetails.gender,
                item_code = programDetails.item_code,

                programstatus = programDetails.status,
                data = finalResult
            });
        }

        [HttpGet]
        [Route("announce_results")]

        public async Task<ActionResult> announce_results(int? stage_id, int? program_id, string? status)
        {
            if (program_id == null)
            {
                return Ok(new { status = false, message = "Program ID is required" });
            }

            // Fetch Program and Stage Info
            var programDetails = await (from p in _context.tbl_program
                                        join st in _context.tbl_stage on p.stage_id equals st.id into stJoin
                                        from stage in stJoin.DefaultIfEmpty()
                                        where p.id == program_id && (!stage_id.HasValue || p.stage_id == stage_id.Value) && (string.IsNullOrEmpty(status) || p.status == status)
                                        select new
                                        {
                                            program_name = p.program_name,
                                            status = p.status,
                                            program_id = p.id,
                                            stage_name = stage != null ? stage.stage_name : ""
                                        }).FirstOrDefaultAsync();

            if (programDetails == null)
            {
                return Ok(new { status = false, message = "Program not found" });
            }

            // Fetch participants and their marks
            var result = await (from p in _context.tbl_prgm_participants
                                join s in _context.tbl_student on p.student_id equals s.id
                                where p.prgm_id == program_id
                                select new
                                {
                                    p.student_id,
                                    student_name = s.name,
                                    admsn_no = s.admsn_no,
                                    chest_no = p.chess_no,
                                    grade_point = p.grade_point ?? 0,
                                    position_point = p.position_point ?? 0,
                                    total_point = p.total_point ?? 0,
                                    position = p.position,
                                    average_point = p.average_point
                                }).ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "No data found" });
            }

            // Fetch grades to map
            var grades = await _context.tbl_grade.ToListAsync();

            // Assign grade and prize based on saved position
            var prizeTitles = new[] { "First Prize", "Second Prize", "Third Prize" };

            //var finalResult = result.Select(r =>
            var finalResult = result
       .Where(r => int.TryParse(r.position, out int pos) && pos >= 1 && pos <= 3)
       .Select(r =>
       {
           int pos = int.TryParse(r.position, out int parsedPos) ? parsedPos : 0;
           string prize = (pos > 0 && pos <= prizeTitles.Length) ? prizeTitles[pos - 1] : "";

           var grade = grades
               .Where(g => r.average_point >= g.from_point && r.average_point <= g.to_point)
               .Select(g => g.grade)
               .FirstOrDefault() ?? "N/A";

           return new
           {
               program_id = programDetails.program_id,
               program_name = programDetails.program_name,
               stage_name = programDetails.stage_name,
               programstatus = programDetails.status,
               r.student_id,
               r.chest_no,
               r.student_name,
               r.admsn_no,
               average_marks = r.average_point,
               grade,
               position = r.position,
               prize,
               total_marks = r.total_point
           };
       })
            .OrderBy(r => int.TryParse(r.position, out var pos) ? pos : int.MaxValue)
            .ToList();

            return Ok(new
            {
                status = true,
                message = "Success",
                data = finalResult
            });
        }



        //update program status
        //[HttpPut]
        //[Route("update_program_status")]
        //public async Task<ActionResult> update_program_status([FromForm] int? program_id, [FromForm] string? status)
        //{
        //    if (program_id == null)
        //        return BadRequest(new { status = false, message = "program_id is required" });

        //    if (string.IsNullOrEmpty(status))
        //        return BadRequest(new { status = false, message = "Status is required" });


        //    // Get the program
        //    var program = await _context.tbl_program
        //        .FirstOrDefaultAsync(p => p.id == program_id && p.delete_status.ToLower() == "active");

        //    program.status = status;
        //    program.status_updated = DateTime.Now;
        //    await _context.SaveChangesAsync();


        //    if (status == "Tabulation Manager Approved")
        //    {
        //        // 🔹 Get Chief Program Manager users with FCM tokens
        //        var recipients = await (from u in _context.tbl_user
        //                                where u.type == "chief_program_manager" && !string.IsNullOrEmpty(u.fcm)
        //                                select u.fcm).ToListAsync();

        //        foreach (var fcmToken in recipients)
        //        {
        //            try
        //            {
        //                var message = new Message
        //                {
        //                    Notification = new Notification
        //                    {
        //                        Title = "Tabulation Manager Approved",
        //                        Body = $"The Tabulation Manager has approved the marks for program '{program.program_name}' (Item Code: {program.item_code}). The Chief Program Manager can now proceed."
        //                    },
        //                    Data = new Dictionary<string, string>
        //      {
        //          { "program_id", program_id.ToString() },
        //          { "status", "Tabulation Manager Approved" }
        //      },
        //                    Token = fcmToken
        //                };

        //                await FirebaseMessaging.DefaultInstance.SendAsync(message);
        //            }
        //            catch (Exception ex)
        //            {
        //               // return Ok(new { status = true, message = "notification failed", error = ex.Message });
        //            }
        //        }
        //    }
        //    return Ok(new
        //    {
        //        status = true,
        //        message = "Status updated successfully"
        //    });
        //}

[HttpPut]
[Route("update_program_status")]
public async Task<ActionResult> update_program_status(
    [FromForm] int? program_id,
    [FromForm] string? status)
        {
            if (program_id == null)
                return BadRequest(new
                {
                    status = false,
                    message = "program_id is required"
                });

            if (string.IsNullOrEmpty(status))
                return BadRequest(new
                {
                    status = false,
                    message = "Status is required"
                });

            // Get the program
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p =>
                    p.id == program_id &&
                    p.delete_status.ToLower() == "active");

            if (program == null)
                return NotFound(new
                {
                    status = false,
                    message = "Program not found or inactive."
                });

            // Update program status
            program.status = status;
            program.status_updated = DateTime.Now;

            // Save database changes first
            await _context.SaveChangesAsync();

            // ---------------------------------------------------------
            // Tabulation Manager Approved
            // ---------------------------------------------------------
            if (status == "Tabulation Manager Approved")
            {
                // Get Chief Program Manager users with FCM tokens
                var recipients = await (
                    from u in _context.tbl_user
                    where u.type == "chief_program_manager"
                          && !string.IsNullOrEmpty(u.fcm)
                    select u.fcm
                ).ToListAsync();

                // Background Firebase notification
                _ = Task.Run(async () =>
                {
                    foreach (var fcmToken in recipients)
                    {
                        try
                        {
                            var message = new Message
                            {
                                Notification = new Notification
                                {
                                    Title = "Tabulation Manager Approved",

                                    Body =
                                        $"The Tabulation Manager has approved the marks " +
                                        $"for program '{program.program_name}' " +
                                        $"(Item Code: {program.item_code}). " +
                                        $"The Chief Program Manager can now proceed."
                                },

                                Data = new Dictionary<string, string>
                        {
                            {
                                "program_id",
                                program_id.ToString()
                            },
                            {
                                "status",
                                "Tabulation Manager Approved"
                            }
                        },

                                Token = fcmToken
                            };

                            await FirebaseMessaging.DefaultInstance
                                .SendAsync(message);
                        }
                        catch (Exception ex)
                        {
                            // Log notification error
                        }
                    }
                });
            }

            // Return immediately.
            // API does NOT wait for Firebase.
            return Ok(new
            {
                status = true,
                message = "Status updated successfully"
            });
        }


        ///automated marks in tbl_prgm_participants
        [HttpPost]
        [Route("UpdateRanksAndPointsForProgram")]
        public async Task<ActionResult> UpdateRanksAndPointsForProgram(int prgmId)
        {
            // Step 1: Update program_status to 'judgement completed' for all participants of this program
            var participantsToUpdateStatus = await _context.tbl_prgm_participants
                .Where(p => p.prgm_id == prgmId)
                .ToListAsync();

            if (participantsToUpdateStatus == null || participantsToUpdateStatus.Count == 0)
            {
                return NotFound(new { status = false, message = "No participants found for the given program." });
            }

            foreach (var participant in participantsToUpdateStatus)
            {
                participant.program_status = "Judgement Completed";
                _context.Entry(participant).State = EntityState.Modified;
            }

            //status updation in tbl_program
            var programEntity = await _context.tbl_program.FirstOrDefaultAsync(p => p.id == prgmId);
            if (programEntity == null)
            {
                return NotFound(new { status = false, message = "Program not found." });
            }

            programEntity.status = "Completed";
            programEntity.status_updated = DateTime.Now;
            _context.Entry(programEntity).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            // Step 2: Get the program type
            //var program = await _context.tbl_program
            //    .Where(p => p.id == prgmId)
            //    .Select(p => new { p.program_type })
            //    .FirstOrDefaultAsync();

            //if (program == null)
            //{
            //    return NotFound(new { status = false, message = "Program not found." });
            //}

            string programType = programEntity.program_type?.ToLower().Trim();

            // Step 3: Get position points for this program type
            var positionPoints = await _context.tbl_position_point
                .Where(pp => pp.program_type.ToLower().Trim() == programType)
                .ToListAsync();

            // Step 4: Get participants for ranking (only those with average_point > 0)
            var participants = await _context.tbl_prgm_participants
                .Where(p => p.prgm_id == prgmId && p.program_status == "Judgement Completed" && p.average_point > 0)
                .OrderByDescending(p => p.average_point)
                .ToListAsync();

            if (participants.Count == 0)
            {
                return Ok(new { status = true, message = "Participants updated, but no eligible participants for ranking." });
            }


            // Step 5: Assign ranks and calculate points (dense ranking with ties)
            //int position = 1;
            //double? lastScore = null;
            //string lastPosition = null;

            //foreach (var participant in participants)
            //{
            //    if (lastScore != null && participant.average_point == lastScore)
            //    {
            //        // Same score → same position
            //        participant.position = lastPosition;
            //    }
            //    else
            //    {
            //        // New score → assign next position
            //        participant.position = position.ToString();
            //        lastPosition = participant.position;
            //    }

            //    // All tied participants get same position_point
            //    var posPoint = positionPoints.FirstOrDefault(pp => pp.position == participant.position);
            //    participant.position_point = posPoint?.point ?? 0;

            //    participant.total_point = (participant.grade_point ?? 0) + (participant.position_point ?? 0);

            //    _context.Entry(participant).State = EntityState.Modified;

            //    lastScore = participant.average_point;
            //    position++; // dense ranking → always increment by 1
            //}
            int position = 1;
            double? lastScore = null;
            string lastPosition = null;
            int? lastPositionPoint = null;

            foreach (var participant in participants.OrderByDescending(x => x.average_point))
            {
                if (lastScore != null && participant.average_point == lastScore)
                {
                    // Same score → same position and same position_point
                    participant.position = lastPosition;
                    participant.position_point = lastPositionPoint;
                }
                else
                {
                    // New score → assign next position and fetch corresponding points
                    participant.position = position.ToString();
                    var posPoint = positionPoints.FirstOrDefault(pp => pp.position == participant.position);
                    participant.position_point = posPoint?.point ?? 0;

                    // Store last values for next comparison
                    lastPosition = participant.position;
                    lastPositionPoint = participant.position_point.HasValue
                        ? (int?)Math.Round(participant.position_point.Value)
                        : null;


                    // Increment position only when score changes (dense ranking)
                    position++;
                }

                // Calculate total points
                participant.total_point = (participant.grade_point ?? 0) + (participant.position_point ?? 0);

                _context.Entry(participant).State = EntityState.Modified;

                lastScore = participant.average_point;
            }


            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Ranks and points updated successfully." });
        }

        //[HttpPost]
        //[Route("UpdateRanksAndPointsForProgram")]


        //public async Task<ActionResult> UpdateRanksAndPointsForProgram(int prgmId)
        //{
        //    // Step 1: Update program_status to 'judgement completed' for all participants of this program
        //    var participantsToUpdateStatus = await _context.tbl_prgm_participants
        //        .Where(p => p.prgm_id == prgmId)
        //        .ToListAsync();

        //    if (participantsToUpdateStatus == null || participantsToUpdateStatus.Count == 0)
        //    {
        //        return NotFound(new { status = false, message = "No participants found for the given program." });
        //    }

        //    foreach (var participant in participantsToUpdateStatus)
        //    {
        //        participant.program_status = "Judgement Completed";
        //        _context.Entry(participant).State = EntityState.Modified;
        //    }

        //    //status updation in tbl_program
        //    var programEntity = await _context.tbl_program.FirstOrDefaultAsync(p => p.id == prgmId);
        //    if (programEntity == null)
        //    {
        //        return NotFound(new { status = false, message = "Program not found." });
        //    }

        //    programEntity.status = "Completed";
        //    programEntity.status_updated = DateTime.Now;
        //    _context.Entry(programEntity).State = EntityState.Modified;

        //    await _context.SaveChangesAsync();

        //    // Step 2: Get the program type
        //    //var program = await _context.tbl_program
        //    //    .Where(p => p.id == prgmId)
        //    //    .Select(p => new { p.program_type })
        //    //    .FirstOrDefaultAsync();

        //    //if (program == null)
        //    //{
        //    //    return NotFound(new { status = false, message = "Program not found." });
        //    //}

        //    string programType = programEntity.program_type?.ToLower().Trim();

        //    // Step 3: Get position points for this program type
        //    var positionPoints = await _context.tbl_position_point
        //        .Where(pp => pp.program_type.ToLower().Trim() == programType)
        //        .ToListAsync();

        //    // Step 4: Get participants for ranking (only those with average_point > 0)
        //    var participants = await _context.tbl_prgm_participants
        //        .Where(p => p.prgm_id == prgmId && p.program_status == "Judgement Completed" && p.average_point > 0)
        //        .OrderByDescending(p => p.average_point)
        //        .ToListAsync();

        //    if (participants.Count == 0)
        //    {
        //        return Ok(new { status = true, message = "Participants updated, but no eligible participants for ranking." });
        //    }

        //    // Step 5: Assign ranks and calculate points
        //    int position = 1;
        //    foreach (var participant in participants)
        //    {
        //        participant.position = position.ToString();

        //        var posPoint = positionPoints.FirstOrDefault(pp => pp.position == position.ToString());
        //        participant.position_point = posPoint?.point ?? 0;

        //        participant.total_point = (participant.grade_point ?? 0) + (participant.position_point ?? 0);

        //        _context.Entry(participant).State = EntityState.Modified;
        //        position++;
        //    }

        //    await _context.SaveChangesAsync();

        //    return Ok(new { status = true, message = "Ranks and points updated successfully." });
        //}



        //backstage prepare judgement sheet
        [HttpPost]
        [Route("update_student_program_status")]
        public async Task<ActionResult> update_student_program_status([FromForm] int? program_id, [FromForm] string? status)
        {
            if (program_id == null)
                return BadRequest(new { status = false, message = "program_id is required" });

            if (string.IsNullOrEmpty(status))
                return BadRequest(new { status = false, message = "Status is required" });

            // Get the programF
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == program_id && p.delete_status.ToLower() == "active");

            if (program == null)
                return NotFound(new { status = false, message = "Program not found or inactive." });

           

            // When status = "Judgement Sheet Prepared"
            if (status == "Judgement Sheet Prepared")
            {
                //judges with FCM tokens
                var judges = await (from jp in _context.tbl_judge_prgm
                                   // join j in _context.tbl_judge on jp.judge_id equals j.id
                                    join u in _context.tbl_user on jp.judge_id equals u.reference_id
                                    where jp.program_id == program_id && !string.IsNullOrEmpty(u.fcm)
                                    select new { u.fcm, jp.judge_id, jp.program_id })
                                   .Distinct()
                                   .ToListAsync();

                var participants = await _context.tbl_prgm_participants
                    .Where(p => p.prgm_id == program_id
                                && p.status.ToLower() != "deleted"
                                && p.program_status.ToLower() == "transmitted to frontstage")
                    .ToListAsync();

                foreach (var participant in participants)
                {
                    participant.program_status = status;
                    participant.status_updated = DateTime.Now;
                }

                _ = Task.Run(async () =>
                {
                    foreach (var judge in judges)
                    {
                        try
                        {
                            var message = new Message
                            {
                                Notification = new Notification
                                {
                                    Title = "Judgement Sheet Prepared",
                                    Body = $"The judgement sheet for program '{program.program_name}' with item code '{program.item_code}' has been successfully prepared."
                                },
                                Data = new Dictionary<string, string>
                {
                    { "program_id", program_id.ToString() },
                    { "status", "Judgement Sheet Prepared" }
                },
                                Token = judge.fcm
                            };

                            await FirebaseMessaging.DefaultInstance.SendAsync(message);
                        }
                        catch (Exception ex)
                        {
                            // Log notification error
                        }
                    }
                });

            }

            // When status = "Tabulation Team Approved"
            else if (status == "Tabulation Team Approved")
            {
                //judges with FCM tokens
                var judges = await (from jp in _context.tbl_judge_prgm
                                  //  join j in _context.tbl_judge on jp.judge_id equals j.id
                                    join u in _context.tbl_user on jp.judge_id equals u.reference_id
                                    where jp.program_id == program_id && !string.IsNullOrEmpty(u.fcm)
                                    select new { u.fcm, jp.judge_id, jp.program_id })
                                   .Distinct()
                                   .ToListAsync();
                // foreach (var judge in judges)
                // {
                //     try
                //     {
                //         var message = new Message
                //         {
                //             Notification = new Notification
                //             {
                //                 Title = "Tabulation Team Approved",
                //                 Body = $"The tabulation team has approved the results for program '{program.program_name}' with item code '{program.item_code}'."
                //             },
                //             Data = new Dictionary<string, string>
                //{
                //    { "program_id", program_id.ToString() },
                //    { "status", "Tabulation Team Approved" }
                //},
                //             Token = judge.fcm
                //         };

                //         await FirebaseMessaging.DefaultInstance.SendAsync(message);
                //     }
                //     catch (Exception ex)
                //     {
                //         // return Ok(new { status = true, message = "notification failed", error = ex.Message });
                //     }
                // }
                _ = Task.Run(async () =>
                {
                    foreach (var judge in judges)
                    {
                        try
                        {
                            var message = new Message
                            {
                                Notification = new Notification
                                {
                                    Title = "Judgement Sheet Prepared",
                                    Body = $"The judgement sheet for program '{program.program_name}' with item code '{program.item_code}' has been successfully prepared."
                                },
                                Data = new Dictionary<string, string>
                {
                    { "program_id", program_id.ToString() },
                    { "status", "Judgement Sheet Prepared" }
                },
                                Token = judge.fcm
                            };

                            await FirebaseMessaging.DefaultInstance.SendAsync(message);
                        }
                        catch (Exception ex)
                        {
                            // Log notification error
                        }
                    }
                });


            }

            // Update main program status
            program.status = status;
            program.status_updated = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Status updated successfully"
            });
        }
        ///announcement
        [HttpGet]
        [Route("GetResultSummary")]
        public async Task<ActionResult> GetResultSummary(int? stage_id, int? program_id, DateTime? date, string? status,int?ac_year_id)
        {
            var programs_count = await _context.tbl_program
                .Where(tbl => tbl.delete_status == "active" &&tbl.ac_year_id==ac_year_id)
                .CountAsync();

            var count = await _context.tbl_program
               .Where(tbl => tbl.delete_status == "active" && tbl.stage_id == stage_id && tbl.date == date&& tbl.ac_year_id==ac_year_id)
               .CountAsync();

            var query = from p in _context.tbl_program
                        join s in _context.tbl_stage on p.stage_id equals s.id
                        //where p.status.ToLower()== "Transmitted to Announcement Team" && (!stage_id.HasValue || p.stage_id == stage_id)
                        where (p.status.ToLower() == "transmitted to announcement team"
       || p.status.ToLower() == "result published" || p.status == "Transferred to Media")
      && (!stage_id.HasValue || p.stage_id == stage_id)

                           && (!program_id.HasValue || p.id == program_id)
                           && (!date.HasValue || p.date == date) 
                           &&p.ac_year_id==ac_year_id
                        select new
                        {
                            p.id,
                            p.program_name,
                            s.stage_name,
                            p.item_code,
                            p.status
                        };


            var programs = await query.ToListAsync();
            //status filtration

            if (!string.IsNullOrEmpty(status))
            {
                var statusLower = status.ToLower();

                if (statusLower == "pending")
                {
                    // Exclude "Result Published" and "Transferred to Media"
                    programs = programs
                        .Where(p => p.status.ToLower() != "result published"
                                 && p.status.ToLower() != "transferred to media")
                        .ToList();
                }
                else if (statusLower == "result published")
                {
                    // Include only "Result Published" and "Transferred to Media"
                    programs = programs
                        .Where(p => p.status.ToLower() == "result published" || p.status.ToLower()== "transferred to media")
                        .ToList();
                }
                // else, leave all programs if status is something else
            }
            //

            var result = new List<object>();

            foreach (var prog in programs)
            {
                var participants = await (from pp in _context.tbl_prgm_participants
                                          join st in _context.tbl_student on pp.student_id equals st.id
                                          where pp.prgm_id == prog.id
                                          select new
                                          {
                                              st.name,
                                              pp.position
                                          }).ToListAsync();

                //string GetNameByPosition(int pos) =>
                //    participants.FirstOrDefault(x => int.TryParse(x.position, out int p) && p == pos)?.name ?? "";
                string GetNameByPosition(int pos) =>
    string.Join(", ", participants
        .Where(x => int.TryParse(x.position, out int p) && p == pos)
        .Select(x => x.name));


                result.Add(new
                {
                    program_id = prog.id,
                    item_code = prog.item_code,
                    program_name = prog.program_name,
                    stage_name = prog.stage_name,
                    first_prize = GetNameByPosition(1),
                    second_prize = GetNameByPosition(2),
                    third_prize = GetNameByPosition(3),
                    status = prog.status
                });
            }

            return Ok(new
            {
                status = true,
                message = "Success",
                programs_count = programs_count,
                count = count,
                data = result
            });
        }


        [HttpPut]
        [Route("mark_absent")]

        public async Task<ActionResult> mark_absent(
    [FromForm] int? program_id,
    [FromForm] string? chest_number,
    [FromForm] string? present,
    [FromForm] string? remark)
        {
            if (program_id == null)
                return BadRequest(new { status = false, message = "program_id is required" });

            if (string.IsNullOrEmpty(chest_number))
                return BadRequest(new { status = false, message = "chest_number is required" });

            if (string.IsNullOrEmpty(present))
                return BadRequest(new { status = false, message = "presence status is required" });

            // Step 1: Find participant from tbl_prgm_participants
            var participant = await _context.tbl_prgm_participants
                .FirstOrDefaultAsync(p => p.prgm_id == program_id && p.chess_no.ToLower() == chest_number.ToLower());



            // Step 3: Update tbl_prgm_point
            participant.ispresent = present;
            participant.remark = remark;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "data updated successfully"
            });
        }



        //

        //frontstage manager

        [HttpGet]
        [Route("get_prgm_students")]
        public async Task<ActionResult> get_prgm_students(int? program_id, DateTime? date)
        {
            if (_context.tbl_program == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var query = from pp in _context.tbl_prgm_participants
                        join p in _context.tbl_program
                            on pp.prgm_id equals p.id into prg
                        from pr in prg.DefaultIfEmpty()

                        join s in _context.tbl_student
                          on pp.student_id equals s.id into st
                        from stu in st.DefaultIfEmpty()
                        join c in _context.tbl_class
                         on stu.class_id equals c.id into cl
                        from cll in cl.DefaultIfEmpty()
                        join d in _context.tbl_division
                         on stu.division_id equals d.id into div
                        from di in div.DefaultIfEmpty()
                        join i in _context.tbl_institute
                        on stu.institute equals i.id into ins
                        from inst in ins.DefaultIfEmpty()
                        select new
                        {
                            program_id = pr.id,
                            program = pr != null ? pr.program_name : null,
                            date = pr != null ? pr.date : null,
                            programstatus = pr.status,
                            chess_no = pp.chess_no,
                            name = stu.name,
                            admsn_no = stu.admsn_no,
                            phone_no = stu.phone_no,
                            class_id = stu.class_id,
                            division_id = stu.division_id,
                            class_name = cll != null ? cll.@class : null,
                            division_name = di.division,
                            institute_id = stu.institute,
                            institute_name = inst.name,
                            token_no = pp.token_no,
                            total_points = pp != null ? pp.average_point ?? 0 : 0,
                            id = stu.id,
                            status = pp.program_status,
                        };

            if (program_id.HasValue)
            {
                query = query.Where(g => g.program_id == program_id.Value);
            }

            if (date.HasValue)
                query = query.Where(g => g.date == date);

            var result = await query.ToListAsync();

            if (result == null || result.Count == 0)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }

        //

        //get registered students count
        [HttpGet]
        [Route("registered_students_count")]
        public async Task<ActionResult> registered_students_count(int? institute_id, int? ac_year_id)
        {
            if (!institute_id.HasValue)
            {
                return Ok(new { status = false, message = "Please provide institute_id" });
            }

            // SOLO programs
            var soloQuery = await (
               from pp in _context.tbl_prgm_participants
               join p in _context.tbl_program on pp.prgm_id equals p.id
               join s in _context.tbl_student on pp.student_id equals s.id
               where p.program_type == "solo"
                     && pp.status != "deleted" && s.status!= "deleted"
                     && s.institute == institute_id.Value
               select new
               {
                   StudentId = s.id,
                   AcYearId = s.ac_year_id
               }
            ).Distinct().ToListAsync();

            if (ac_year_id.HasValue)
            {
                soloQuery = soloQuery
                    .Where(x => x.AcYearId == ac_year_id.Value)
                    .ToList();
            }

            var soloStudents = soloQuery
                .Select(x => x.StudentId)
                .Distinct()
                .ToList();


            // GROUP programs
            var groupQuery = await (
                from gm in _context.tbl_group_members
                join p in _context.tbl_program on gm.prgm_id equals p.id
                join s in _context.tbl_student on gm.student_id equals s.id
                where p.program_type == "group" 
                      && s.institute == institute_id.Value && s.status != "deleted"
                select new
                {
                    StudentId = s.id,
                    AcYearId = s.ac_year_id
                }
            ).Distinct().ToListAsync();

            if (ac_year_id.HasValue)
            {
                groupQuery = groupQuery
                    .Where(x => x.AcYearId == ac_year_id.Value)
                    .ToList();
            }

            var groupStudents = groupQuery
                .Select(x => x.StudentId)
                .Distinct()
                .ToList();


            // Merge both lists and get distinct count
            var totalStudents = soloStudents
                .Concat(groupStudents)
                .Distinct()
                .Count();
            return Ok(new
            {
                status = true,
                message = "Success",
                data = new { total_students = totalStudents }
            });
        }
        //


        //recently completed judgement list
        [HttpGet]
        [Route("get_recently_completed_judgements")]
        public async Task<ActionResult> get_recently_completed_judgements(int?ac_year_id)
        {
            var result = await (from p in _context.tbl_program
                                join s in _context.tbl_stage on p.stage_id equals s.id into stageJoin
                                from s in stageJoin.DefaultIfEmpty()
                                where p.status.ToLower() == "completed"
                                orderby p.status_updated descending
                                select new
                                {
                                    p.stage_id,
                                    stage_name = s.stage_name,
                                    p.participant_type,
                                    p.program_type,
                                    p.gender,
                                    p.program_name,
                                    p.status,
                                    p.item_code,
                                    p.ac_year_id
                                })
                                .ToListAsync();


            if(ac_year_id.HasValue)
            {
                result = result
                    .Where(r => r.ac_year_id == ac_year_id.Value)
                    .ToList();
            }
            if (result == null)
            {
                return Ok(new { status = false, message = "No recently completed judgements found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }


        //


        //update overall status to transfer to media 
        [HttpPut]
        [Route("transfer_to_media")]

        public async Task<ActionResult> transfer_to_media([FromForm] string? status)
        {
            if (string.IsNullOrEmpty(status))
                return BadRequest(new { status = false, message = "Status is required" });

            // Check if all programs have status = "result published"
            bool allPublished = await _context.tbl_program
                .AllAsync(p => p.status.ToLower() == "result published");

            if (!allPublished)
            {
                return Ok(new
                {
                    status = false,
                    message = "All programs must have the status 'result published' before updating."
                });
            }

            // Get all events
            var events = await _context.tbl_event.ToListAsync();

            // Update each event
            foreach (var ev in events)
            {
                ev.program_status = status;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Program status updated successfully in events."
            });
        }


        //

        //program excel
        [HttpGet]
        [Route("programs_excel")]
        public async Task<IActionResult> programs_excel(DateTime? from_date, DateTime? to_date,int?ac_year_id)
        {
            // Base query - returns anonymous type
            var query = from p in _context.tbl_program.AsNoTracking()
                        join i in _context.tbl_stage on p.stage_id equals i.id into ins
                        from it in ins.DefaultIfEmpty()
                        join ac in _context.tbl_academic_year on p.ac_year_id equals ac.id
                        where p.delete_status == "active"
                        select new
                        {
                            id = p.id,
                            program_name = p.program_name,
                            stage = it.stage_name,
                            date = p.date,
                            time = p.time,
                            color_code = p.color_code,
                            program_type = p.program_type,
                            gender = p.gender,
                            participant_type = p.participant_type,
                            item_code = p.item_code,
                            offstage_onstage = p.offstage_onstage,
                            no_of_group = p.no_of_group,
                            p.ac_year_id,
                            ac.year
                        };

            if (from_date.HasValue)
            {
                query = query.Where(r => r.date >= from_date.Value);
            }

            if (to_date.HasValue)
            {
                var toDateInclusive = to_date.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(r => r.date <= to_date.Value);
            }

            if (ac_year_id.HasValue)
            {
                query = query.Where(r => r.ac_year_id == ac_year_id);
            }
            var regList = await query.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Programs");

                // Headers (match program fields)
                var headers = new[]
                {
            "Program ID", "Program Name", "Stage", "Date", "Time",
            "Color Code", "Program Type", "Gender", "Participant Type",
            "Item Code", "Offstage/Onstage","Academic Year"
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
                    var program = regList[rowIndex];
                    worksheet.Cell(rowIndex + 2, 1).Value = program.id;
                    worksheet.Cell(rowIndex + 2, 2).Value = program.program_name;
                    worksheet.Cell(rowIndex + 2, 3).Value = program.stage;
                    worksheet.Cell(rowIndex + 2, 4).Value = program.date?.ToString("yyyy-MM-dd");
                    worksheet.Cell(rowIndex + 2, 5).Value = program.time;
                    worksheet.Cell(rowIndex + 2, 6).Value = program.color_code;
                    worksheet.Cell(rowIndex + 2, 7).Value = program.program_type;
                    worksheet.Cell(rowIndex + 2, 8).Value = program.gender;
                    worksheet.Cell(rowIndex + 2, 9).Value = program.participant_type;
                    worksheet.Cell(rowIndex + 2, 10).Value = program.item_code;
                    worksheet.Cell(rowIndex + 2, 11).Value = program.offstage_onstage;
                    worksheet.Cell(rowIndex + 2, 12).Value = program.year;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"programs.xlsx");
                }
            }
        }

        //


        //backstage and frontstage

        [HttpGet]
        [Route("backstage_programs_details")]
        public async Task<ActionResult> backstage_programs_details(int? program_id, int? institute_id = null, int? stage_id = null, DateTime? date = null, string? status = null, string? keyword = null, string? item_code = null, string? admsn_no = null,int?ac_year_id=null)
        {
            var eventData = await _context.tbl_event.Where(x => x.ac_year_id == ac_year_id)
               .FirstOrDefaultAsync(e => e.status == "active");
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";



            if ((program_id == null || program_id <= 0) && string.IsNullOrEmpty(item_code))
            {
                return Ok(new { status = false, message = "Program ID or Item Code is required" });
            }

            List<object> queryResult;

            var programType = await _context.tbl_program
     .Where(p => p.id == program_id)
     .Select(p => p.program_type)
     .FirstOrDefaultAsync();

            if (programType?.ToLower() == "group")
            {
                queryResult = await (
                    from pp in _context.tbl_prgm_participants

                    join prog in _context.tbl_program on pp.prgm_id equals prog.id

                    join s in _context.tbl_stage on prog.stage_id equals s.id into sta
                    from stg in sta.DefaultIfEmpty()

                    join stud in _context.tbl_student on pp.student_id equals stud.id
                    join institute in _context.tbl_institute on stud.institute equals institute.id into inst
                    from institute in inst.DefaultIfEmpty()
                    join cl in _context.tbl_class on stud.class_id equals cl.id into classJoin
                    from cl in classJoin.DefaultIfEmpty()
                    join di in _context.tbl_division on stud.division_id equals di.id into divisionJoin
                    from di in divisionJoin.DefaultIfEmpty()

                    where
                           (institute_id == null || stud.institute == institute_id) && pp.status != "deleted"
                          && (stage_id == null || prog.stage_id == stage_id)
&& (
    date == null ||
    (prog.date >= date.Value.Date &&
     prog.date < date.Value.Date.AddDays(1))
   ) && (status == null || prog.status == status)
                          && (admsn_no == null || stud.admsn_no == admsn_no)
                          && (ac_year_id == null || stud.ac_year_id == ac_year_id)

                      && ((program_id.HasValue && pp.prgm_id == program_id.Value) || !program_id.HasValue)
&& ((item_code != null && prog.item_code == item_code) || string.IsNullOrEmpty(item_code))

                    select new
                    {
                        program = prog.program_name,
                        programstatus = prog.status,
                        stage_id = prog.stage_id,
                        stage = stg.stage_name,
                        participant_type = prog.participant_type,
                        gender = prog.gender,
                        program_type = prog.program_type,
                        date = prog.date,
                        time = prog.time,
                        no_of_group = prog.no_of_group,
                        student_id = pp.student_id,
                        chess_no = pp != null ? pp.chess_no : null,
                        name = stud.name,
                        institute_id = stud.institute,
                        institute_name = institute != null ? institute.name : null,
                        admsn_no = stud.admsn_no,
                        phone_no = stud.phone_no,
                        class_id = stud.class_id,

                        division_id = stud.division_id,
                        image = stud.image,
                        imageurl = !string.IsNullOrEmpty(stud.image) ? $"{baseUrl}uploads/student/{stud.image}" : null,
                        class_name = cl != null ? cl.@class : null,
                        division_name = di != null ? di.division : null,
                        id = pp.prgm_id,
                        status = pp != null ? pp.program_status : "pending", // fallback default
                        token_no = pp != null ? pp.token_no : null,
                        item_code = prog.item_code,
                        group_name = pp.group_name,
                        total_points = pp != null ? pp.average_point ?? 0 : 0,
                        ac_year_id=prog.ac_year_id
                    }
                ).Cast<object>().ToListAsync();
            }

            else
            {
                queryResult = await (
                    from prp in _context.tbl_prgm_participants
                    join prog in _context.tbl_program on prp.prgm_id equals prog.id

                    join s in _context.tbl_stage on prog.stage_id equals s.id into sta
                    from stg in sta.DefaultIfEmpty()

                    join stud in _context.tbl_student on prp.student_id equals stud.id
                    join institute in _context.tbl_institute on stud.institute equals institute.id into inst
                    from institute in inst.DefaultIfEmpty()
                    join cl in _context.tbl_class on stud.class_id equals cl.id into classJoin
                    from cl in classJoin.DefaultIfEmpty()
                    join di in _context.tbl_division on stud.division_id equals di.id into divisionJoin
                    from di in divisionJoin.DefaultIfEmpty()
                    where ((program_id.HasValue && prp.prgm_id == program_id.Value) || !program_id.HasValue)
                          && prp.status != "deleted"
                          && (institute_id == null || stud.institute == institute_id)
                        && (stage_id == null || prog.stage_id == stage_id)
                        && (date == null || prog.date == date)
                        && (admsn_no == null || stud.admsn_no == admsn_no)
                        && (item_code == null || prog.item_code == item_code)
                        && (ac_year_id == null || stud.ac_year_id == ac_year_id)

                        && (status == null || prog.status == status)
                    select (object)new
                    {
                        programstatus = prog.status,

                        program = prog.program_name,
                        stage_id = prog.stage_id,
                        stage = stg.stage_name,
                        participant_type = prog.participant_type,
                        gender = prog.gender,
                        program_type = prog.program_type,
                        date = prog.date,

                        time = prog.time,
                        no_of_group = prog.no_of_group,

                        student_id = prp.student_id,
                        chess_no = prp.chess_no,
                        name = stud.name,
                        institute_id = stud.institute,
                        institute_name = institute != null ? institute.name : null,
                        admsn_no = stud.admsn_no,
                        phone_no = stud.phone_no,
                        class_id = stud.class_id,
                        division_id = stud.division_id,
                        image = stud.image,
                        imageurl = !string.IsNullOrEmpty(stud.image) ? $"{baseUrl}uploads/student/{stud.image}" : null,
                        class_name = cl != null ? cl.@class : null,
                        division_name = di != null ? di.division : null,
                        id = prp.prgm_id,
                        status = prp.program_status,
                        token_no = prp.token_no,
                        item_code = prog.item_code,
                        group_name = prp != null ? prp.group_name : null,

                        total_points = prp.average_point ?? 0,
                        ac_year_id = prog.ac_year_id

                    }
                ).ToListAsync();
            }
            //keyword
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.ToLower();
                queryResult = queryResult
                    .Select(p => (dynamic)p)
                    .Where(x =>
                        (!string.IsNullOrEmpty(x.name) && x.name.ToLower().Contains(keyword)) ||
                        (!string.IsNullOrEmpty(x.admsn_no) && x.admsn_no.ToLower().Contains(keyword))
                    //(!string.IsNullOrEmpty(x.phone_no) && x.phone_no.ToLower().Contains(keyword)) ||
                    //(!string.IsNullOrEmpty(x.institute_name) && x.institute_name.ToLower().Contains(keyword))
                    )
                    .Cast<object>()
                    .ToList();
            }
            //

            var result = queryResult
                .Select(p => (dynamic)p)
               .GroupBy(x => new { x.id, x.student_id })
.Select(g => g.First())

                .Select(x => new
                {
                    event_name = eventData.event_name,
                    program = x.program,
                    programstatus = x.programstatus,
                    stage_id = x.stage_id,
                    stage = x.stage,
                    no_of_group = x.no_of_group,
                    participant_type = x.participant_type,
                    gender = x.gender,
                    program_type = x.program_type,
                    date = x.date,
                    time = x.time,
                    chess_no = x.chess_no,
                    name = x.name,
                    admsn_no = x.admsn_no,
                    phone_no = x.phone_no,
                    class_id = x.class_id,
                    division_id = x.division_id,
                    class_name = x.class_name,
                    division_name = x.division_name,
                    institute_id = x.institute_id,
                    institute_name = x.institute_name,
                    token_no = x.token_no,
                    item_code = x.item_code,
                    group_name = x.group_name,

                    image = x.image,
                    imageurl = !string.IsNullOrEmpty(x.image) ? $"{baseUrl}uploads/student/{x.image}" : null,
                    total_points = x.total_points,
                    id = x.id,
                    student_id = x.student_id,
                    status = x.status,
                    ac_year_id = x.ac_year_id

                })
                .ToList();

            var chestnoPendingCount = result.Count(x =>
         string.IsNullOrEmpty(x.status?.ToString()) ||
         x.status.ToString().ToLower() == "verification completed");

            var allocationPendingCount = result.Count(x =>
                 string.IsNullOrEmpty(x.status?.ToString()) ||
                 x.status.ToString().ToLower() == "chest number generated");

            var programPendingCount = result.Count(x =>
                string.IsNullOrEmpty(x.status?.ToString()) ||
                x.status.ToString().ToLower() == "pending");
            var prepjudgmentsheetCount = result.Count(x =>
               string.IsNullOrEmpty(x.status?.ToString()) ||
               x.status.ToString().ToLower() == "transmitted to frontstage");
            if (!result.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success",
                allocationPendingCount= allocationPendingCount,chestnoPendingCount=chestnoPendingCount,
                programPendingCount= programPendingCount,
                prepjudgmentsheetCount= prepjudgmentsheetCount, data = result });
        }



        //

        //allocation sheet
        [HttpGet]
        [Route("allocation_sheet")]
        public async Task<ActionResult> allocation_sheet(int? program_id)
        {
            // Get participants first
            var chestNumbers = await (from pp in _context.tbl_prgm_participants
                                      where pp.prgm_id == program_id && pp.chess_no != null
                          orderby Convert.ToInt32(pp.chess_no) // numeric ordering

                                      select new
                                      {
                                          chest_no = pp.chess_no
                                      }).ToListAsync();

            // If no participants, skip program
            if (chestNumbers == null || chestNumbers.Count == 0)
            {
                return Ok(new { status = false, message = "Program not found" });
            }

            // Get program details
            var programDetails = await (from prgm in _context.tbl_program
                                        where prgm.id == program_id
                                        select new
                                        {
                                            prgm.item_code,
                                            prgm.program_name,
                                            prgm.program_type,
                                            prgm.participant_type,
                                            prgm.gender
                                        }).FirstOrDefaultAsync();

            if (programDetails == null)
            {
                return Ok(new { status = false, message = "Program not found" });
            }

            var result = new
            {
                programDetails.item_code,
                programDetails.program_name,
                programDetails.program_type,
                programDetails.gender,
                programDetails.participant_type,
                chest_numbers = chestNumbers
            };

            return Ok(new { status = true, message = "Success", data = result });
        }


        //



        //transmit to frontstage
        [HttpPost]
        [Route("transmit_to_frontstage_status")]
        public async Task<ActionResult> transmit_to_frontstage_status([FromForm] int? program_id, [FromForm] string? status)
        {
            if (program_id == null)
                return Ok(new { status = false, message = "Program ID is required." });

            if (string.IsNullOrEmpty(status))
                return Ok(new { status = false, message = "Status is required." });

            // Fetch all active participants for this program
            var participants = await _context.tbl_prgm_participants
                .Where(p => p.prgm_id == program_id && p.status.ToLower() == "active")
                .ToListAsync();

            if (participants.Count == 0)
                return Ok(new { status = false, message = "No active participants found for this program." });

            // Fetch the program and check its current status
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == program_id && p.delete_status.ToLower() == "active");

            if (program == null)
                return Ok(new { status = false, message = "Program not found." });

            // Only proceed if program status is "pending" or "Transmitted to frontstage"
            if (program.status != null &&
                (program.status.ToLower() == "pending" || program.status == "Transmitted to frontstage"))
            {
                // Update student status for all participants with non-empty chest_no
                foreach (var participant in participants.Where(p => !string.IsNullOrEmpty(p.chess_no)))
                {
                    participant.program_status = status;
                    participant.status_updated = DateTime.Now;
                }

                // Update program status
                program.status = status;
                program.status_updated = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = true,
                    message = "Status updated successfully"
                });
            }
            else
            {
                return Ok(new
                {
                    status = false,
                    message = $"Program status is '{program.status}', cannot update participants or program status."
                });
            }
        }


        //


        [HttpPost]
        [Route("update_group_prgrm_names")]
        public async Task<ActionResult> update_group_prgrm_names([FromForm] int? program_id, [FromForm] int? student_id, [FromForm] string name)
        {
            if (program_id == null)
                return BadRequest(new { status = false, message = "program_id is required" });

            if (student_id == null)
                return BadRequest(new { status = false, message = "student_id is required" });

            if (string.IsNullOrEmpty(name))
                return BadRequest(new { status = false, message = "name is required" });

            var program = _context.tbl_program
                .FirstOrDefault(p => p.id == program_id && p.delete_status.ToLower() == "active");

            if (program == null)
                return NotFound(new { status = false, message = "Program not found or inactive" });

            //check program participants table 
            var existingParticipant = _context.tbl_prgm_participants
                .FirstOrDefault(p => p.prgm_id == program_id && p.student_id == student_id);

            if (existingParticipant != null)
            {
                return Ok(new
                {
                    status = false,
                    message = "Cannot change group name since student is a main participant."
                });
            }

            var groupMember = _context.tbl_group_members
                .FirstOrDefault(g => g.prgm_id == program_id && g.student_id == student_id);

            if (groupMember == null)
                return NotFound(new { status = false, message = "Group member not found" });

            // Update the name
            groupMember.group_name = name;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Group name updated successfully"
            });
        }


        //view program
        [HttpGet]
        [Route("program_view")]
        public async Task<ActionResult> program_view(int? id, DateTime? date, int? stage_id, string? status, int? institute_id, string? participant_type, string? program_type, string? gender, string? keyword, string? item_code,int?ac_year_id)
        {
            if (_context.tbl_program == null)
                return NotFound(new { status = false, message = "Data not found" });

            var query = from p in _context.tbl_program
                        join s in _context.tbl_stage on p.stage_id equals s.id into st
                        from stg in st.DefaultIfEmpty()  // left join
                        where p.delete_status == "active"
                        select new
                        {
                            p.id,
                            p.program_type,
                            p.participant_type,
                            p.gender,
                            p.stage_id,
                            stg.stage_name,           
                            p.program_name,
                            p.color_code,
                            p.date,
                            p.time,
                            p.added_by,
                            p.addedtype,
                            p.addedon,
                            p.status,
                            p.item_code,
                            p.offstage_onstage,
                            p.group_min_participants,
                            p.group_max_participants,
                            p.no_of_group,
                            p.ac_year_id


                        };

            if (id.HasValue)
                query = query.Where(g => g.id == id.Value);


            if (date.HasValue)
                query = query.Where(g => g.date == date);

            if (stage_id.HasValue)
                query = query.Where(g => g.stage_id == stage_id.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(e => e.status == status);

            if (!string.IsNullOrWhiteSpace(participant_type))
                query = query.Where(e => e.participant_type == participant_type);

            if (!string.IsNullOrWhiteSpace(program_type))
                query = query.Where(e => e.program_type == program_type);

            if (!string.IsNullOrWhiteSpace(gender))
                query = query.Where(e => e.gender == gender);

            if (!string.IsNullOrWhiteSpace(item_code))
                query = query.Where(e => e.item_code == item_code);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(e =>
                    e.program_name.Contains(keyword) || e.item_code.Contains(keyword));
            }
            if(ac_year_id.HasValue)
            {
                query = query.Where(e => e.ac_year_id == ac_year_id.Value);
            }
            var result = await query.ToListAsync();
            var ongoingprogramcount = result.Count(x => x.status.ToLower() == "ongoing");

            if (result == null || !result.Any())
                return Ok(new { status = false, message = "Data not found" });

            return Ok(new { status = true, message = "Success", ongoingprogramcount= ongoingprogramcount, data = result });
        }
        //

        //participant history
        [HttpPut]
        [Route("update_participant_status")]
        public async Task<ActionResult> update_participant_status(
      [FromForm] int? program_id,
      [FromForm] int? student_id,
      [FromForm] string? history_type,
      [FromForm] int? created_user_id)
        {
            if (program_id == null)
                return Ok(new { status = false, message = "program_id is required" });

            if (student_id == null)
                return Ok(new { status = false, message = "student_id is required" });

            var participants = await _context.tbl_prgm_participants
                .Where(p => p.prgm_id == program_id && p.student_id == student_id && p.status.ToLower() != "deleted")
                .ToListAsync();

            if (participants == null || !participants.Any())
                return NotFound(new { status = false, message = "No active participants found for this program." });

            foreach (var participant in participants)
            {
                // Insert full record into tbl_participant_history
                var history = new participant_history
                {
                    prgm_id = participant.prgm_id,
                    student_id = participant.student_id,
                    chess_no = participant.chess_no,
                    token_no = participant.token_no,
                    average_point = participant.average_point,
                    program_status = participant.program_status,
                    user_id = participant.user_id,
                    status_updated = participant.status_updated,
                    grade = participant.grade,
                    grade_point = participant.grade_point,
                    position = participant.position,
                    position_point = participant.position_point,
                    total_point = participant.total_point,
                    ispresent = participant.ispresent,
                    remark = participant.remark,
                    group_name = participant.group_name,
                    status = participant.status,
                    addedon = DateTime.Now,
                    added_by = participant.added_by,
                    addedtype = participant.addedtype,
                    deletedon = participant.deletedon,
                    deleted_by = participant.deleted_by,
                    deleted_type = participant.deleted_type,
                    created_user_id = created_user_id,
                    history_type = history_type,
                };

                _context.tbl_participant_history.Add(history);

                // Reset participant fields after copying
                participant.chess_no = null;
                participant.token_no = null;
                participant.user_id = null;
                participant.status_updated = null;
                participant.program_status = "pending";
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Data updated Successfully"
            });
        }



        //

        //update participant status when student status is in chest number generated (new update)
      
        [HttpPost]
        [Route("update_late_student_status")]
        public async Task<ActionResult> update_late_student_status([FromForm] int? program_id, [FromForm] int? student_id, [FromForm] int? user_id)
        {
            if (program_id == null)
                return Ok(new { status = false, message = "Program ID is required." });

            if (student_id == null)
                return Ok(new { status = false, message = "Student ID is required." });

            // Fetch all active participants for this program and student
            var participants = await _context.tbl_prgm_participants
                .Where(p => p.prgm_id == program_id && p.status.ToLower() == "active" && p.student_id == student_id)
                .ToListAsync();

            if (participants.Count == 0)
                return Ok(new { status = false, message = "No active participants found for this program." });

            // Fetch the program and check if it is ongoing
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == program_id && p.delete_status.ToLower() == "active" && p.status.ToLower() == "ongoing");

            if (program == null)
                return Ok(new { status = false, message = "Program status is not ongoing." });

            foreach (var participant in participants)
            {
                participant.program_status = "Judgement Sheet Prepared";
                participant.status_updated = DateTime.Now;
                participant.user_id = user_id;                  
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Status updated successfully."
            });
        }

        //


        //update status as tabulation manager approved
       
        [HttpPost]
        [Route("update_program_status_new")]
        public async Task<ActionResult> update_program_status_new([FromForm] int? program_id, [FromForm] string? status)
        {
            if (program_id == null)
                return BadRequest(new { status = false, message = "program_id is required" });

            if (string.IsNullOrEmpty(status))
                return BadRequest(new { status = false, message = "Status is required" });

            if (status != "Tabulation Team Approved")
                return BadRequest(new { status = false, message = "Only 'Tabulation Team Approved' status is allowed." });

            // Fetch program first
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == program_id && p.delete_status.ToLower() == "active" && p.status == "Completed");

            if (program == null)
                return NotFound(new { status = false, message = "Program not found or status is not 'Completed'." });

            // Fetch all active judge entries for this program
            var judgePrograms = await _context.tbl_judge_prgm
                .Where(jp => jp.program_id == program_id && jp.status == "active")
                .ToListAsync();

            if (judgePrograms.Count > 0)
            {
                foreach (var jp in judgePrograms)
                {
                    jp.program_status = "approved";
                    jp.status_updated = DateTime.Now;
                }

                await _context.SaveChangesAsync();
            }

            // Update main program status
            program.status = "Tabulation Manager Approved";
            program.status_updated = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Status updated successfully."
            });
        }

        //edit judgement point
        //[HttpPut]
        //[Route("editprgmpoint")]
        //public async Task<ActionResult> editprgmpoint([FromBody] prgm_pointmodel request)
        //{
        //    if (_context.tbl_prgm_point == null)
        //        return Problem("Entity set '_context.tbl_prgm_point' is null.");

        //    if (!ModelState.IsValid)
        //    {
        //        var errorMessages = ModelState.Values
        //            .SelectMany(v => v.Errors)
        //            .Select(e => e.ErrorMessage)
        //            .ToList();

        //        return Ok(new { status = false, message = string.Join(", ", errorMessages) });
        //    }

        //    if (!request.prgm_id.HasValue || !request.student_id.HasValue || !request.judge_id.HasValue || !request.judgement_criteria_id.HasValue)
        //        return Ok(new { status = false, message = "prgm_id, student_id, judge_id and judgement_criteria_id are required" });


        //    // find existing point record
        //    var existing = await _context.tbl_prgm_point.FirstOrDefaultAsync(p =>
        //        p.prgm_id == request.prgm_id &&
        //        p.student_id == request.student_id &&
        //        p.judge_id == request.judge_id &&
        //        p.judgement_criteria_id == request.judgement_criteria_id);

        //    if (existing == null)
        //    {
        //        return Ok(new { status = false, message = "Point entry not found" });
        //    }

        //    // Validate against max allowed point for this criteria
        //    var maxAllowedPoint = await _context.tbl_prgm_judgement_criteria
        //        .Where(j => j.prgm_id == request.prgm_id && j.judgement_criteria == request.judgement_criteria_id)
        //        .Select(j => j.point)
        //        .FirstOrDefaultAsync();

        //    if (maxAllowedPoint == 0)
        //    {
        //        return Ok(new { status = false, message = "Criteria max point not configured." });
        //    }

        //    if (request.point.HasValue && request.point.Value > maxAllowedPoint)
        //    {
        //        return Ok(new { status = false, message = $"Entered point exceeds max allowed ({maxAllowedPoint})." });
        //    }

        //    // update fields
        //    existing.point = request.point;
        //    existing.comments = request.comments;
        //    existing.addedon = DateTime.Now;
        //   existing.remark = "mark updated";

        //    // reuse addedon for timestamp; adjust if you have separate modified column
        //    _context.Entry(existing).State = EntityState.Modified;

        //    await _context.SaveChangesAsync();

        //    // Recalculate averages and grades for affected program participants
        //    int prgmId = request.prgm_id.GetValueOrDefault();

        //    var program = await _context.tbl_program.Where(p => p.id == prgmId).FirstOrDefaultAsync();
        //    if (program == null)
        //        return Ok(new { status = true, message = "Point updated but program not found for recalculation." });

        //    int noOfJudges = await _context.tbl_judge_prgm
        //        .Where(j => j.program_id == prgmId && j.status != "deleted")
        //        .Select(j => j.judge_id)
        //        .Distinct()
        //        .CountAsync();

        //    var participants = await _context.tbl_prgm_participants
        //        .Where(p => p.prgm_id == prgmId && p.chess_no != null && p.status != "deleted")
        //        .ToListAsync();

        //    foreach (var participant in participants)
        //    {
        //        int studentId = participant.student_id ?? 0;

        //        var pointsQuery = _context.tbl_prgm_point
        //            .Where(p => p.prgm_id == prgmId && p.student_id == studentId);

        //        var totalPoints = await pointsQuery.SumAsync(p => (int?)p.point) ?? 0;
        //        var judgeCount = await pointsQuery.Select(p => p.judge_id).Distinct().CountAsync();

        //        var averagePoints = judgeCount > 0
        //            ? Math.Round((double)totalPoints / judgeCount, 2)
        //            : 0;

        //        participant.average_point = averagePoints;

        //        var participantProgramType = await _context.tbl_program
        //            .Where(p => p.id == participant.prgm_id)
        //            .Select(p => p.program_type)
        //            .FirstOrDefaultAsync();

        //        int gradeTypeId = (participantProgramType != null && participantProgramType.ToLower().Trim() == "group") ? 2 : 1;

        //        var grade = await _context.tbl_grade
        //            .Where(g => g.gradetype_id == gradeTypeId &&
        //                        averagePoints >= g.from_point &&
        //                        averagePoints <= g.to_point)
        //            .OrderByDescending(g => g.point)
        //            .FirstOrDefaultAsync();

        //        if (grade != null)
        //        {
        //            participant.grade = grade.grade;
        //            participant.grade_point = grade.point;
        //        }

        //        _context.Entry(participant).State = EntityState.Modified;
        //    }

        //    await _context.SaveChangesAsync();

        //    // Check if all judges have submitted for each student; if yes, finalize program and update ranks
        //    bool allScored = true;
        //    foreach (var student in participants)
        //    {
        //        var distinctJudgeCount = await _context.tbl_prgm_point
        //            .Where(p => p.prgm_id == prgmId && p.student_id == student.student_id)
        //            .Select(p => p.judge_id)
        //            .Distinct()
        //            .CountAsync();

        //        if (distinctJudgeCount < noOfJudges)
        //        {
        //            allScored = false;
        //            break;
        //        }
        //    }

        //    if (allScored)
        //    {
        //        program.status = "Completed";
        //        _context.Entry(program).State = EntityState.Modified;

        //        foreach (var p in participants)
        //        {
        //            p.program_status = "Judgement Completed";
        //            _context.Entry(p).State = EntityState.Modified;
        //        }

        //        await _context.SaveChangesAsync();

        //        // Recalculate ranks and points
        //        await UpdateRanksAndPointsForProgram(prgmId);
        //    }

        //    return Ok(new { status = true, message = "Point updated successfully" });
        //}

        [HttpPut]
        [Route("editprgmpoint")]
        [HttpPost]
        public async Task<ActionResult> editprgmpoint([FromBody] List<prgm_pointmodel> requests)
        {
            if (_context.tbl_prgm_point == null)
                return Problem("Entity set '_context.tbl_prgm_point' is null.");

            if (!ModelState.IsValid)
            {
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Ok(new { status = false, message = string.Join(", ", errorMessages) });
            }

            if (requests == null || !requests.Any())
            {
                return Ok(new { status = false, message = "No data received" });
            }

            var firstRequest = requests.First();

            if (!firstRequest.prgm_id.HasValue ||
                !firstRequest.student_id.HasValue ||
                !firstRequest.judge_id.HasValue)
            {
                return Ok(new
                {
                    status = false,
                    message = "prgm_id, student_id and judge_id are required"
                });
            }

            // Ensure program exists and editing is allowed
            var programStatus = await _context.tbl_program
                .Where(p => p.id == firstRequest.prgm_id)
                .Select(p => p.status)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrEmpty(programStatus) &&
                programStatus.Equals("Tabulation Team Approved", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new
                {
                    status = false,
                    message = "Cannot edit marks after Tabulation Team Approved"
                });
            }


            // Update all criteria marks
            foreach (var request in requests)
            {
                if (!request.prgm_id.HasValue ||
                    !request.student_id.HasValue ||
                    !request.judge_id.HasValue ||
                    !request.judgement_criteria_id.HasValue)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "prgm_id, student_id, judge_id and judgement_criteria_id are required"
                    });
                }

                var existing = await _context.tbl_prgm_point.FirstOrDefaultAsync(p =>
                    p.prgm_id == request.prgm_id &&
                    p.student_id == request.student_id &&
                    p.judge_id == request.judge_id &&
                    p.judgement_criteria_id == request.judgement_criteria_id);

                if (existing == null)
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Point entry not found for criteria {request.judgement_criteria_id}"
                    });
                }

                // Validate max point
                var maxAllowedPoint = await _context.tbl_prgm_judgement_criteria
                    .Where(j =>
                        j.prgm_id == request.prgm_id &&
                        j.judgement_criteria == request.judgement_criteria_id)
                    .Select(j => j.point)
                    .FirstOrDefaultAsync();

                if (maxAllowedPoint == 0)
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Criteria max point not configured for criteria {request.judgement_criteria_id}"
                    });
                }

                if (request.point.HasValue && request.point.Value > maxAllowedPoint)
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Entered point exceeds max allowed ({maxAllowedPoint}) for criteria {request.judgement_criteria_id}"
                    });
                }

                // Update mark
                existing.point = request.point;
                existing.comments = request.comments;
                existing.addedon = DateTime.Now;
                existing.remark = "mark updated";

                _context.Entry(existing).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            // Recalculate averages and grades
            int prgmId = firstRequest.prgm_id.GetValueOrDefault();

            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == prgmId);

            if (program == null)
            {
                return Ok(new
                {
                    status = true,
                    message = "Point updated but program not found for recalculation."
                });
            }

            int noOfJudges = await _context.tbl_judge_prgm
                .Where(j => j.program_id == prgmId && j.status != "deleted")
                .Select(j => j.judge_id)
                .Distinct()
                .CountAsync();

            var participants = await _context.tbl_prgm_participants
                .Where(p =>
                    p.prgm_id == prgmId &&
                    p.chess_no != null &&
                    p.status != "deleted")
                .ToListAsync();

            var participantProgramType = await _context.tbl_program
                .Where(p => p.id == prgmId)
                .Select(p => p.program_type)
                .FirstOrDefaultAsync();

            int gradeTypeId =
                participantProgramType != null &&
                participantProgramType.ToLower().Trim() == "group"
                    ? 2
                    : 1;

            // Get Academic Year Id
            int acYearId = program.ac_year_id ?? 0;

            foreach (var participant in participants)
            {
                int studentId = participant.student_id ?? 0;

                var pointsQuery = _context.tbl_prgm_point
                    .Where(p =>
                        p.prgm_id == prgmId &&
                        p.student_id == studentId);

                var totalPoints = await pointsQuery.SumAsync(p => (decimal?)p.point) ?? 0;

                var judgeCount = await pointsQuery
                    .Select(p => p.judge_id)
                    .Distinct()
                    .CountAsync();

                var averagePoints = judgeCount > 0
                    ? Math.Round((double)(totalPoints / judgeCount), 2)
                    : 0;

                participant.average_point = averagePoints;

                // ---------- Grade Calculation ----------
                if (averagePoints < 40)
                {
                    participant.grade = null;
                    participant.grade_point = 0;
                }
                else
                {
                    var grade = await _context.tbl_grade
                        .Where(g =>
                            g.ac_year_id == acYearId &&
                            g.gradetype_id == gradeTypeId &&
                            averagePoints >= g.from_point &&
                            averagePoints <= g.to_point)
                        .OrderByDescending(g => g.point)
                        .FirstOrDefaultAsync();

                    if (grade != null)
                    {
                        participant.grade = grade.grade;
                        participant.grade_point = grade.point;
                    }
                    else
                    {
                        participant.grade = null;
                        participant.grade_point = 0;
                    }
                }

                _context.Entry(participant).State = EntityState.Modified;
            }
            await _context.SaveChangesAsync();

            // Check whether all judges completed scoring
            bool allScored = true;

            foreach (var participant in participants)
            {
                var distinctJudgeCount = await _context.tbl_prgm_point
                    .Where(p =>
                        p.prgm_id == prgmId &&
                        p.student_id == participant.student_id)
                    .Select(p => p.judge_id)
                    .Distinct()
                    .CountAsync();

                if (distinctJudgeCount < noOfJudges)
                {
                    allScored = false;
                    break;
                }
            }

            if (allScored)
            {
                // Program becomes completed
                if (!string.Equals(program.status, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    program.status = "Completed";
                    program.status_updated = DateTime.Now;
                    _context.Entry(program).State = EntityState.Modified;
                }

                foreach (var participant in participants)
                {
                    participant.program_status = "Judgement Completed";
                    _context.Entry(participant).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync();
            }

            // If program is completed (either previously or just now),
            // always recalculate positions after editing marks.
            if (string.Equals(program.status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                await UpdateRanksAndPointsForProgram(prgmId);
            }
            return Ok(new
            {
                status = true,
                message = "Points updated successfully"
            });
        }
        //[HttpPut]
        //[Route("generate_chestno")]
        //public async Task<ActionResult> generate_chestno([FromForm] int? program_id, [FromForm] int? student_id)
        //{
        //    if (program_id == null)
        //    {
        //        return Ok(new { status = false, message = "program is required" });
        //    }

        //    if (student_id == null)
        //    {
        //        return Ok(new { status = false, message = "student is required" });
        //    }

        //    var participant = await _context.tbl_prgm_participants
        //        .FirstOrDefaultAsync(p => p.prgm_id == program_id
        //                               && p.student_id == student_id
        //                               && p.status != "deleted");

        //    if (participant == null)
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Participant not found for the given program and student"
        //        });
        //    }

        //    // Check program status
        //    if (participant.program_status != "Verification Completed")
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Student not verified"
        //        });
        //    }

        //    // Check if chest number already assigned
        //    if (!string.IsNullOrEmpty(participant.chess_no))
        //    {
        //        return Ok(new
        //        {
        //            status = false,
        //            message = "Chest number already assigned for this student in the selected program."
        //        });
        //    }

        //    int? acYearId = participant.ac_year_id;

        //    // Get stage for this program
        //    var stageId = await _context.tbl_program
        //        .Where(p => p.id == program_id)
        //        .Select(p => p.stage_id)
        //        .FirstOrDefaultAsync();

        //    // Collect chest numbers for the same stage + same academic year
        //    var participantNos = await (
        //        from pp in _context.tbl_prgm_participants
        //        join pr in _context.tbl_program on pp.prgm_id equals pr.id
        //        where pp.status != "deleted"
        //              && pp.ac_year_id == acYearId
        //              && pr.stage_id == stageId
        //              && !string.IsNullOrEmpty(pp.chess_no)
        //        select pp.chess_no
        //    ).ToListAsync();

        //    var groupNos = await (
        //        from gm in _context.tbl_group_members
        //        join pr in _context.tbl_program on gm.prgm_id equals pr.id
        //        where pr.stage_id == stageId
        //              && pr.ac_year_id == acYearId
        //              && !string.IsNullOrEmpty(gm.chest_no)
        //        select gm.chest_no
        //    ).Distinct().ToListAsync();

        //    var allChestNos = participantNos
        //        .Concat(groupNos)
        //        .Where(x => int.TryParse(x, out _))
        //        .Select(int.Parse)
        //        .Distinct()
        //        .ToList();

        //    // Stage-based seed: ensures each stage has its own numeric range.
        //    // Simple formula: stageSeed = (stageId ?? 0) * 1000
        //    // If you prefer per-stage custom start values, add a column to tbl_stage (e.g. chest_start) and read it here.
        //    int stageSeed = (stageId ?? 0) * 1000;
        //    int nextNumber;

        //    if (!allChestNos.Any())
        //    {
        //        // start at stageSeed + 1, but keep small numbers if stageSeed == 0
        //        nextNumber = stageSeed > 0 ? stageSeed + 1 : 1;
        //    }
        //    else
        //    {
        //        int maxExisting = allChestNos.Max();
        //        // ensure nextNumber is at least stageSeed+1
        //        nextNumber = Math.Max(maxExisting + 1, stageSeed > 0 ? stageSeed + 1 : 1);
        //    }

        //    string chest_no = nextNumber.ToString("D3");

        //    // Assign chest number to main participant record
        //    participant.chess_no = chest_no;
        //    participant.program_status = "Chest Number Generated";

        //    // Update chest number in group members table for the same group & institute (if any)
        //    var student = await _context.tbl_student.FirstOrDefaultAsync(s => s.id == participant.student_id);
        //    var instituteId = student?.institute;
        //    var groupName = participant.group_name;

        //    if (!string.IsNullOrEmpty(groupName) && instituteId != null)
        //    {
        //        var groupMembers = await _context.tbl_group_members
        //            .Where(g => g.group_name == groupName
        //                     && g.instit_id == instituteId
        //                     && g.prgm_id == program_id)
        //            .ToListAsync();

        //        foreach (var member in groupMembers)
        //        {
        //            member.chest_no = chest_no;
        //            _context.Entry(member).State = EntityState.Modified;
        //        }
        //    }

        //    _context.Entry(participant).State = EntityState.Modified;
        //    await _context.SaveChangesAsync();

        //    return Ok(new
        //    {
        //        status = true,
        //        chest_no = chest_no,
        //        message = "Chest number generated successfully"
        //    });
        //}




        [HttpGet]
        [Route("backstage_programs_status_list")]
        public async Task<ActionResult> backstage_programs_status_list(
    string? program_status,
    int? program_id = null,
    int? institute_id = null,
    int? stage_id = null,
    string? item_code = null,
    int? ac_year_id = null, DateTime? date = null)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            var data = await (
                from pp in _context.tbl_prgm_participants
                join prog in _context.tbl_program on pp.prgm_id equals prog.id
                join stud in _context.tbl_student on pp.student_id equals stud.id

                join ins in _context.tbl_institute on stud.institute equals ins.id into inst
                from ins in inst.DefaultIfEmpty()

                join cls in _context.tbl_class on stud.class_id equals cls.id into classJoin
                from cls in classJoin.DefaultIfEmpty()

                join div in _context.tbl_division on stud.division_id equals div.id into divJoin
                from div in divJoin.DefaultIfEmpty()

                join sta in _context.tbl_stage on prog.stage_id equals sta.id into staJoin
                from sta in staJoin.DefaultIfEmpty()
                where pp.status != "deleted"
                    &&(program_status == null || pp.program_status.ToLower() == program_status.ToLower())
                    && (program_id == null || pp.prgm_id == program_id)
                    && (institute_id == null || stud.institute == institute_id)
                    && (stage_id == null || prog.stage_id == stage_id)
                    && (item_code == null || prog.item_code == item_code)
                    && (ac_year_id == null || prog.ac_year_id == ac_year_id)
                    && (date == null || prog.date == date)

                select new
                {
                    id = pp.prgm_id,
                    student_id = pp.student_id,
                    qid=stud.admsn_no,
                    program = prog.program_name,
                    item_code = prog.item_code,
                    date=prog.date,
                    chess_no = pp.chess_no,
                    token_no = pp.token_no,
                    group_name = pp.group_name,
                    name = stud.name,
                    admsn_no = stud.admsn_no,
                    phone_no = stud.phone_no,
                    institute_id = stud.institute,
                    institute_name = ins != null ? ins.name : null,
                    class_name = cls != null ? cls.@class : null,
                    division_name = div != null ? div.division : null,
                    image = stud.image,
                    imageurl = !string.IsNullOrEmpty(stud.image)
                        ? $"{baseUrl}uploads/student/{stud.image}"
                        : null,
                    status = pp.program_status,
                    total_points = pp.average_point ?? 0,
                    prog.stage_id,
                    stage=sta.stage_name

                })
                .ToListAsync();

            if (!data.Any())
            {
                return Ok(new
                {
                    status = false,
                    message = "No data found."
                });
            }

            return Ok(new
            {
                status = true,
                message = "Success",
                count = data.Count,
                data = data
            });
        }



        [HttpGet]
        [Route("program_pending_count")]
        public async Task<ActionResult> program_pending_count(
       string? program_status,
       int? program_id = null,
       int? stage_id = null,
       string? item_code = null,
       int? ac_year_id = null,
       DateTime? date = null)
        {
            var data = _context.tbl_program.AsQueryable();

            if (program_id.HasValue)
                data = data.Where(x => x.id == program_id);

            if (stage_id.HasValue)
                data = data.Where(x => x.stage_id == stage_id);

            if (!string.IsNullOrEmpty(item_code))
                data = data.Where(x => x.item_code == item_code);

            if (ac_year_id.HasValue)
                data = data.Where(x => x.ac_year_id == ac_year_id);

            if (date.HasValue)
                data = data.Where(x => x.date== date.Value.Date);

            if (!string.IsNullOrEmpty(program_status))
                data = data.Where(x => x.status == program_status);

            var result = await data.ToListAsync();

            if (!result.Any())
            {
                return Ok(new
                {
                    status = false,
                    message = "No data found."
                });
            }

            return Ok(new
            {
                status = true,
                message = "Success",
                data = result
            });
        }


       
[HttpGet]
    [Route("export_program_students")]
    public async Task<IActionResult> ExportProgramStudents(int program_id, int ac_year_id)
    {
        var program = await (
            from p in _context.tbl_program
            join st in _context.tbl_stage on p.stage_id equals st.id into stageJoin
            from stage in stageJoin.DefaultIfEmpty()
            where p.id == program_id && p.ac_year_id == ac_year_id
            select new
            {
                p.program_name,
                p.item_code,
                p.program_type,
                p.participant_type,
                Stage = stage != null ? stage.stage_name : "",
                p.date
            }).FirstOrDefaultAsync();

        if (program == null)
            return NotFound("Program not found.");

        var students = await (
            from pp in _context.tbl_prgm_participants
            join s in _context.tbl_student on pp.student_id equals s.id
            join i in _context.tbl_institute on s.institute equals i.id into inst
            from institute in inst.DefaultIfEmpty()
            where pp.prgm_id == program_id
                  && pp.ac_year_id == ac_year_id
            orderby pp.average_point descending
            select new
            {
                AdmissionNo = s.admsn_no,
                StudentName = s.name,
                Institute = institute != null ? institute.name : "",
                ChestNo = pp.chess_no,
                Marks = pp.average_point,
                Position = pp.position,
                PositionPoint = pp.position_point
            }).ToListAsync();

        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Student Marks");

            // ================= Program Details =================
            ws.Cell("A1").Value = "PROGRAM DETAILS";
            ws.Range("A1:B1").Merge();
            ws.Range("A1:B1").Style.Font.Bold = true;
            ws.Range("A1:B1").Style.Font.FontSize = 16;
            ws.Range("A1:B1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell("A3").Value = "Program Name";
            ws.Cell("B3").Value = program.program_name;

            ws.Cell("A4").Value = "Item Code";
            ws.Cell("B4").Value = program.item_code;

            ws.Cell("A5").Value = "Program Type";
            ws.Cell("B5").Value = program.program_type;

            ws.Cell("A6").Value = "Participant Type";
            ws.Cell("B6").Value = program.participant_type;

            ws.Cell("A7").Value = "Stage";
            ws.Cell("B7").Value = program.Stage;

            ws.Cell("A8").Value = "Date";
            ws.Cell("B8").Value = program.date?.ToString("dd-MM-yyyy");

            ws.Cell("A9").Value = "Total Participants";
            ws.Cell("B9").Value = students.Count;
                // Participants Heading
                ws.Range("A11:H11").Merge();
                ws.Cell("A11").Value = "PARTICIPANT DETAILS";
                ws.Cell("A11").Style.Font.Bold = true;
                ws.Cell("A11").Style.Font.FontSize = 14;
                ws.Cell("A11").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell("A11").Style.Fill.BackgroundColor = XLColor.LightBlue;
                ws.Cell("A11").Style.Font.FontColor = XLColor.DarkBlue;
                // ================= Student Header =================
                int headerRow = 14;

            ws.Cell(headerRow, 1).Value = "Sl No";
            ws.Cell(headerRow, 2).Value = "Admission No";
            ws.Cell(headerRow, 3).Value = "Student Name";
            ws.Cell(headerRow, 4).Value = "Institute";
            ws.Cell(headerRow, 5).Value = "Chest No";
            ws.Cell(headerRow, 6).Value = "Marks";
            ws.Cell(headerRow, 7).Value = "Position";
            ws.Cell(headerRow, 8).Value = "Position Point";

            var header = ws.Range(headerRow, 1, headerRow, 8);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // ================= Student Data =================
            int row = headerRow + 1;
            int slNo = 1;

            foreach (var student in students)
            {
                ws.Cell(row, 1).Value = slNo++;
                ws.Cell(row, 2).Value = student.AdmissionNo;
                ws.Cell(row, 3).Value = student.StudentName;
                ws.Cell(row, 4).Value = student.Institute;
                ws.Cell(row, 5).Value = student.ChestNo;
                ws.Cell(row, 6).Value = student.Marks;
                ws.Cell(row, 7).Value = student.Position;
                ws.Cell(row, 8).Value = student.PositionPoint;
                row++;
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents();

            // Border for program details
            ws.Range("A3:B9").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range("A3:B9").Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Border for student table
            ws.Range(headerRow, 1, row - 1, 8).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, row - 1, 8).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"{program.program_name}_StudentMarks.xlsx");
            }
        }
    }
        [HttpPost]
        [Route("upload_program_excel")]
        public async Task<IActionResult> UploadProgramExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Ok(new
                {
                    status = false,
                    message = "Please upload an Excel file."
                });
            }

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);

                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);

                    var headers = worksheet.FirstRowUsed().Cells()
                        .ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber);

                    foreach (var row in worksheet.RowsUsed().Skip(1))
                    {
                        var program = new programmodel
                        {
                            stage_id = 1,

                            program_name = GetString(row, headers, nameof(programmodel.program_name)),
                            date = DateTime.Now,
                            time = GetString(row, headers, nameof(programmodel.time)),
                            addedon = DateTime.Now,
                            status = "pending",
                            no_of_regstn_per_institute = GetInt(row, headers, nameof(programmodel.no_of_regstn_per_institute)),
                            program_type = GetString(row, headers, nameof(programmodel.program_type)),
                            gender = GetString(row, headers, nameof(programmodel.gender)),
                            participant_type = GetString(row, headers, nameof(programmodel.participant_type)),
                            offstage_onstage = "Onstage",
                            item_code = GetString(row, headers, nameof(programmodel.item_code)),
                            group_min_participants = GetInt(row, headers, nameof(programmodel.group_min_participants)),
                            group_max_participants = GetInt(row, headers, nameof(programmodel.group_max_participants)),
                            no_of_group = GetString(row, headers, nameof(programmodel.no_of_group)),
                            ac_year_id = 3,
                            delete_status= "active",
                            addedtype="admin",
                            added_by=1,
                        };

                        _context.tbl_program.Add(program);
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new
            {
                status = true,
                message = "Programs uploaded successfully."
            });
        }
        // Add these helper methods inside your programController class (or as static methods in the same file)
        private string? GetString(IXLRow row, Dictionary<string, int> headers, string propertyName)
        {
            if (headers.TryGetValue(propertyName, out int col))
            {
                return row.Cell(col).GetString();
            }
            return null;
        }

        private int? GetInt(IXLRow row, Dictionary<string, int> headers, string propertyName)
        {
            if (headers.TryGetValue(propertyName, out int col))
            {
                var val = row.Cell(col).GetString();
                if (int.TryParse(val, out int result))
                    return result;
            }
            return null;
        }

        private DateTime? GetDate(IXLRow row, Dictionary<string, int> headers, string propertyName)
        {
            if (headers.TryGetValue(propertyName, out int col))
            {
                var val = row.Cell(col).GetString();
                if (DateTime.TryParse(val, out DateTime result))
                    return result;
            }
            return null;
        }

        [HttpGet]
        [Route("program_with_judgement_criteria")]
        public async Task<ActionResult> program_with_judgement_criteria(int? id,  int? ac_year_id)
        {
            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower() == "judge");

            // FIX: avoid direct (int) cast on a possibly-null reference_id
            int ref_id = 0;
            if (user != null)
            {
                ref_id = (int)(user.reference_id ?? 0);
            }

            if (_context.tbl_program == null)
                return NotFound(new { status = false, message = "Data not found" });

            var query = from p in _context.tbl_program
                        join s in _context.tbl_stage on p.stage_id equals s.id into sta
                        from st in sta.DefaultIfEmpty()
                        where p.delete_status == "active"
                        select new
                        {
                            p.id,
                            p.program_type,
                            p.participant_type,
                            p.gender,
                            p.stage_id,
                            stage_name = st != null ? st.stage_name : "",
                            p.program_name,
                            p.color_code,
                            p.date,
                            p.time,
                            p.added_by,
                            p.addedtype,
                            p.addedon,
                            p.status,
                            p.item_code,
                            p.offstage_onstage,
                            p.group_min_participants,
                            p.group_max_participants,
                            p.no_of_group,
                            p.ac_year_id,
                            participant_count = p.program_type.ToLower() == "group"
                                ? (from gm in _context.tbl_group_members
                                   join pp in _context.tbl_prgm_participants on gm.prgm_id equals pp.prgm_id
                                   where pp.prgm_id == p.id && pp.status.ToLower() != "deleted"
                                   select gm.id).Count()
                                : _context.tbl_prgm_participants
                                    .Count(pp => pp.prgm_id == p.id && pp.status.ToLower() != "deleted"),

                            judgement_criteria = _context.tbl_prgm_judgement_criteria
                                .Where(c => c.prgm_id == p.id)
                                .Join(_context.tbl_judgement_criteria,
                                      c => c.judgement_criteria,
                                      j => j.id,
                                      (c, j) => new
                                      {
                                          c.id,
                                          c.prgm_id,
                                          judgement_criteria = c.judgement_criteria,
                                          name = j != null ? j.name : "",
                                          point = c.point
                                      }).ToList(),

                         
                        };

            if (id.HasValue)
                query = query.Where(g => g.id == id.Value);
            if (ac_year_id.HasValue)
                query = query.Where(g => g.ac_year_id == ac_year_id.Value);

           
            var result = await query.ToListAsync();

            if (result == null || !result.Any())
                return Ok(new { status = false, message = "Data not found" });

            return Ok(new { status = true, message = "Success", data = result });
        }
        [HttpGet]
        [Route("programs_with_students_and_judges")]
        public async Task<ActionResult> programs_with_students_and_judges(int? id, int? ac_year_id, int? institute_id, string? keyword, string? chest_no)
        {
            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower() == "judge");
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            // FIX: avoid direct (int) cast on a possibly-null reference_id
            int ref_id = 0;
            if (user != null)
            {
                ref_id = (int)(user.reference_id ?? 0);
            }

            if (_context.tbl_program == null)
                return NotFound(new { status = false, message = "Data not found" });

            var query = from p in _context.tbl_program
                        join s in _context.tbl_stage on p.stage_id equals s.id into sta
                        from st in sta.DefaultIfEmpty()
                        where p.delete_status == "active"
                        select new
                        {
                            p.id,
                            p.program_type,
                            p.participant_type,
                            p.gender,
                            p.stage_id,
                            stage_name = st != null ? st.stage_name : "",
                            p.program_name,
                            p.color_code,
                            p.date,
                            p.time,
                            p.added_by,
                            p.addedtype,
                            p.addedon,
                            p.status,
                            p.item_code,
                            p.offstage_onstage,
                            p.group_min_participants,
                            p.group_max_participants,
                            p.no_of_group,
                            p.ac_year_id,
                            participant_count = p.program_type.ToLower() == "group"
                                ? (from gm in _context.tbl_group_members
                                   join pp in _context.tbl_prgm_participants on gm.prgm_id equals pp.prgm_id
                                   where pp.prgm_id == p.id && pp.status.ToLower() != "deleted"
                                   select gm.id).Count()
                                : _context.tbl_prgm_participants
                                    .Count(pp => pp.prgm_id == p.id && pp.status.ToLower() != "deleted"),


                            //program judges
                            judges = _context.tbl_judge_prgm
                                .Where(c => c.program_id == p.id && c.status == "active")
                                .Join(_context.tbl_judge,
                                      c => c.judge_id,
                                      j => j.id,
                                      (c, j) => new
                                      {
                                          c.id,
                                          c.judge_id,
                                          judge = j.judge_name
                                      })
                                .ToList(),

                            // FIX: both branches now return the SAME anonymous shape
                            // (same property names, same types, same order) so EF Core
                            // can translate the conditional expression. No more (object) cast needed.
                            prgm_student_detail = p.program_type.ToLower() == "group"
                                ? (from gd in _context.tbl_group_members
                                   join s2 in _context.tbl_student on gd.student_id equals s2.id
                                   join i in _context.tbl_institute on s2.institute equals i.id

                                   join gp in _context.tbl_prgm_participants
                                       .Where(g => g.status.ToLower() != "deleted")
                                       on new { gd.prgm_id, gd.student_id } equals new { gp.prgm_id, gp.student_id }
                                       into gpJoin
                                   from gp in gpJoin.DefaultIfEmpty()

                                   where gd.prgm_id == p.id
                                         && (!institute_id.HasValue || s2.institute == institute_id)
                                   orderby s2.name
                                   select new
                                   {
                                       student_id = (int?)gd.student_id,
                                       student_name = s2.name,
                                       institute = (int?)s2.institute,
                                       institute_name = i.name,
                                       phone_no = s2.phone_no,
                                       admsn_no = s2.admsn_no,
                                       email = s2.email,
                                       chest_no = gp != null ? gp.chess_no : null,
                                       status = gp != null ? gp.program_status : null,
                                       group_name = gd.group_name,
                                       point = (double?)null,
                                       judgement_criteria_id = (int?)null,
                                       imageurl = !string.IsNullOrEmpty(s2.image) ? $"{baseUrl}uploads/student/{s2.image}" : null,

                                   }).ToList()
                                : (from sp in _context.tbl_prgm_participants
                                   join st in _context.tbl_student on sp.student_id equals st.id into std
                                   from student in std.DefaultIfEmpty()
                                   join i in _context.tbl_institute on student.institute equals i.id into inst
                                   from ins in inst.DefaultIfEmpty()
                                   join pp in _context.tbl_prgm_point.Where(x => x.judge_id == ref_id)
                                       on new
                                       {
                                           sp.chess_no,
                                           sp.student_id,
                                           sp.prgm_id
                                       }
                                       equals new
                                       {
                                           pp.chess_no,
                                           pp.student_id,
                                           pp.prgm_id
                                       }
                                       into ppJoin
                                   from pp in ppJoin.DefaultIfEmpty()

                                   where sp.prgm_id == p.id
                                         && sp.status.ToLower() != "deleted"
                                         && (!institute_id.HasValue || (student != null && student.institute == institute_id))
                                         && (string.IsNullOrWhiteSpace(chest_no) || sp.chess_no == chest_no)
                                   orderby student != null ? student.name : ""
                                   select new
                                   {
                                       student_id = student != null ? (int?)student.id : null,
                                       student_name = student != null ? student.name : "",
                                       institute = student != null ? (int?)student.institute : null,
                                       institute_name = ins != null ? ins.name : "",
                                       phone_no = student != null ? student.phone_no : "",
                                       admsn_no = student != null ? student.admsn_no : "",
                                       email = student != null ? student.email : "",
                                       chest_no = sp.chess_no,
                                       status = sp.program_status,
                                       group_name = (string?)null,
                                       point = pp.point,
                                       judgement_criteria_id = pp.judgement_criteria_id,
                                       imageurl = !string.IsNullOrEmpty(student.image) ? $"{baseUrl}uploads/student/{student.image}" : null,

                                   }).ToList()
                        };

            if (id.HasValue)
                query = query.Where(g => g.id == id.Value);


            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(e =>
                    e.program_name.Contains(keyword) || e.item_code.Contains(keyword));
            }
            if (ac_year_id.HasValue)
                query = query.Where(g => g.ac_year_id == ac_year_id.Value);

            var result = await query.ToListAsync();

            if (result == null || !result.Any())
                return Ok(new { status = false, message = "Data not found" });

            return Ok(new { status = true, message = "Success", data = result });
        }

        //    public async Task<ActionResult> programs_with_students_and_judges(
        //int? id,
        //int? ac_year_id,
        //int? institute_id,
        //string? keyword,
        //string? chest_no)
        //    {
        //        var enc_key = Request.Headers["XapiKey"].ToString();

        //        // ============================================================
        //        // GET JUDGE
        //        // ============================================================

        //        var user = await _context.tbl_user
        //            .AsNoTracking()
        //            .FirstOrDefaultAsync(u =>
        //                u.enc_key == enc_key &&
        //                u.type.ToLower() == "judge");

        //        int ref_id = user?.reference_id ?? 0;

        //        var baseUrl = $"{Request.Scheme}://{Request.Host}/";

        //        if (_context.tbl_program == null)
        //            return NotFound(new
        //            {
        //                status = false,
        //                message = "Data not found"
        //            });


        //        // ============================================================
        //        // 1. GET PROGRAMS
        //        // ============================================================

        //        var programQuery =
        //            from p in _context.tbl_program.AsNoTracking()

        //            join s in _context.tbl_stage.AsNoTracking()
        //                on p.stage_id equals s.id into sta

        //            from st in sta.DefaultIfEmpty()

        //            where p.delete_status == "active" 

        //            select new
        //            {
        //                p.id,
        //                p.program_type,
        //                p.participant_type,
        //                p.gender,
        //                p.stage_id,
        //                stage_name = st != null ? st.stage_name : "",
        //                p.program_name,
        //                p.color_code,
        //                p.date,
        //                p.time,
        //                p.added_by,
        //                p.addedtype,
        //                p.addedon,
        //                p.status,
        //                p.item_code,
        //                p.offstage_onstage,
        //                p.group_min_participants,
        //                p.group_max_participants,
        //                p.no_of_group,
        //                p.ac_year_id
        //            };


        //        // Apply filters BEFORE loading programs

        //        if (id.HasValue)
        //            programQuery = programQuery.Where(x => x.id == id.Value);

        //        if (ac_year_id.HasValue)
        //            programQuery = programQuery.Where(x => x.ac_year_id == ac_year_id.Value);

        //        if (!string.IsNullOrWhiteSpace(keyword))
        //        {
        //            programQuery = programQuery.Where(x =>
        //                x.program_name.Contains(keyword) ||
        //                x.item_code.Contains(keyword));
        //        }


        //        var programs = await programQuery.ToListAsync();

        //        if (!programs.Any())
        //        {
        //            return Ok(new
        //            {
        //                status = false,
        //                message = "Data not found"
        //            });
        //        }


        //        var programIds = programs
        //            .Select(x => x.id)
        //            .ToList();


        //        // ============================================================
        //        // 2. GET JUDGES
        //        // ============================================================

        //        var judgeData = await (
        //            from c in _context.tbl_judge_prgm.AsNoTracking()

        //            join j in _context.tbl_judge.AsNoTracking()
        //                on c.judge_id equals j.id

        //            where c.program_id.HasValue
        //                  && programIds.Contains(c.program_id.Value)
        //                  && c.status == "active"

        //            select new
        //            {
        //                program_id = c.program_id.Value,
        //                id = c.id,
        //                judge_id = c.judge_id,
        //                judge = j.judge_name
        //            }
        //        ).ToListAsync();


        //        var judgesByProgram = judgeData
        //            .GroupBy(x => x.program_id)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g.Select(x => new
        //                {
        //                    x.id,
        //                    x.judge_id,
        //                    x.judge
        //                }).ToList()
        //            );


        //        // ============================================================
        //        // 3. GROUP / NORMAL PROGRAM IDS
        //        // ============================================================

        //        var groupProgramIds = programs
        //            .Where(x =>
        //                x.program_type != null &&
        //                x.program_type.ToLower() == "group")
        //            .Select(x => x.id)
        //            .ToList();

        //        var normalProgramIds = programs
        //            .Where(x =>
        //                x.program_type == null ||
        //                x.program_type.ToLower() != "group")
        //            .Select(x => x.id)
        //            .ToList();


        //        // ============================================================
        //        // 4. GET GROUP STUDENTS
        //        // ============================================================

        //        var groupStudentData = await (
        //            from gd in _context.tbl_group_members.AsNoTracking()

        //            join s2 in _context.tbl_student.AsNoTracking()
        //                on gd.student_id equals s2.id

        //            join i in _context.tbl_institute.AsNoTracking()
        //                on s2.institute equals i.id

        //            join gp0 in _context.tbl_prgm_participants
        //                .AsNoTracking()
        //                .Where(g => g.status.ToLower() != "deleted")

        //                on new
        //                {
        //                    gd.prgm_id,
        //                    gd.student_id
        //                }
        //                equals new
        //                {
        //                    prgm_id = gp0.prgm_id,
        //                    student_id = gp0.student_id
        //                }

        //                into gpJoin

        //            from gp in gpJoin.DefaultIfEmpty()

        //            where gd.prgm_id.HasValue
        //                  && groupProgramIds.Contains(gd.prgm_id.Value)

        //                  && (!institute_id.HasValue ||
        //                      s2.institute == institute_id)

        //            select new
        //            {
        //                program_id = gd.prgm_id.Value,

        //                student_id = (int?)gd.student_id,
        //                student_name = s2.name,

        //                institute = (int?)s2.institute,
        //                institute_name = i.name,

        //                phone_no = s2.phone_no,
        //                admsn_no = s2.admsn_no,
        //                email = s2.email,

        //                chest_no = gp != null
        //                    ? gp.chess_no
        //                    : null,

        //                status = gp != null
        //                    ? gp.program_status
        //                    : null,

        //                group_name = gd.group_name,

        //                point = (double?)null,
        //                judgement_criteria_id = (int?)null,

        //                image = s2.image
        //            }
        //        )
        //        .OrderBy(x => x.student_name)
        //        .ToListAsync();


        //        // ============================================================
        //        // 5. GROUP STUDENTS BY PROGRAM
        //        // ============================================================

        //        var groupStudentsByProgram = groupStudentData
        //            .GroupBy(x => x.program_id)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g.Select(x => new
        //                {
        //                    student_id = x.student_id,
        //                    student_name = x.student_name,
        //                    institute = x.institute,
        //                    institute_name = x.institute_name,
        //                    phone_no = x.phone_no,
        //                    admsn_no = x.admsn_no,
        //                    email = x.email,
        //                    chest_no = x.chest_no,
        //                    status = x.status,
        //                    group_name = x.group_name,
        //                    point = x.point,
        //                    judgement_criteria_id = x.judgement_criteria_id,

        //                    imageurl = !string.IsNullOrEmpty(x.image)
        //                        ? $"{baseUrl}uploads/student/{x.image}"
        //                        : null
        //                }).ToList()
        //            );


        //        // ============================================================
        //        // 6. GET NORMAL STUDENTS + POINTS
        //        // ============================================================

        //        var normalStudentData = await (
        //            from sp in _context.tbl_prgm_participants.AsNoTracking()

        //            join st in _context.tbl_student.AsNoTracking()
        //                on sp.student_id equals st.id into std

        //            from student in std.DefaultIfEmpty()

        //            join i in _context.tbl_institute.AsNoTracking()
        //                on student.institute equals i.id into inst

        //            from ins in inst.DefaultIfEmpty()

        //            join pp0 in _context.tbl_prgm_point
        //                .AsNoTracking()
        //                .Where(x => x.judge_id == ref_id)

        //                on new
        //                {
        //                    sp.chess_no,
        //                    sp.student_id,
        //                    sp.prgm_id
        //                }

        //                equals new
        //                {
        //                    pp0.chess_no,
        //                    pp0.student_id,
        //                    pp0.prgm_id
        //                }

        //                into ppJoin

        //            from pp in ppJoin.DefaultIfEmpty()

        //            where sp.prgm_id.HasValue
        //                  && normalProgramIds.Contains(sp.prgm_id.Value)

        //                  && sp.status.ToLower() != "deleted"

        //                  && (!institute_id.HasValue ||
        //                      (student != null &&
        //                       student.institute == institute_id))

        //                  && (string.IsNullOrWhiteSpace(chest_no) ||
        //                      sp.chess_no == chest_no)

        //            select new
        //            {
        //                program_id = sp.prgm_id.Value,

        //                student_id = student != null
        //                    ? (int?)student.id
        //                    : null,

        //                student_name = student != null
        //                    ? student.name
        //                    : "",

        //                institute = student != null
        //                    ? (int?)student.institute
        //                    : null,

        //                institute_name = ins != null
        //                    ? ins.name
        //                    : "",

        //                phone_no = student != null
        //                    ? student.phone_no
        //                    : "",

        //                admsn_no = student != null
        //                    ? student.admsn_no
        //                    : "",

        //                email = student != null
        //                    ? student.email
        //                    : "",

        //                chest_no = sp.chess_no,
        //                status = sp.program_status,

        //                group_name = (string?)null,

        //                point = pp != null
        //                    ? pp.point
        //                    : null,

        //                judgement_criteria_id = pp != null
        //                    ? pp.judgement_criteria_id
        //                    : null,

        //                image = student != null
        //                    ? student.image
        //                    : null
        //            }
        //        )
        //        .OrderBy(x => x.student_name)
        //        .ToListAsync();


        //        // ============================================================
        //        // 7. NORMAL STUDENTS BY PROGRAM
        //        // ============================================================

        //        var normalStudentsByProgram = normalStudentData
        //            .GroupBy(x => x.program_id)
        //            .ToDictionary(
        //                g => g.Key,
        //                g => g.Select(x => new
        //                {
        //                    student_id = x.student_id,
        //                    student_name = x.student_name,
        //                    institute = x.institute,
        //                    institute_name = x.institute_name,
        //                    phone_no = x.phone_no,
        //                    admsn_no = x.admsn_no,
        //                    email = x.email,
        //                    chest_no = x.chest_no,
        //                    status = x.status,
        //                    group_name = x.group_name,
        //                    point = x.point,
        //                    judgement_criteria_id = x.judgement_criteria_id,

        //                    imageurl = !string.IsNullOrEmpty(x.image)
        //                        ? $"{baseUrl}uploads/student/{x.image}"
        //                        : null
        //                }).ToList()
        //            );


        //        // ============================================================
        //        // 8. NORMAL PARTICIPANT COUNTS
        //        // ============================================================

        //        var normalCounts = await _context.tbl_prgm_participants
        //            .AsNoTracking()
        //            .Where(x =>
        //                x.prgm_id.HasValue &&
        //                normalProgramIds.Contains(x.prgm_id.Value) &&
        //                x.status.ToLower() != "deleted")
        //            .GroupBy(x => x.prgm_id.Value)
        //            .Select(g => new
        //            {
        //                program_id = g.Key,
        //                count = g.Count()
        //            })
        //            .ToDictionaryAsync(
        //                x => x.program_id,
        //                x => x.count);


        //        // ============================================================
        //        // 9. GROUP PARTICIPANT COUNTS
        //        // ============================================================

        //        var groupCounts = await (
        //            from gm in _context.tbl_group_members.AsNoTracking()

        //            join pp in _context.tbl_prgm_participants.AsNoTracking()
        //                on gm.prgm_id equals pp.prgm_id

        //            where gm.prgm_id.HasValue
        //                  && groupProgramIds.Contains(gm.prgm_id.Value)
        //                  && pp.status.ToLower() != "deleted"

        //            group gm by gm.prgm_id.Value
        //            into g

        //            select new
        //            {
        //                program_id = g.Key,
        //                count = g.Select(x => x.id).Count()
        //            }
        //        )
        //        .ToDictionaryAsync(
        //            x => x.program_id,
        //            x => x.count);


        //        // ============================================================
        //        // 10. BUILD FINAL RESULT
        //        // ============================================================

        //        var result = programs.Select(p =>
        //        {
        //            bool isGroup =
        //                p.program_type != null &&
        //                p.program_type.ToLower() == "group";


        //            // Do not use new List<dynamic>() here.
        //            // Keep the value as object to avoid anonymous-type mismatch.

        //            object students;

        //            if (isGroup)
        //            {
        //                students = groupStudentsByProgram.TryGetValue(
        //                    p.id,
        //                    out var groupList)
        //                    ? groupList
        //                    : new object[0];
        //            }
        //            else
        //            {
        //                students = normalStudentsByProgram.TryGetValue(
        //                    p.id,
        //                    out var normalList)
        //                    ? normalList
        //                    : new object[0];
        //            }


        //            int participantCount;

        //            if (isGroup)
        //            {
        //                participantCount =
        //                    groupCounts.TryGetValue(
        //                        p.id,
        //                        out var groupCount)
        //                        ? groupCount
        //                        : 0;
        //            }
        //            else
        //            {
        //                participantCount =
        //                    normalCounts.TryGetValue(
        //                        p.id,
        //                        out var normalCount)
        //                        ? normalCount
        //                        : 0;
        //            }


        //            object judges =
        //                judgesByProgram.TryGetValue(
        //                    p.id,
        //                    out var judgeList)
        //                    ? judgeList
        //                    : new object[0];


        //            return new
        //            {
        //                p.id,
        //                p.program_type,
        //                p.participant_type,
        //                p.gender,
        //                p.stage_id,
        //                p.stage_name,
        //                p.program_name,
        //                p.color_code,
        //                p.date,
        //                p.time,
        //                p.added_by,
        //                p.addedtype,
        //                p.addedon,
        //                p.status,
        //                p.item_code,
        //                p.offstage_onstage,
        //                p.group_min_participants,
        //                p.group_max_participants,
        //                p.no_of_group,
        //                p.ac_year_id,

        //                participant_count = participantCount,

        //                judges,

        //                prgm_student_detail = students
        //            };
        //        }).ToList();


        //        // ============================================================
        //        // 11. FINAL RESPONSE
        //        // ============================================================

        //        if (!result.Any())
        //        {
        //            return Ok(new
        //            {
        //                status = false,
        //                message = "Data not found"
        //            });
        //        }


        //        return Ok(new
        //        {
        //            status = true,
        //            message = "Success",
        //            data = result
        //        });
        //    }


   



    }




}
