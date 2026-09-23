using ClosedXML.Excel;
using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Office2010.Excel;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing;

using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class dashboardController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public dashboardController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("dashboard")]
        public async Task<ActionResult> dashboard()
        {

            var enc_key = Request.Headers["XapiKey"].ToString();

            var user = await _context.tbl_user
                .FirstOrDefaultAsync(u => u.enc_key == enc_key);

            int ref_id = user?.reference_id ?? 0;

            try
            {
                // Total active programs
                var program_schedule = await _context.tbl_program
                    .Where(tbl => tbl.delete_status == "active")
                    .CountAsync();

                // Individual program IDs for this student
                var individualProgramIds = await _context.tbl_prgm_participants
                    .Where(p => p.student_id == ref_id && p.status == "active")
                    .Select(p => p.prgm_id)
                    .ToListAsync();

                // Group program IDs for this student
                var groupProgramIds = await _context.tbl_group_members
                    .Where(g => g.student_id == ref_id)
                    .Select(g => g.prgm_id)
                    .ToListAsync();

                // Combine all program IDs
                var allProgramIds = individualProgramIds
                    .Concat(groupProgramIds)
                    .Distinct()
                    .ToList();

                // Count active programs for this student
                var my_programs = await _context.tbl_program
                    .Where(p => allProgramIds.Contains(p.id) && p.delete_status == "active")
                    .CountAsync();

                // ✅ Get points from NON-group programs using EF-safe ToLower()
                decimal my_points = await (
      from pp in _context.tbl_prgm_participants
      join prog in _context.tbl_program on pp.prgm_id equals prog.id
      where pp.student_id == ref_id
            && pp.status == "active"
            && prog.delete_status == "active"
            && prog.program_type != null
            && prog.program_type.ToLower() != "group"
      select (decimal?)(pp.average_point ?? 0) // safely handle nulls
  ).SumAsync() ?? 0m; // if no rows, default to 0


                var baseUrl = $"{Request.Scheme}://{Request.Host}/";

                // Event details
                var eventdetails = from e in _context.tbl_event
                                   join v in _context.tbl_venue on e.venue_id equals v.id into ven
                                   from vn in ven.DefaultIfEmpty()
                                   where e.status == "active" && e.ac_year_id == 3
                                   select new
                                   {
                                       id = e.id,
                                       event_name = e.event_name,
                                       short_name = e.short_name,
                                       from_date = e.from_date,
                                       to_date = e.to_date,
                                       venue_id = e.venue_id,
                                       venue = vn.venue,
                                       location = vn.location,
                                       image = e.image,
                                       imageurl = !string.IsNullOrEmpty(e.image) ? $"{baseUrl}uploads/event/{e.image}" : null
                                   };

                return Ok(new
                {
                    status = true,
                    data = new
                    {
                        program_schedule,
                        my_programs,
                        my_points,
                        eventdetails
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

        //  app


        [HttpGet]
        [Route("my_programs")]

        public async Task<ActionResult> my_programs(string? status)
        {
           

            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = await _context.tbl_user.FirstOrDefaultAsync(u => u.enc_key == enc_key);
          //  return Ok(user);

            if (user == null || user.type != "student")
            {
                return Ok(new { status = false, message = "Invalid user" });
            }

            int ref_id = user.reference_id ?? 0;

            double total_points = await (
from p in _context.tbl_prgm_participants
join prog in _context.tbl_program on p.prgm_id equals prog.id into progJoin
from prog in progJoin.DefaultIfEmpty()
where p.student_id == ref_id && prog != null && prog.status.ToLower() == "result published" || prog.status== "Transferred to Media" && prog.program_type.ToLower() != "group"
select (double?)p.average_point
).SumAsync() ?? 0;

            var individualQuery = from p in _context.tbl_prgm_participants
                                  join pr in _context.tbl_program on p.prgm_id equals pr.id
                                  join s in _context.tbl_stage on pr.stage_id equals s.id into stageJoin
                                  from st in stageJoin.DefaultIfEmpty()
                                  where pr.delete_status == "active" && pr.program_type.ToLower() != "group"
                                        && p.student_id == ref_id
                                  select new
                                  {
                                      pr.id,
                                      pr.status,
                                      pr.program_name,
                                      pr.program_type,
                                      pr.gender,
                                      pr.participant_type,
                                      st.stage_name,
                                      pr.date,
                                      pr.time,
                                      pr.color_code,
                                      points = pr.status.ToLower() == "result published" || pr.status == "Transferred to Media" 
                                                  ? _context.tbl_prgm_participants
                                                      .Where(x => x.student_id == ref_id && x.prgm_id == pr.id)
                                                      .Select(x => (double?)x.average_point)
                                                      .FirstOrDefault() ?? 0
                                                  : 0,
                                      pr.item_code,

                                  };
          //  return Ok(individualQuery);
            // -------------------------------
            // Group programs (student is member)
            // -------------------------------
            var groupQuery = from gm in _context.tbl_group_members
                             join pr in _context.tbl_program on gm.prgm_id equals pr.id
                             join st in _context.tbl_stage on pr.stage_id equals st.id into stageJoin
                             from st in stageJoin.DefaultIfEmpty()
                             where pr.delete_status == "active" && pr.program_type.ToLower() == "group"
                                   && gm.student_id == ref_id
                             select new
                             {
                                 pr.id,
                                 pr.status,
                                 pr.program_name,
                                 pr.program_type,
                                 pr.gender,
                                 pr.participant_type,
                                 stage_name = st != null ? st.stage_name : "",
                                 pr.date,
                                 pr.time,
                                 pr.color_code,
                                 points = pr.status.ToLower() == "result published" || pr.status == "Transferred to Media"      
                                             ? _context.tbl_prgm_participants
                                                 .Where(x => x.prgm_id == pr.id && x.student_id == ref_id)
                                                 .Select(x => (double?)x.average_point)
                                                 .FirstOrDefault() ?? 0
                                             : 0,
                                 pr.item_code,

                             };

            var combinedQuery = individualQuery.Union(groupQuery);

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                status = status.ToLower();
                if (status == "pending")
                {
                    combinedQuery = combinedQuery.Where(x => x.status.ToLower() != "result published");
                }
                else if (status == "completed")
                {
                    combinedQuery = combinedQuery.Where(x => x.status.ToLower() == "result published");
                }
                else
                {
                    return Ok(new { status = false, message = "Invalid status value. Use 'pending' or 'completed'." });
                }
            }

            // Deduplicate
            var result = await combinedQuery
                .GroupBy(x => new { x.id, x.program_name, x.stage_name, x.date, x.time, x.color_code,x.status })
                .Select(g => g.First())
                .ToListAsync();

            if (result == null || !result.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var data = result.Select(x => new
            {
                x.id,
                x.program_name,
                x.stage_name,
                date = x.date?.ToString("dd/MM/yyyy"),
                time = x.time,
                x.color_code,
                x.points,
                x.participant_type,
                x.gender,
                program_type = x.program_type,
                x.status,
                x.item_code
            });

            return Ok(new
            {
                status = true,
                message = "Success",
                program_count = data.Count(),
                points = total_points,      
                data
            });
        }


        //my points

        [HttpGet]
        [Route("my_points")]
        public async Task<ActionResult> my_points(int?ac_year_id)
        {
           var enc_key = Request.Headers["XapiKey"].ToString();

            if (_context.tbl_prgm_point == null || _context.tbl_program == null || _context.tbl_stage == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var user = _context.tbl_user
                                .FirstOrDefault(u => u.enc_key == enc_key);

            if (user == null)
            {
                return Ok(new { status = false, message = "User not found" });
            }

            int ref_id = (int)user.reference_id;

            //            double total_points = await (
            //  from p in _context.tbl_prgm_participants
            //  join prog in _context.tbl_program on p.prgm_id equals prog.id into progJoin
            //  from prog in progJoin.DefaultIfEmpty()
            //  where p.student_id == ref_id && prog != null && prog.status.ToLower() == "result published" || prog.status == "Transferred to Media" && prog.program_type.ToLower() != "group"   
            //  select (double?)p.average_point
            //).SumAsync() ?? 0;

            double total_points = await (
    from p in _context.tbl_prgm_participants
    join prog in _context.tbl_program
        on p.prgm_id equals prog.id
    where p.student_id == ref_id
          && prog != null
          && (
              prog.status.ToLower() == "result published"
              || prog.status.ToLower() == "transferred to media"
          )
          && prog.program_type.ToLower() != "group"
    select (double?)p.average_point
).SumAsync() ?? 0;

            var query =
                from p in _context.tbl_prgm_point
                join pr in _context.tbl_program
                    on p.prgm_id equals pr.id
                join s in _context.tbl_stage
                    on pr.stage_id equals s.id
                join part in _context.tbl_prgm_participants
                    on new
                    {
                        p.prgm_id,
                        p.student_id
                    }
                    equals new
                    {
                        prgm_id = part.prgm_id,
                        student_id = part.student_id
                    }
                    into partJoin

                from part in partJoin.DefaultIfEmpty()

                where pr.delete_status == "active"
                      && (
                            pr.status.ToLower() == "result published"
                            || pr.status == "Transferred to Media"
                         )
                      && p.student_id == ref_id
                      && pr.ac_year_id == ac_year_id

                group new { p, pr, s, part } by new
                {
                    p.prgm_id,
                    pr.program_name,
                    pr.color_code,
                    s.stage_name,
                    pr.date,
                    pr.time,
                    total_points = part != null ? part.average_point : 0,
                    pr.program_type,
                    pr.gender,
                    pr.item_code,
                    pr.participant_type
                }
                into g

                select new
                {
                    program_id = g.Key.prgm_id,
                    program_name = g.Key.program_name,
                    color_code = g.Key.color_code,
                    stage_name = g.Key.stage_name,
                    date = g.Key.date,
                    time = g.Key.time,
                    points = g.Key.total_points,
                    program_type = g.Key.program_type,
                    gender = g.Key.gender,
                    item_code = g.Key.item_code,
                    participant_type = g.Key.participant_type
                };


            var result = await query.ToListAsync();

            if (!result.Any())
            {
                return Ok(new { status = false, message = "No points found" });
            }

            var data = result.Select(x => new
            {
                program_id = x.program_id,
                program_name = x.program_name,
                color_code = x.color_code,
                stage_name = x.stage_name,
                date = x.date?.ToString("dd/MM/yyyy"),
                time = x.time,
                points = x.points,
                program_type=x.program_type,
                gender=x.gender,
                item_code=x.item_code,
                participant_type=x.participant_type

            });

            return Ok(new
            {
                status = true,
                message = "Success",
                total_points,
                data = data
            });
        }



        //app-judge prgm list

        //[HttpGet]
        //[Route("list_judge_prgms")]
        //public async Task<ActionResult> list_judge_prgms(DateTime? date, string? status)
        //{
        //    var enc_key = Request.Headers["XapiKey"].ToString();
        //    var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower()=="judge");

        //   int ref_id = user?.reference_id ?? 0;

        //    if (_context.tbl_user == null)
        //    {
        //        return Ok(new { status = false, message = "Data not found" });
        //    }

        //    var baseQuery = from jp in _context.tbl_judge_prgm
        //                    join pr in _context.tbl_program on jp.program_id equals pr.id
        //                    where pr.delete_status == "active" && jp.status!= "deleted" && jp.judge_id==ref_id 

        //                    select new
        //                    {
        //                        jp.judge_id,
        //                        jp.program_id,
        //                        pr.program_name,
        //                        pr.date,
        //                        pr.time,
        //                        pr.color_code,
        //                        actualStatus = pr.status,
        //                        hasScored = _context.tbl_prgm_point
        //                                     .Any(s => s.judge_id == jp.judge_id && s.prgm_id == jp.program_id),
        //                                     pr.item_code,
        //                                     pr.gender,
        //                                     pr.participant_type,
        //                                     pr.program_type,
        //                    };

        //    // Apply status filter
        //    if (!string.IsNullOrWhiteSpace(status))
        //    {
        //        status = status.ToLower();

        //        if (status == "pending")
        //        {
        //            //baseQuery = baseQuery.Where(x => !x.hasScored);
        //            //   baseQuery = baseQuery.Where(x => !x.hasScored && (x.actualStatus == "pending" || x.actualStatus == "ongoing"));

        //            baseQuery = baseQuery.Where(x => x.actualStatus == "pending"  || x.actualStatus == "ongoing" || x.actualStatus== "Judgement Sheet Prepared" || x.actualStatus== "Transmitted to frontstage");

        //        }
        //        else if (status == "completed")
        //        {
        //            // completed means: hasScored and status is result published
        //            baseQuery = baseQuery.Where(x => x.actualStatus.ToLower() == "completed" || x.actualStatus.ToLower() == "tabulation team approved" || x.actualStatus.ToLower() == "result published" || 
        //            x.actualStatus.ToLower() == "Tabulation Manager Approved"|| x.actualStatus.ToLower()== "Transmitted to Announcement Team"|| x.actualStatus.ToLower()== "approved");


        //            //  baseQuery = baseQuery.Where(x => x.hasScored);
        //        }
        //        else
        //        {
        //            baseQuery = baseQuery.Where(x => x.actualStatus.ToLower() == status);
        //        }
        //    }

        //    // Apply date filter
        //    //if (date.HasValue)
        //    //{
        //    //    baseQuery = baseQuery.Where(x => x.date == date.Value);
        //    //}

        //    if (date.HasValue)
        //    {
        //        var targetDate = date.Value.Date;
        //        baseQuery = baseQuery.Where(x => x.date.HasValue && x.date.Value.Date == targetDate);
        //    }


        //    var result = await baseQuery
        //        .GroupBy(x => new { x.judge_id, x.program_id })
        //        .Select(g => g.First())
        //        .ToListAsync();

        //    if (result == null || !result.Any())
        //    {
        //        return Ok(new { status = false, message = "Data not found" });
        //    }

        //    var finalResult = result.Select(x => new
        //    {
        //        x.judge_id,
        //        x.program_id,
        //        x.program_name,
        //        x.date,
        //        x.time,
        //        x.color_code,
        //        status = x.hasScored ? (x.actualStatus ?? "completed") : "pending",
        //        program_status=x.actualStatus,
        //        x.item_code,
        //        x.gender,
        //        x.participant_type,
        //        x.program_type,
        //    });

        //    return Ok(new { status = true, message = "Success", data = finalResult });
        //}

        [HttpGet]
        [Route("list_judge_prgms")]
        public async Task<ActionResult> list_judge_prgms(DateTime? date, string? status)
        {
            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower() == "judge");

            int ref_id = user?.reference_id ?? 0;

            if (_context.tbl_user == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }
          
            var baseQuery = from jp in _context.tbl_judge_prgm
                            join pr in _context.tbl_program on jp.program_id equals pr.id
                            where pr.delete_status == "active" && jp.status != "deleted" && jp.judge_id == ref_id&& pr.ac_year_id==3
                            select new
                            {
                                jp.judge_id,
                                jp.program_id,
                                pr.program_name,
                                pr.date,
                                pr.time,
                                pr.color_code,
                                actualStatus = pr.status,
                                hasScored = _context.tbl_prgm_point
                                             .Any(s => s.judge_id == jp.judge_id && s.prgm_id == jp.program_id),

                                //anyJudgeMarked = _context.tbl_prgm_participants
                                //.Where(pp => pp.prgm_id == jp.program_id && pp.chess_no != null)
                                //.All(pp => _context.tbl_prgm_point
                                //    .Any(p => p.judge_id == ref_id
                                //              && p.prgm_id == jp.program_id
                                //              && p.student_id == pp.student_id)),

                                // ✅ Fixed anyJudgeMarked logic
                                anyJudgeMarked = _context.tbl_prgm_participants
    .Where(pp => pp.prgm_id == jp.program_id && pp.chess_no != null && pp.ispresent==null)
    .Select(pp => pp.student_id)
    .AsEnumerable()  // materialize in memory to safely use .All()
    .DefaultIfEmpty() // ensures empty collections are handled
    .All(studentId =>
        studentId != 0 && // skip placeholder from DefaultIfEmpty
        _context.tbl_prgm_point.Any(p =>
            p.judge_id == ref_id &&
            p.prgm_id == jp.program_id &&
            p.student_id == studentId
        )
    ),


            pr.item_code,
                                pr.gender,
                                pr.participant_type,
                                pr.program_type,
                            };

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                status = status.ToLower();

                if (status == "pending")
                {
                    baseQuery = baseQuery.Where(x => x.actualStatus == "pending"
                                                   || x.actualStatus == "ongoing"
                                                   || x.actualStatus == "Judgement Sheet Prepared"
                                                   || x.actualStatus == "Transmitted to frontstage"
                                                   || x.actualStatus.ToLower() == "completed");
                }
                else if (status == "completed")
                {
                    baseQuery = baseQuery.Where(x =>
                    //x.actualStatus.ToLower() == "completed"||
                                                   x.actualStatus.ToLower() == "tabulation team approved"
                                                   || x.actualStatus.ToLower() == "result published"
                                                   || x.actualStatus.ToLower() == "tabulation manager approved"

                                                   || x.actualStatus.ToLower() == "transferred to media"
                                                   || x.actualStatus.ToLower() == "transmitted to announcement team"
                                                   || x.actualStatus.ToLower() == "judges approved");
                }
                else
                {
                    baseQuery = baseQuery.Where(x => x.actualStatus.ToLower() == status);
                }
            }

            // Apply date filter
            if (date.HasValue)
            {
                var targetDate = date.Value.Date;
                baseQuery = baseQuery.Where(x => x.date.HasValue && x.date.Value.Date == targetDate);
            }

            var result = await baseQuery
                .GroupBy(x => new { x.judge_id, x.program_id })
                .Select(g => g.First())
                .ToListAsync();

            if (result == null || !result.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var finalResult = result.Select(x => new
            {
                x.judge_id,
                x.program_id,
                x.program_name,
                x.date,
                x.time,
                x.color_code,
                status = x.hasScored ? (x.actualStatus ?? "completed") : "pending",
                program_status = x.actualStatus,
                x.item_code,
                x.gender,
                x.participant_type,
                x.program_type,
                judge_marked = x.anyJudgeMarked ? "Marked" : "Not marked",

            });

            return Ok(new { status = true, message = "Success", data = finalResult });
        }

        //chess no: list

        [HttpGet]
        [Route("chess_no_list")]
        public async Task<ActionResult> chess_no_list(int? program_id,int?ac_year_id)
        {
            var enc_key = Request.Headers["XapiKey"].ToString();
            var user = _context.tbl_user.FirstOrDefault(u => u.enc_key == enc_key && u.type.ToLower()=="judge");

            int ref_id = 0;
            if (user != null)
            {
                ref_id = (int)user.reference_id;
            }

            if (_context.tbl_program == null)
            {
                return Ok(new { status = false, message = "Program data not found" });
            }

            if (!program_id.HasValue)
            {
                return Ok(new { status = false, message = "Program ID is required" });
            }

            var result = await (
     from p in _context.tbl_prgm_participants
     join j in _context.tbl_judge_prgm
         on p.prgm_id equals j.program_id

     join pp in _context.tbl_prgm_point.Where(x => x.judge_id == ref_id)
         on new
         {
             p.chess_no,
             p.student_id,
             p.prgm_id
         }
         equals new
         {
             pp.chess_no,
             pp.student_id,
             pp.prgm_id
         }
         into ppJoin

     from pp in ppJoin.DefaultIfEmpty()

     where p.prgm_id == program_id
           && j.judge_id == ref_id
           && p.status.ToLower() != "deleted"
           && p.chess_no != null

     select new
     {
         p.prgm_id,
         p.chess_no,
         p.student_id,
         p.program_status,
         point = pp != null ? pp.point : null,
         p.ac_year_id,
         j.judge_id,
         p.remark,
         pp.comments,

         message = p.ispresent == "absent"
             ? "absent"
             : pp != null
                 ? "completed"
                 : "pending"
     }
 )
 .Distinct()
 .ToListAsync();

            if (ac_year_id.HasValue)
            {
                result = result.Where(x => x.ac_year_id == ac_year_id.Value).ToList();
            }
            // Optional: deduplicate by chess_no and student_id (if needed)
            result = result
                .GroupBy(x => new { x.chess_no, x.student_id })
                .Select(g => g.First())
                .ToList();

            if (!result.Any())
            {
                return Ok(new { status = false, message = "No chest numbers found for this program" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }
      

        //judge_pgm_date
        [HttpGet]
        [Route("judge_event_date")]
        public async Task<ActionResult> judge_event_date(int?ac_year_id)
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

            if (_context.tbl_program == null)
            {
                return Ok(new { status = false, message = "Program data not found" });
            }


            var query = from p in _context.tbl_program
                        join e in _context.tbl_event on p.event_id equals e.id into evt
                        from e in evt.DefaultIfEmpty()
                        join j in _context.tbl_judge_prgm on p.id equals j.program_id into jdg
                        from j in jdg.DefaultIfEmpty()
                        where  j.judge_id == ref_id &&p.ac_year_id==ac_year_id
                        select p.date;

            var result = await query.Distinct().ToListAsync();



          //  var result = await query.ToListAsync();

          

            if (result == null || !result.Any())
            {
                return Ok(new { status = false, message = "No program dates found for this judge" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }

        //


        // dashboard web
        [HttpGet]
        [Route("dashboard_web")]
        public async Task<ActionResult> dashboard_web(int? ac_year_id)
        {
            try
            {
                // institute count
                var institute = await _context.tbl_institute
                    .Where(tbl => tbl.status == "active")
                    .CountAsync();

                // student count
                var students = await _context.tbl_student
                    .Where(tbl => tbl.status == "active" && tbl.ac_year_id == ac_year_id)
                    .CountAsync();

                // program count
                var programs = await _context.tbl_program
                    .Where(tbl => tbl.delete_status == "active" && tbl.ac_year_id == ac_year_id)
                    .CountAsync();

                // judge count
                var judges = await _context.tbl_judge
                    .Where(tbl => tbl.status == "active")
                    .CountAsync();

                var baseUrl = $"{Request.Scheme}://{Request.Host}/";

                // event details
                var eventdetails = await (
                    from e in _context.tbl_event

                    join v in _context.tbl_venue
                    on e.venue_id equals v.id into ven

                    from vn in ven.DefaultIfEmpty()

                    where e.status == "active"
                          && e.ac_year_id == ac_year_id

                    select new
                    {
                        id = e.id,
                        event_name = e.event_name,
                        short_name = e.short_name,
                        from_date = e.from_date,
                        to_date = e.to_date,
                        venue_id = e.venue_id,
                        venue = vn.venue,
                        location = vn.location,
                        image = e.image,
                        imageurl = !string.IsNullOrEmpty(e.image)
                            ? $"{baseUrl}uploads/event/{e.image}"
                            : null,

                        image1 = e.image1,
                        image1url = !string.IsNullOrEmpty(e.image1)
                            ? $"{baseUrl}uploads/event/{e.image1}"
                            : null
                    }).ToListAsync();

                return Ok(new
                {
                    status = true,
                    data = new
                    {
                        selected_year = ac_year_id,
                        institute,
                        students,
                        programs,
                        judges,
                        eventdetails
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "An error occurred while fetching dashboard data.",
                    error = ex.Message
                });
            }
        }

        //dashboard_program_detail

        [HttpGet]
        [Route("dashboard_result_published")]
        public async Task<ActionResult> dashboard_result_published(int? id, DateTime? date, int? institute_id)
        {
            if (_context.tbl_program == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var query = from p in _context.tbl_program
                        join e in _context.tbl_event on p.event_id equals e.id into evv
                        from ev in evv.DefaultIfEmpty()

                        where p.delete_status.ToLower() == "active" && p.status.ToLower()== "result published" || p.status == "Transferred to Media"    
                        select new
                        {
                            program_id = p.id,
                            p.program_name,
                           p.event_id,
                            p.date,
                            p.time,
                            p.status,
                         
                           event_name = ev.event_name,
                          


                            // Student list for this program
                            students = (from sp in _context.tbl_prgm_participants
                                        join s in _context.tbl_student on sp.student_id equals s.id
                                        where sp.prgm_id == p.id && (!institute_id.HasValue || s.institute == institute_id)
                                        select new
                                        {
                                            student_id = s.id,
                                            s.name
                                        }).ToList()
                        };

            if (id.HasValue)
            {
                query = query.Where(g => g.program_id == id.Value);
            }

            if (date.HasValue)
            {
                query = query.Where(g => g.date == date);
            }
            // Uncomment if status filtering is needed
            // if (!string.IsNullOrWhiteSpace(status))
            // {
            //     query = query.Where(g => g.status == status);
            // }

            var result = await query.ToListAsync();

            if (result == null || !result.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }


        //dashboard admin
        [HttpGet]
        [Route("dashboard_institute")]
        public async Task<ActionResult> dashboard_institute(int? institute_id,int?ac_year_id)
        {
            try
            {

                var students = await _context.tbl_student
                    .Where(tbl => tbl.status == "active" && tbl.institute==institute_id&&tbl.ac_year_id==ac_year_id)
                    .CountAsync();

                var programs = await _context.tbl_program
                    .Where(tbl => tbl.delete_status == "active"&&tbl.ac_year_id==ac_year_id)
                    .CountAsync();

                var completed_programs = await _context.tbl_program
                  .Where(tbl => tbl.delete_status == "active" && tbl.status.ToLower()=="completed" && tbl.ac_year_id == ac_year_id)
                  .CountAsync();

                var pending_programs = await _context.tbl_program
                 .Where(tbl => tbl.delete_status == "active" && tbl.status.ToLower() == "pending" && tbl.ac_year_id == ac_year_id)
                 .CountAsync();
                var baseUrl = $"{Request.Scheme}://{Request.Host}/";



                var eventdetails = from e in _context.tbl_event

                                   join v in _context.tbl_venue on e.venue_id equals v.id into ven
                                   from vn in ven.DefaultIfEmpty()

                                   where e.status == "active" && e.ac_year_id == ac_year_id
                                   select new
                                   {
                                       id = e.id,
                                       event_name = e.event_name,
                                       short_name = e.short_name,
                                       from_date = e.from_date,
                                       to_date = e.to_date,
                                       venue_id = e.venue_id,
                                       venue = vn.venue,
                                       location = vn.location,
                                       image = e.image,
                                       imageurl = !string.IsNullOrEmpty(e.image) ? $"{baseUrl}uploads/event/{e.image}" : null,
                                       image1 = e.image1,
                                       image1url = !string.IsNullOrEmpty(e.image1) ? $"{baseUrl}uploads/event/{e.image1}" : null

                                   };

                return Ok(new
                {
                    status = true,
                    data = new
                    {
                        students,
                        programs,
                        completed_programs,
                        pending_programs,
                        eventdetails
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

        //student_prgm_details
        [HttpGet]
        [Route("student_prgm_details")]
        public async Task<ActionResult> student_prgm_details(int? institute_id)
        {
            if (_context.tbl_student == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var query = from p in _context.tbl_student

                       
                        join c in _context.tbl_class on p.class_id equals c.id into cla
                        from cl in cla.DefaultIfEmpty()
                        join d in _context.tbl_division on p.division_id equals d.id into div
                        from di in div.DefaultIfEmpty()
                        where p.status == "active" && p.institute==institute_id
                        select new
                        {
                            p.id,
                            p.name,
                            p.phone_no,
                            p.admsn_no,
                            p.class_id,
                            cl.@class,
                            p.division_id,
                            di.division,
                          
                            studentprograms = (from c in _context.tbl_prgm_participants
                                               join prg in _context.tbl_program on c.prgm_id equals prg.id into prog
                                               from pr in prog.DefaultIfEmpty()

                                               where p.id == c.student_id
                                               select new
                                               {
                                                   c.id,
                                                   c.prgm_id,
                                                   program_name = pr.program_name,
                                                   pr.color_code,

                                               }).ToList(),
                            // total_points = _context.tbl_prgm_point
                            //.Where(pp => pp.student_id == p.id)
                            //.Sum(pp => (int?)pp.point) ?? 0
                            //total_points = (from pp in _context.tbl_prgm_point
                            //                join pr in _context.tbl_program on pp.prgm_id equals pr.id into prgm
                            //                from pr in prgm.DefaultIfEmpty()
                            //                where pp.student_id == p.id && (pr == null || pr.status.ToLower() == "result published")
                            //                select (int?)pp.point).Sum() ?? 0

        };

            //if (id.HasValue)
            //{
            //    query = query.Where(g => g.id == id.Value);
            //}

            var result = await query.ToListAsync();

            if (result == null || !result.Any())
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            return Ok(new { status = true, message = "Success", data = result });
        }


        [HttpGet]
        [Route("institute_points")]
        public async Task<ActionResult> institute_points(int? ac_year_id)
        {
            try
            {
               
                // Fetch and calculate institute points
                var institutePoints = (from p in _context.tbl_prgm_participants
                                       join s in _context.tbl_student on p.student_id equals s.id into stds
                                       from st in stds.DefaultIfEmpty()
                                       join i in _context.tbl_institute on st.institute equals i.id into insts
                                       from inst in insts.DefaultIfEmpty()
                                       join r in _context.tbl_program on p.prgm_id equals r.id into prgm
                                       from prg in prgm.DefaultIfEmpty()
                                       where (p.position == "1" || p.position == "2" || p.position == "3")
                                             && p.ac_year_id == ac_year_id
                                       group p by new { st.institute, inst.name,inst.color_code } into grp
                                       orderby grp.Sum(x => x.total_point) descending
                                       select new
                                       {
                                           institute_id = grp.Key.institute,
                                           institute_name = grp.Key.name,
                                           color_code=grp.Key.color_code,
                                          // total_points = grp.Sum(x => x.average_point)
                     total_points = Math.Round((decimal)grp.Sum(x => x.total_point), 2, MidpointRounding.AwayFromZero)

                                       }).ToList();

                // Assign color codes dynamically
                var resultWithColors = institutePoints.Select((x, index) => new
                {
                    x.institute_id,
                    x.institute_name,
                    x.total_points,
                    x.color_code, 
                }).ToList();

                // Return response
                return Ok(new
                {
                    status = true,
                    data = resultWithColors
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = "An error occurred while fetching institute points.",
                    error = ex.Message
                });
            }
        }

        //guest login
        [HttpGet]
        [Route("guest_result")]
        public async Task<ActionResult> guest_result(DateTime? date, int? stage_id, string? gender, string? category, string? type)
        {
            var data = await (
                from pp in _context.tbl_prgm_participants
                join p in _context.tbl_program on pp.prgm_id equals p.id into ppJoin
                from pr in ppJoin.DefaultIfEmpty()
                join s in _context.tbl_stage on pr.stage_id equals s.id into sJoin
                from s in sJoin.DefaultIfEmpty()
                join st in _context.tbl_student on pp.student_id equals st.id into stJoin
                from st in stJoin.DefaultIfEmpty()
                join i in _context.tbl_institute on st.institute equals i.id into iJoin
                from i in iJoin.DefaultIfEmpty()
                where (pr.status.ToLower() == "result published" || pr.status == "Transferred to Media" )
                && pr.ac_year_id==3 &&(pp.position == "1" || pp.position == "2" || pp.position == "3")  
                group new { pr, pp, s, st, i } by new
                {
                    pr.stage_id,
                    s.stage_name,
                    pr.program_name,
                    pr.program_type,
                    pr.color_code,
                    pr.item_code
                } into grp

                select new
                {
                    stage_id = grp.Key.stage_id,    
                    stage_name = grp.Key.stage_name,
                    program_name = grp.Key.program_name,
                    program_type = grp.Key.program_type,
                    color_code = grp.Key.color_code,
                    participant_type = grp.FirstOrDefault().pr.participant_type,
                    gender = grp.FirstOrDefault().pr.gender,
                    item_code = grp.Key.item_code,  
                    status = grp.FirstOrDefault().pr.status,
                    date = grp.FirstOrDefault().pr.date,
                    participants = grp
    .OrderBy(x => x.pp.position)
    .Select(x => new
    {
        student_id = x.pp.student_id,
        institute = x.i.name,
        student = x.st.name,
        position = x.pp.position
    })
    .ToList()
                }).ToListAsync();

            if (date.HasValue)
            {
                data = data.Where(g => g.date.HasValue && g.date.Value.Date == date.Value.Date).ToList();
            }

            if (stage_id.HasValue)
            {
                data = data.Where(g => g.stage_id == stage_id.Value).ToList();
            }

            if (!string.IsNullOrEmpty(gender))
            {
                data = data.Where(g => g.gender == gender).ToList();
            }

            if (!string.IsNullOrEmpty(category))
            {
                data = data.Where(g => g.participant_type == category).ToList();
            }

            if (!string.IsNullOrEmpty(type))
            {
                data = data.Where(g => g.program_type == type).ToList();
            }

            if (!data.Any())
            {
                return Ok(new { status = false, message = "No data found" });
            }
            return Ok(new
            {
                status = true,
                message = "Success",
                data = data
            });
        }

        //kalathilak kalaprathiba
        [HttpGet]
        [Route("top_performers")]

        public async Task<ActionResult> top_performers(string? participant_type, string? kalaprathibha_kalathilakam)
        {
            var candidates = await (
                from p in _context.tbl_prgm_participants
                join s in _context.tbl_student on p.student_id equals s.id
                join prg in _context.tbl_program on p.prgm_id equals prg.id
                join inst in _context.tbl_institute on s.institute equals inst.id
                where p.status != "deleted"
                      && prg.program_type.ToLower() != "group"
                      && (participant_type == null || prg.participant_type.ToLower() == participant_type.ToLower())
                group new { p, s, prg, inst } by new
                {
                    student_id = s.id,
                    student_name = s.name,
                    s.gender,
                    s.institute,
                    school_name = inst.name,
                    prg.participant_type
                } into g
                select new
                {
                    student_id = g.Key.student_id,
                    name = g.Key.student_name,
                    gender = g.Key.gender,
                    institute_id = g.Key.institute,
                    school = g.Key.school_name,
                    participant_type = g.Key.participant_type,
                    points = g.Sum(x => x.p.average_point ?? 0)
                }
            ).ToListAsync();

            if (candidates == null || !candidates.Any())
                return Ok(new { status = false, message = "No participants found." });

            var result = candidates
                .GroupBy(x => x.participant_type?.ToLower())
                .Select(g =>
                {
                    var boys = g.Where(x => x.gender?.ToLower() == "boys").OrderByDescending(x => x.points).FirstOrDefault();
                    var girls = g.Where(x => x.gender?.ToLower() == "girls").OrderByDescending(x => x.points).FirstOrDefault();

                    return new
                    {
                        participant_type = g.Key,
                        kalaprathibha = boys,
                        kalathilakam = girls
                    };
                })
                .ToList();

            // Filter response dynamically
            var filteredResult = result.Select(r =>
            {
                var obj = new Dictionary<string, object?>
                {
                    ["participant_type"] = r.participant_type
                };

                if (string.IsNullOrEmpty(kalaprathibha_kalathilakam) || kalaprathibha_kalathilakam.ToLower() == "kalaprathibha")
                {
                    if (r.kalaprathibha != null)
                    {
                        obj["kalaprathibha"] = new
                        {
                            title = "Kalaprathibha",
                            name = r.kalaprathibha.name,
                            school = r.kalaprathibha.school,
                            points = Math.Round(r.kalaprathibha.points, 2)
                        };
                    }
                }

                if (string.IsNullOrEmpty(kalaprathibha_kalathilakam) || kalaprathibha_kalathilakam.ToLower() == "kalathilakam")
                {
                    if (r.kalathilakam != null)
                    {
                        obj["kalathilakam"] = new
                        {
                            title = "Kalathilakam",
                            name = r.kalathilakam.name,
                            school = r.kalathilakam.school,
                            points = Math.Round(r.kalathilakam.points, 2)
                        };
                    }
                }

                return obj;
            });

            return Ok(new
            {
                status = true,
                data = filteredResult
            });
        }



        [HttpGet]
        [Route("kalathilakam_kalaprathiba")]
      
        public async Task<ActionResult> kalathilakam_kalaprathiba(string? participant_type, string? kalathilakam_kalaprathiba)
        {
            if (_context.tbl_prgm_participants == null || _context.tbl_student == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }


            // Step 1: Calculate points per student
            var studentsWithPoints = (from p in _context.tbl_prgm_participants
                                      join s in _context.tbl_student on p.student_id equals s.id into studentGroup
                                      from s in studentGroup.DefaultIfEmpty()
                                      join i in _context.tbl_institute on s.institute equals i.id into inst
                                      from ins in inst.DefaultIfEmpty()
                                      join pr in _context.tbl_program on p.prgm_id equals pr.id into programGroup
                                      from pr in programGroup.DefaultIfEmpty()
                                      group new { p, s, pr } by new
                                      {
                                          s.id,
                                          s.name,
                                          s.admsn_no,
                                          s.institute,
                                          institute_name = ins.name,
                                          s.gender,
                                          s.kalathilakam_kalaprathiba,
                                          s.class_id,
                                          pr.participant_type
                                      } into g
                                      select new
                                      {
                                          student_id = g.Key.id,
                                          name = g.Key.name,
                                          admsn_no = g.Key.admsn_no,
                                          institute = g.Key.institute,
                                          institute_name = g.Key.institute_name,
                                          gender = g.Key.gender,
                                          participant_type=g.Key.participant_type,      
                                          kalathilakam_kalaprathiba = g.Key.kalathilakam_kalaprathiba ?? "",
                                          class_id = g.Key.class_id,
                                          total_points = g.Where(x => x.pr.program_type.ToLower() != "group")
                                                          .Sum(x => x.p.average_point)
                                      }).ToList();

            // Step 2: Assign participant_type using tbl_participants_type (CSV matching)
            var studentsWithType = studentsWithPoints.Select(s => new
            {
                s.student_id,
                s.name,
                s.admsn_no,
                s.institute,
                s.institute_name,
                s.gender,
                s.kalathilakam_kalaprathiba,
                points = s.total_points,
                class_id = s.class_id,
                participant_type =s.participant_type
            }).ToList();

            // Step 3: Get all programs for students
            var studentIds = studentsWithType.Select(x => x.student_id).ToList();

            //var studentPrograms = (from pp in _context.tbl_prgm_participants
            //                       join pr in _context.tbl_program on pp.prgm_id equals pr.id into programGroup
            //                       from pr in programGroup.DefaultIfEmpty()
            //                       where pp.student_id.HasValue
            //                             && studentIds.Contains(pp.student_id.Value)
            //                             && pr.status.ToLower() == "result published"
            //                       select new
            //                       {
            //                           student_id = pp.student_id.Value,
            //                           program_name = pr.program_name,
            //                           participant_type = pr.participant_type
            //                       }).ToList();

            // Step 4: Apply participant_type filter if passed
            if (!string.IsNullOrEmpty(participant_type))
            {
                studentsWithType = studentsWithType
                    .Where(s => s.participant_type != null &&
                                s.participant_type.Equals(participant_type, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Step 5: Sort by points (descending)
            studentsWithType = studentsWithType.OrderByDescending(s => s.points).ToList();

            // Step 6: Build final result including programs list
            var finalResult = studentsWithType.Select(s => new
            {
                s.student_id,
                s.name,
                s.admsn_no,
                s.institute,
                s.institute_name,
                s.gender,
                s.kalathilakam_kalaprathiba,
                points = s.points,
                //programs = studentPrograms
                //    .Where(p => p.student_id == s.student_id)
                //    .Select(p => new
                //    {
                //        program_name = p.program_name,
                //        participant_type = p.participant_type
                //    })
                //    .ToList()
            }).ToList();

            // Step 7: Filter based on kalathilakam_kalaprathiba (if provided)
            List<object> data;
            if (!string.IsNullOrEmpty(kalathilakam_kalaprathiba))
            {
                if (kalathilakam_kalaprathiba.ToLower() == "kalathilakam")
                {
                    data = finalResult.Where(s => s.gender?.ToLower() == "girl").ToList<object>();
                }
                else if (kalathilakam_kalaprathiba.ToLower() == "kalaprathibha")
                {
                    data = finalResult.Where(s => s.gender?.ToLower() == "boy").ToList<object>();
                }
                else
                {
                    data = new List<object>();
                }

                return Ok(new { status = true, message = "Success", data });
            }

            // Step 8: Return all if no gender-based filter
            data = finalResult.ToList<object>();

            return Ok(new { status = true, message = "Success", data });
        }


        [HttpPut]
        [Route("set_kalathilakam_kalaprathiba")]
     
        public async Task<ActionResult> set_kalathilakam_kalaprathiba([FromForm] int? student_id, [FromForm] string? kalathilakam_kalaprathiba)
        {
            if (student_id == null)
                return Ok(new { status = false, message = "student_id is required" });

          
            // Get the program
            var program = await _context.tbl_student
                .FirstOrDefaultAsync(p => p.id == student_id && p.status.ToLower() == "active");

            program.kalathilakam_kalaprathiba = kalathilakam_kalaprathiba;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Status updated successfully"
            });
        }

        //remove kalathilakam_kalaprathiba
        [HttpPut]
        [Route("remove_kalathilakam_kalaprathiba")]

        public async Task<ActionResult> remove_kalathilakam_kalaprathiba([FromForm] int? student_id, [FromForm] string? kalathilakam_kalaprathiba)
        {
            if (student_id == null)
                return Ok(new { status = false, message = "student_id is required" });


            // Get the program
            var program = await _context.tbl_student
                .FirstOrDefaultAsync(p => p.id == student_id && p.status.ToLower() == "active");

            program.kalathilakam_kalaprathiba = null;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                message = "Status updated successfully"
            });
        }

        ////
        [HttpGet]
        [Route("student_point_details")]
        public async Task<ActionResult> student_point_details(int? student_id)
        {
            if (student_id == null)
            {
                return Ok(new { status = false, message = "student_id is required" });
            }

            var student = await _context.tbl_student.FirstOrDefaultAsync(s => s.id == student_id);
            if (student == null)
            {
                return Ok(new { status = false, message = "Student not found" });
            }

            var programs = await (
                from pp in _context.tbl_prgm_participants
                join p in _context.tbl_program on pp.prgm_id equals p.id into programJoin
                from p in programJoin.DefaultIfEmpty()
                where pp.student_id == student_id && pp.status != "deleted" &&
                      p.status.ToLower() == "result published" || p.status== "Transferred to Media"
                select new
                {
                    program_name = p.program_name,
                    type = p.program_type,
                    position = pp.position,
                    points = pp.total_point
                }).ToListAsync();

            if (programs == null || !programs.Any())
            {
                return Ok(new { status = false, message = "No programs found for student" });
            }

            var formattedPrograms = programs
            .Select(p => new
             {
        // slno = index + 1,
                    program_name = p.program_name,
                    type = p.type,
                    position = p.position,
                    points = p.points,
                }).ToList();

            return Ok(new
            {
                status = true,
                message = "Success",
              
                    student_name = student.name,
                    programs = formattedPrograms
                
            });
        }

        /// 
    

        [HttpGet]
        [Route("dashboard_kalathilakam_kalaprathiba")]
        //  public async Task<ActionResult> dashboard_kalathilakam_kalaprathibha(int? ac_year_id)
        //  {
        //      if (_context.tbl_prgm_participants == null)
        //      {
        //          return Ok(new { status = false, message = "Data not found" });
        //      }
        //      var baseUrl = $"{Request.Scheme}://{Request.Host}/";
        //      // Step 0: Filter programs where all participants have a chess_no
        //      var programsWithAllChessNumbers = _context.tbl_prgm_participants
        //.GroupBy(pp => pp.prgm_id)
        //.Where(g =>
        //    g.All(pp => !string.IsNullOrEmpty(pp.chess_no)) &&
        //    g.All(pp => !ac_year_id.HasValue || pp.ac_year_id == ac_year_id)
        //)
        //.Select(g => g.Key)
        //.ToList();

        //      //  return Ok(programsWithAllChessNumbers);
        //      // Step 1: From those, get only programs that are Published or Transferred to Media
        //      var eligiblePrograms = _context.tbl_program
        //.Where(p =>
        //    programsWithAllChessNumbers.Contains(p.id) &&
        //    (p.status == "Result Published" ||
        //     p.status == "Transferred to Media") &&
        //    (!ac_year_id.HasValue || p.ac_year_id == ac_year_id)
        //)
        //.Select(p => p.id)
        //.ToList();

        //      // Step 2: If no eligible programs, return empty result
        //      if (!eligiblePrograms.Any())
        //      {
        //          return Ok(new
        //          {
        //              status = true,
        //              message = "Success",
        //              data = new { kalathilakam = new List<object>(), kalaprathibha = new List<object>() }
        //          });
        //      }



        //      // Step 1: Load solo participation
        //      var soloParticipation = (from pp in _context.tbl_prgm_participants
        //                               join p in _context.tbl_program on pp.prgm_id equals p.id
        //                               where p.program_type == "solo" && eligiblePrograms.Contains(p.id)
        //                               group pp by pp.student_id into g
        //                               select new
        //                               {
        //                                   student_id = g.Key,
        //                                   solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
        //                                   solo_points = g.Sum(x => x.average_point ?? 0)
        //                               }).ToList();

        //      // Step 2: Load group participation counts
        //      var groupParticipation = (from gm in _context.tbl_group_members
        //                                join p in _context.tbl_program on gm.prgm_id equals p.id
        //                                where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                                group gm by gm.student_id into g
        //                                select new
        //                                {
        //                                    student_id = g.Key,
        //                                    group_count = g.Select(x => x.prgm_id).Distinct().Count()
        //                                }).ToList();

        //      // Step 3: Load group points per institute per program (materialize in memory)
        //      var groupPointsByInstitute = (from pp in _context.tbl_prgm_participants
        //                                    join p in _context.tbl_program on pp.prgm_id equals p.id
        //                                    join s in _context.tbl_student on pp.student_id equals s.id
        //                                    where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                                    group pp by new { s.institute, pp.prgm_id } into g
        //                                    select new
        //                                    {
        //                                        institute_id = g.Key.institute,
        //                                        prgm_id = g.Key.prgm_id,
        //                                        group_points = g.Sum(x => x.average_point ?? 0)
        //                                    }).ToList(); // materialize

        //      // Step 4: Assign group points to each student based on their institute (in memory)
        //      var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
        //                                join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
        //                                join gpi in groupPointsByInstitute on new { s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, gpi.prgm_id }
        //                                group gpi by gm.student_id into g
        //                                select new
        //                                {
        //                                    student_id = g.Key,
        //                                    group_points = g.Sum(x => x.group_points)
        //                                }).ToList();


        //      // Step 5: Base student data
        //      var baseStudents = (from s in _context.tbl_student
        //                          join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
        //                          join p in _context.tbl_program on pp.prgm_id equals p.id
        //                          join i in _context.tbl_institute on s.institute equals i.id into instJoin
        //                          from inst in instJoin.DefaultIfEmpty()
        //                          where eligiblePrograms.Contains(p.id)
        //                          select new
        //                          {
        //                              s.id,
        //                              s.name,
        //                              image = s.image,
        //                              imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
        //                              s.admsn_no,
        //                              institute_name = inst.name,
        //                              s.gender,
        //                              p.participant_type
        //                          }).ToList();

        //      // Step 6: Merge participation + points
        //      var qualifiedStudents = (from bs in baseStudents
        //                               join sp in soloParticipation on bs.id equals sp.student_id into spJoin
        //                               from sp in spJoin.DefaultIfEmpty()
        //                               join gp in studentGroupPoints on bs.id equals gp.student_id into gpJoin
        //                               from gp in gpJoin.DefaultIfEmpty()
        //                               join gm in groupParticipation on bs.id equals gm.student_id into gmJoin
        //                               from gm in gmJoin.DefaultIfEmpty()
        //                               select new
        //                               {
        //                                   student_id = bs.id,
        //                                   student_name = bs.name,
        //                                   image = bs.image,
        //                                   imageurl = bs.imageurl,
        //                                   q_id = bs.admsn_no,
        //                                   institute_name = bs.institute_name,
        //                                   gender = bs.gender,
        //                                   participant_type = bs.participant_type,
        //                                   total_points = (sp?.solo_points ?? 0),
        //                                   solo_count = sp?.solo_count ?? 0,
        //                                   group_count = gm?.group_count ?? 0,
        //                                   total_programs = (sp?.solo_count ?? 0) + (gm?.group_count ?? 0) // ✅ New addition

        //                               }).ToList();

        //      // Step 7: Filter eligible students
        //      var eligibleStudents = qualifiedStudents
        //          .Where(s => s.solo_count >= 3 && s.group_count >= 2)
        //          .ToList();

        //      // Step 8: Assign Kalathilakam/Kalaprathibha
        //      var studentData = eligibleStudents
        //          .Select(s => new
        //          {
        //              s.student_id,
        //              s.student_name,
        //              s.image,
        //              s.imageurl,
        //              s.q_id,
        //              s.institute_name,
        //              s.participant_type,
        //              s.total_points,
        //              s.total_programs,

        //              kalathilakam_kalaprathibha = s.gender.ToLower() == "girl" ? "Kalathilakam" : "Kalaprathibha"
        //          }).ToList();

        //      // Step 9: Pick top per participant_type
        //      //var kalathilakamList = studentData
        //      //    .Where(x => x.kalathilakam_kalaprathibha == "Kalathilakam")
        //      //    .GroupBy(x => x.participant_type)
        //      //    .Select(g => g.OrderByDescending(s => s.total_points).First())
        //      //    .OrderBy(x => x.participant_type)
        //      //    .Select(s => new
        //      //    {
        //      //        participant_type = s.participant_type,
        //      //        student_name = s.student_name,
        //      //        image = s.image,
        //      //        imageurl = s.imageurl,
        //      //        q_id = s.q_id,
        //      //        institution = s.institute_name,
        //      //        points = s.total_points,
        //      //        total_programs = s.total_programs

        //      //    }).ToList();
        //      var kalathilakamList = studentData
        //.Where(x => x.kalathilakam_kalaprathibha == "Kalathilakam")
        //.GroupBy(x => x.participant_type)
        //.SelectMany(g =>
        //{
        //    var maxPoints = g.Max(s => s.total_points);
        //    // Take all students with max points but remove duplicates by student_id
        //    return g.Where(s => s.total_points == maxPoints)
        //            .GroupBy(s => s.student_name)
        //            .Select(sg => sg.First());
        //})
        //.OrderBy(x => x.participant_type)
        //.ThenBy(x => x.student_name)
        //.Select(s => new
        //{
        //    participant_type = s.participant_type,
        //    student_id = s.student_id,
        //    student_name = s.student_name,
        //    image = s.image,
        //    imageurl = s.imageurl,
        //    q_id = s.q_id,
        //    institution = s.institute_name,
        //    //points = s.total_points,
        //    points = Math.Round(s.total_points, 2),

        //    total_programs = s.total_programs
        //})
        //.ToList();



        //      //var kalaprathibhaList = studentData
        //      //    .Where(x => x.kalathilakam_kalaprathibha == "Kalaprathibha")
        //      //    .GroupBy(x => x.participant_type)
        //      //    .Select(g => g.OrderByDescending(s => s.total_points).First())
        //      //    .OrderBy(x => x.participant_type)
        //      //    .Select(s => new
        //      //    {
        //      //        participant_type = s.participant_type,
        //      //        student_name = s.student_name,
        //      //        image = s.image,
        //      //        imageurl = s.imageurl,
        //      //        q_id = s.q_id,
        //      //        institution = s.institute_name,
        //      //        points = s.total_points,
        //      //        total_programs = s.total_programs

        //      //    }).ToList();
        //      var kalaprathibhaList = studentData
        // .Where(x => x.kalathilakam_kalaprathibha == "Kalaprathibha")
        // .GroupBy(x => x.participant_type)
        // .SelectMany(g =>
        // {
        //     var maxPoints = g.Max(s => s.total_points);
        //     // Take all students with max points but remove duplicates by student_id
        //     return g.Where(s => s.total_points == maxPoints)
        //             .GroupBy(s => s.student_name)
        //             .Select(sg => sg.First());
        // })
        // .OrderBy(x => x.participant_type)
        // .ThenBy(x => x.student_name)
        // .Select(s => new
        // {
        //     participant_type = s.participant_type,
        //     student_id = s.student_id,

        //     student_name = s.student_name,
        //     image = s.image,
        //     imageurl = s.imageurl,
        //     q_id = s.q_id,
        //     institution = s.institute_name,
        //     //points = s.total_points,
        //     points = Math.Round(s.total_points, 2),

        //     total_programs = s.total_programs
        // })
        // .ToList();


        //      // Step 10: Final JSON response
        //      return Ok(new
        //      {
        //          status = true,
        //          message = "Success",
        //          data = new
        //          {
        //              kalathilakam = kalathilakamList,
        //              kalaprathibha = kalaprathibhaList
        //          }
        //      });
        //  }
        //
        //


        //
        public async Task<ActionResult> dashboard_kalathilakam_kalaprathibha(int? ac_year_id)
        {
            if (_context.tbl_prgm_participants == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";
            // Step 0: Filter programs where all participants have a chess_no
            //      var programsWithAllChessNumbers = _context.tbl_prgm_participants
            //.GroupBy(pp => pp.prgm_id)
            //.Where(g =>
            //    g.Any(pp => !string.IsNullOrEmpty(pp.chess_no)) &&
            //              g.Any(pp => pp.position != "1" &&
            //  pp.position != "2" &&
            //  pp.position != "3") &&

            //    g.All(pp => pp.ac_year_id == ac_year_id)
            //)
            //.Select(g => g.Key)
            //.ToList();

            var programsWithAllChessNumbers = _context.tbl_prgm_participants
    .GroupBy(pp => pp.prgm_id)
    .Where(g =>
        g.All(pp => pp.ac_year_id == ac_year_id) &&

        // At least one participant must have chess_no
        g.Any(pp => !string.IsNullOrEmpty(pp.chess_no)) &&

        // ALL participants must have position 1, 2 or 3
        g.Any(pp =>
            pp.position == "1" ||
            pp.position == "2" ||
            pp.position == "3")
    )
    .Select(g => g.Key)
    .ToList();


            // return Ok(programsWithAllChessNumbers);
            //  return Ok(programsWithAllChessNumbers);
            // Step 1: From those, get only programs that are Published or Transferred to Media


            var eligiblePrograms = _context.tbl_program
                .Where(p => programsWithAllChessNumbers.Contains(p.id) &&
                       (p.status == "Result Published" || p.status == "Transferred to Media") && p.offstage_onstage == "Onstage" &&
                       (p.ac_year_id == ac_year_id))
                .Select(p => p.id)
                .ToList();


            //        var studentsWithInvalidPosition = _context.tbl_prgm_participants
            //.Where(pp =>
            //    eligiblePrograms.Contains(pp.prgm_id.Value) &&
            //    pp.position != "1" &&
            //    pp.position != "2" &&
            //    pp.position != "3")
            //.Select(pp => pp.student_id)
            //.Distinct()
            //.ToList();

            // Step 2: If no eligible programs, return empty result
            if (!eligiblePrograms.Any())
            {
                return Ok(new
                {
                    status = true,
                    message = "Success",
                    data = new { kalathilakam = new List<object>(), kalaprathibha = new List<object>() }
                });
            }


            // Step 1: Load solo participation
            var soloParticipation = (from pp in _context.tbl_prgm_participants
                                     join p in _context.tbl_program on pp.prgm_id equals p.id
                                     where p.program_type == "solo" && eligiblePrograms.Contains(p.id)
          && (pp.position == "1"
              || pp.position == "2"
              || pp.position == "3")
                                     group pp by pp.student_id into g
                                     select new
                                     {
                                         student_id = g.Key,
                                         solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
                                         solo_points = g.Sum(x => x.average_point ?? 0)
                                     }).ToList();

            // Step 2: Load group participation counts
            //var groupParticipation = (from gm in _context.tbl_group_members
            //                          join p in _context.tbl_program on gm.prgm_id equals p.id
            //                          where p.program_type == "group" && eligiblePrograms.Contains(p.id)

            //                          group gm by gm.student_id into g
            //                          select new
            //                          {
            //                              student_id = g.Key,
            //                              group_count = g.Select(x => x.prgm_id).Distinct().Count()
            //                          }).ToList();

            var groupParticipation =
 (
     from gm in _context.tbl_group_members
     join p in _context.tbl_program
         on gm.prgm_id equals p.id

     join pp in _context.tbl_prgm_participants
         on new
         {
             gm.prgm_id,
             student_id = gm.student_id
         }
         equals new
         {
             pp.prgm_id,
             pp.student_id
         }

     where p.program_type == "group"
           && eligiblePrograms.Contains(p.id)
           && (pp.position == "1" ||
               pp.position == "2" ||
               pp.position == "3")

     group gm by gm.student_id into g

     select new
     {
         student_id = g.Key,
         group_count = g.Select(x => x.prgm_id).Distinct().Count()
     }
 ).ToList();

            // Step 3: Load group points per institute per program (materialize in memory)
            var groupPointsByInstitute = (from pp in _context.tbl_prgm_participants
                                          join p in _context.tbl_program on pp.prgm_id equals p.id
                                          join s in _context.tbl_student on pp.student_id equals s.id
                                          where p.program_type == "group" && eligiblePrograms.Contains(p.id) && (pp.position == "1" || pp.position == "2" || pp.position == "3")

                                          group pp by new { s.institute, pp.prgm_id } into g
                                          select new
                                          {
                                              institute_id = g.Key.institute,
                                              prgm_id = g.Key.prgm_id,
                                              group_points = g.Sum(x => x.average_point ?? 0)
                                          }).ToList(); // materialize

            // Step 4: Assign group points to each student based on their institute (in memory)
            //var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
            //                          join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
            //                          join gpi in groupPointsByInstitute on new { s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, gpi.prgm_id }
            //                          group gpi by gm.student_id into g
            //                          select new
            //                          {
            //                              student_id = g.Key,
            //                              group_points = g.Sum(x => x.group_points)
            //                          }).ToList();

            // Step 4: Assign group points to each student based on their institute
            var groupMembersData = _context.tbl_group_members
                .ToList();

            var studentsData = _context.tbl_student
                .ToList();

            var participantsData = _context.tbl_prgm_participants
                .Where(pp =>
                    pp.position == "1" ||
                    pp.position == "2" ||
                    pp.position == "3")
                .ToList();

            var studentGroupPoints =
            (
                from gm in groupMembersData
                join pp in participantsData
                    on new
                    {
                        gm.prgm_id,
                        student_id = gm.student_id
                    }
                    equals new
                    {
                        pp.prgm_id,
                        pp.student_id
                    }
                join s in studentsData
                    on gm.student_id equals s.id
                join gpi in groupPointsByInstitute
                    on new
                    {
                        institute = s.institute,
                        gm.prgm_id
                    }
                    equals new
                    {
                        institute = gpi.institute_id,
                        gpi.prgm_id
                    }
                group gpi by gm.student_id into g
                select new
                {
                    student_id = g.Key,
                    group_points = g.Sum(x => x.group_points)
                }
            ).ToList();


            // Step 5: Base student data
            var baseStudents = (from s in _context.tbl_student
                                join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
                                join p in _context.tbl_program on pp.prgm_id equals p.id
                                join i in _context.tbl_institute on s.institute equals i.id into instJoin
                                from inst in instJoin.DefaultIfEmpty()
                                where eligiblePrograms.Contains(p.id)
                                 && (pp.position == "1"
                              || pp.position == "2"
                              || pp.position == "3")

                                //&& !studentsWithInvalidPosition.Contains(s.id)

                                select new
                                {
                                    s.id,
                                    s.name,
                                    image = s.image,
                                    imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
                                    s.admsn_no,
                                    institute_name = inst.name,
                                    s.gender,
                                    p.participant_type,
                                    pp.position,
                                }).ToList();

            // Step 6: Merge participation + points
            var qualifiedStudents = (from bs in baseStudents
                                     join sp in soloParticipation on bs.id equals sp.student_id into spJoin
                                     from sp in spJoin.DefaultIfEmpty()
                                     join gp in studentGroupPoints on bs.id equals gp.student_id into gpJoin
                                     from gp in gpJoin.DefaultIfEmpty()
                                     join gm in groupParticipation on bs.id equals gm.student_id into gmJoin
                                     from gm in gmJoin.DefaultIfEmpty()
                                         //where (bs.position == "1" || bs.position == "2" || bs.position == "3")
                                     select new
                                     {
                                         student_id = bs.id,
                                         student_name = bs.name,
                                         image = bs.image,
                                         imageurl = bs.imageurl,
                                         q_id = bs.admsn_no,
                                         institute_name = bs.institute_name,
                                         gender = bs.gender,
                                         participant_type = bs.participant_type,
                                         total_points = (sp?.solo_points ?? 0),
                                         solo_count = sp?.solo_count ?? 0,
                                         group_count = gm?.group_count ?? 0,
                                         total_programs = (sp?.solo_count ?? 0) + (gm?.group_count ?? 0) // ✅ New addition

                                     }).ToList();
            // return Ok(qualifiedStudents);
            //Step 7: Filter eligible students
            //var eligibleStudents = qualifiedStudents
            //    .Where(s => s.solo_count >= 3 && s.group_count >= 2)
            //        //    .ToList();
            //        var eligibleStudents = qualifiedStudents
            //.Where(s => s.solo_count >= 3)
            //.GroupBy(s => new { s.participant_type, s.gender })
            //.SelectMany(g =>
            //{
            //    var withTwoGroups = g.Where(x => x.group_count >= 2).ToList();

            //    if (withTwoGroups.Any())
            //    {
            //        return withTwoGroups;
            //    }

            //    return g.Where(x => x.group_count >= 1);
            //})
            //.ToList();

            var eligibleStudents = qualifiedStudents
    .Where(s => s.solo_count >= 3)
    .GroupBy(s => new { s.participant_type, s.gender })
    .SelectMany(g =>
    {
        var withTwoGroups = g.Where(x => x.group_count >= 2).ToList();
        if (withTwoGroups.Any()) return withTwoGroups;

        var withOneGroups = g.Where(x => x.group_count >= 1).ToList();
        if (withOneGroups.Any()) return withOneGroups;

        return g.ToList();
    })
    .ToList();
            //        // Step 8: Assign Kalathilakam/Kalaprathibha
            var studentData = eligibleStudents
                .Select(s => new
                {
                    s.student_id,
                    s.student_name,
                    s.image,
                    s.imageurl,
                    s.q_id,
                    s.institute_name,
                    s.participant_type,
                    s.total_points,
                    s.total_programs,

                    kalathilakam_kalaprathibha = s.gender.ToLower() == "girl" ? "Kalathilakam" : "Kalaprathibha"
                }).ToList();


            var kalathilakamList = studentData
      .Where(x => x.kalathilakam_kalaprathibha == "Kalathilakam")
      .GroupBy(x => x.participant_type)
      .SelectMany(g =>
      {
          var maxPoints = g.Max(s => s.total_points);
          // Take all students with max points but remove duplicates by student_id
          return g.Where(s => s.total_points == maxPoints)
                  .GroupBy(s => s.student_name)
                  .Select(sg => sg.First());
      })
      .OrderBy(x => x.participant_type)
      .ThenBy(x => x.student_name)
      .Select(s => new
      {
          participant_type = s.participant_type,
          student_id = s.student_id,
          student_name = s.student_name,
          image = s.image,
          imageurl = s.imageurl,
          q_id = s.q_id,
          institution = s.institute_name,
          //points = s.total_points,
          points = Math.Round(s.total_points, 2),

          total_programs = s.total_programs
      })
      .ToList();


            var kalaprathibhaList = studentData
       .Where(x => x.kalathilakam_kalaprathibha == "Kalaprathibha")
       .GroupBy(x => x.participant_type)
       .SelectMany(g =>
       {
           var maxPoints = g.Max(s => s.total_points);
           // Take all students with max points but remove duplicates by student_id
           return g.Where(s => s.total_points == maxPoints)
                   .GroupBy(s => s.student_name)
                   .Select(sg => sg.First());
       })
       .OrderBy(x => x.participant_type)
       .ThenBy(x => x.student_name)
       .Select(s => new
       {
           participant_type = s.participant_type,
           student_id = s.student_id,

           student_name = s.student_name,
           image = s.image,
           imageurl = s.imageurl,
           q_id = s.q_id,
           institution = s.institute_name,
           //points = s.total_points,
           points = Math.Round(s.total_points, 2),

           total_programs = s.total_programs
       })
       .ToList();


            // Step 10: Final JSON response
            return Ok(new
            {
                status = true,
                message = "Success",
                data = new
                {
                    kalathilakam = kalathilakamList,
                    kalaprathibha = kalaprathibhaList
                }
            });
        }
        ////////////////////////latest/////////////////////
        ////////////////////////latest/////////////////////


        //  public async Task<ActionResult> dashboard_kalathilakam_kalaprathibha(int ac_year_id)
        //  {
        //      if (_context.tbl_prgm_participants == null)
        //      {
        //          return Ok(new { status = false, message = "Data not found" });
        //      }
        //      var baseUrl = $"{Request.Scheme}://{Request.Host}/";
        //      // Step 0: Filter programs where all participants have a chess_no
        //      var programsWithAllChessNumbers = _context.tbl_prgm_participants
        //          .GroupBy(pp => pp.prgm_id)
        //          .Where(g => g.All(pp => !string.IsNullOrEmpty(pp.chess_no))
        //          &&g.All(pp => pp.ac_year_id == ac_year_id)) // all have chess_no
        //          .Select(g => g.Key)
        //          .ToList();

        //      //  return Ok(programsWithAllChessNumbers);
        //      // Step 1: From those, get only programs that are Published or Transferred to Media
        //      var eligiblePrograms = _context.tbl_program
        //          .Where(p => programsWithAllChessNumbers.Contains(p.id) &&
        //                 (p.status == "Result Published" || p.status == "Transferred to Media")&&
        //                 (p.ac_year_id == ac_year_id))
        //          .Select(p => p.id)
        //          .ToList();
        //      // Step 2: If no eligible programs, return empty result
        //      if (!eligiblePrograms.Any())
        //      {
        //          return Ok(new
        //          {
        //              status = true,
        //              message = "Success",
        //              data = new { kalathilakam = new List<object>(), kalaprathibha = new List<object>() }
        //          });
        //      }



        //      // Step 1: Load solo participation
        //      var soloParticipation = (from pp in _context.tbl_prgm_participants
        //                               join p in _context.tbl_program on pp.prgm_id equals p.id
        //                               where p.program_type == "solo" && eligiblePrograms.Contains(p.id)
        //                               group pp by pp.student_id into g
        //                               select new
        //                               {
        //                                   student_id = g.Key,
        //                                   solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
        //                                   solo_points = g.Sum(x => x.average_point ?? 0)
        //                               }).ToList();

        //      // Step 2: Load group participation counts
        //      var groupParticipation = (from gm in _context.tbl_group_members
        //                                join p in _context.tbl_program on gm.prgm_id equals p.id
        //                                where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                                group gm by gm.student_id into g
        //                                select new
        //                                {
        //                                    student_id = g.Key,
        //                                    group_count = g.Select(x => x.prgm_id).Count()
        //                                }).ToList();

        //      // Step 3: Load group points per institute per program (materialize in memory)
        //      var groupPointsByInstitute = (from pp in _context.tbl_prgm_participants
        //                                    join p in _context.tbl_program on pp.prgm_id equals p.id
        //                                    join s in _context.tbl_student on pp.student_id equals s.id
        //                                    where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                                    group pp by new { s.institute, pp.prgm_id } into g
        //                                    select new
        //                                    {
        //                                        institute_id = g.Key.institute,
        //                                        prgm_id = g.Key.prgm_id,
        //                                        group_points = g.Sum(x => x.average_point ?? 0)
        //                                    }).ToList(); // materialize

        //      // Step 4: Assign group points to each student based on their institute (in memory)
        //      var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
        //                                join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
        //                                join gpi in groupPointsByInstitute on new { s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, gpi.prgm_id }
        //                                group gpi by gm.student_id into g
        //                                select new
        //                                {
        //                                    student_id = g.Key,
        //                                    group_points = g.Sum(x => x.group_points)
        //                                }).ToList();


        //      // Step 5: Base student data
        //      var baseStudents = (from s in _context.tbl_student
        //                          join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
        //                          join p in _context.tbl_program on pp.prgm_id equals p.id
        //                          join i in _context.tbl_institute on s.institute equals i.id into instJoin
        //                          from inst in instJoin.DefaultIfEmpty()
        //                          select new
        //                          {
        //                              s.id,
        //                              s.name,
        //                              image = s.image,
        //                              imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
        //                              s.admsn_no,
        //                              institute_name = inst.name,
        //                              s.gender,
        //                              p.participant_type
        //                          }).ToList();

        //      // Step 6: Merge participation + points
        //      var qualifiedStudents = (from bs in baseStudents
        //                               join sp in soloParticipation on bs.id equals sp.student_id into spJoin
        //                               from sp in spJoin.DefaultIfEmpty()
        //                               join gp in studentGroupPoints on bs.id equals gp.student_id into gpJoin
        //                               from gp in gpJoin.DefaultIfEmpty()
        //                               join gm in groupParticipation on bs.id equals gm.student_id into gmJoin
        //                               from gm in gmJoin.DefaultIfEmpty()
        //                               select new
        //                               {
        //                                   student_id = bs.id,
        //                                   student_name = bs.name,
        //                                   image = bs.image,
        //                                   imageurl = bs.imageurl,
        //                                   q_id = bs.admsn_no,
        //                                   institute_name = bs.institute_name,
        //                                   gender = bs.gender,
        //                                   participant_type = bs.participant_type,
        //                                   total_points = (sp?.solo_points ?? 0),
        //                                   solo_count = sp?.solo_count ?? 0,
        //                                   group_count = gm?.group_count ?? 0,
        //                                   total_programs = (sp?.solo_count ?? 0) + (gm?.group_count ?? 0) // ✅ New addition

        //                               }).ToList();

        //      // Step 7: Filter eligible students
        //      var eligibleStudents = qualifiedStudents
        //          .Where(s => s.solo_count >= 3 && s.group_count >= 2)
        //          .ToList();

        //      // Step 8: Assign Kalathilakam/Kalaprathibha
        //      var studentData = eligibleStudents
        //          .Select(s => new
        //          {
        //              s.student_id,
        //              s.student_name,
        //              s.image,
        //              s.imageurl,
        //              s.q_id,
        //              s.institute_name,
        //              s.participant_type,
        //              s.total_points,
        //              s.total_programs,

        //              kalathilakam_kalaprathibha = s.gender.ToLower() == "girl" ? "Kalathilakam" : "Kalaprathibha"
        //          }).ToList();

        //      // Step 9: Pick top per participant_type
        //      //var kalathilakamList = studentData
        //      //    .Where(x => x.kalathilakam_kalaprathibha == "Kalathilakam")
        //      //    .GroupBy(x => x.participant_type)
        //      //    .Select(g => g.OrderByDescending(s => s.total_points).First())
        //      //    .OrderBy(x => x.participant_type)
        //      //    .Select(s => new
        //      //    {
        //      //        participant_type = s.participant_type,
        //      //        student_name = s.student_name,
        //      //        image = s.image,
        //      //        imageurl = s.imageurl,
        //      //        q_id = s.q_id,
        //      //        institution = s.institute_name,
        //      //        points = s.total_points,
        //      //        total_programs = s.total_programs

        //      //    }).ToList();
        //      var kalathilakamList = studentData
        //.Where(x => x.kalathilakam_kalaprathibha == "Kalathilakam")
        //.GroupBy(x => x.participant_type)
        //.SelectMany(g =>
        //{
        //    var maxPoints = g.Max(s => s.total_points);
        //    // Take all students with max points but remove duplicates by student_id
        //    return g.Where(s => s.total_points == maxPoints)
        //            .GroupBy(s => s.student_name)
        //            .Select(sg => sg.First());
        //})
        //.OrderBy(x => x.participant_type)
        //.ThenBy(x => x.student_name)
        //.Select(s => new
        //{
        //    participant_type = s.participant_type,
        //    student_id = s.student_id,
        //    student_name = s.student_name,
        //    image = s.image,
        //    imageurl = s.imageurl,
        //    q_id = s.q_id,
        //    institution = s.institute_name,
        //    //points = s.total_points,
        //    points = Math.Round(s.total_points, 2),

        //    total_programs = s.total_programs
        //})
        //.ToList();



        //      //var kalaprathibhaList = studentData
        //      //    .Where(x => x.kalathilakam_kalaprathibha == "Kalaprathibha")
        //      //    .GroupBy(x => x.participant_type)
        //      //    .Select(g => g.OrderByDescending(s => s.total_points).First())
        //      //    .OrderBy(x => x.participant_type)
        //      //    .Select(s => new
        //      //    {
        //      //        participant_type = s.participant_type,
        //      //        student_name = s.student_name,
        //      //        image = s.image,
        //      //        imageurl = s.imageurl,
        //      //        q_id = s.q_id,
        //      //        institution = s.institute_name,
        //      //        points = s.total_points,
        //      //        total_programs = s.total_programs

        //      //    }).ToList();
        //      var kalaprathibhaList = studentData
        // .Where(x => x.kalathilakam_kalaprathibha == "Kalaprathibha")
        // .GroupBy(x => x.participant_type)
        // .SelectMany(g =>
        // {
        //     var maxPoints = g.Max(s => s.total_points);
        //     // Take all students with max points but remove duplicates by student_id
        //     return g.Where(s => s.total_points == maxPoints)
        //             .GroupBy(s => s.student_name)
        //             .Select(sg => sg.First());
        // })
        // .OrderBy(x => x.participant_type)
        // .ThenBy(x => x.student_name)
        // .Select(s => new
        // {
        //     participant_type = s.participant_type,
        //     student_id = s.student_id,

        //     student_name = s.student_name,
        //     image = s.image,
        //     imageurl = s.imageurl,
        //     q_id = s.q_id,
        //     institution = s.institute_name,
        //     //points = s.total_points,
        //     points = Math.Round(s.total_points, 2),

        //     total_programs = s.total_programs
        // })
        // .ToList();


        //      // Step 10: Final JSON response
        //      return Ok(new
        //      {
        //          status = true,
        //          message = "Success",
        //          data = new
        //          {
        //              kalathilakam = kalathilakamList,
        //              kalaprathibha = kalaprathibhaList
        //          }
        //      });
        //  }

        ////////////////////////latest/////////////////////

        //result published reports
        [HttpGet]
        [Route("get_results_report")]
        //public async Task<IActionResult> get_results_report(DateTime? from_date, DateTime? to_date,int? institute_id)
        //{
        //    var data = await (
        //        from pp in _context.tbl_prgm_participants
        //        join p in _context.tbl_program on pp.prgm_id equals p.id into ppJoin
        //        from pr in ppJoin.DefaultIfEmpty()
        //        join s in _context.tbl_stage on pr.stage_id equals s.id into sJoin
        //        from s in sJoin.DefaultIfEmpty()
        //        join st in _context.tbl_student on pp.student_id equals st.id into stJoin
        //        from st in stJoin.DefaultIfEmpty()
        //        join i in _context.tbl_institute on st.institute equals i.id into iJoin
        //        from i in iJoin.DefaultIfEmpty()
        //        where pr.status == "result published" || pr.status == "Transferred to Media"    
        //        group new { pr, pp, s, st, i } by new
        //        {
        //            s.stage_name,
        //            pr.program_name,
        //            pr.program_type,
        //            pr.color_code
        //        } into grp
        //        select new
        //        {
        //            stage_name = grp.Key.stage_name,
        //            program_name = grp.Key.program_name,
        //            program_type = grp.Key.program_type,
        //            color_code = grp.Key.color_code,
        //            participant_type = grp.FirstOrDefault().pr.participant_type,
        //            gender = grp.FirstOrDefault().pr.gender,
        //            status = grp.FirstOrDefault().pr.status,
        //            date = grp.FirstOrDefault().pr.date,
        //            participants = grp.Select(x => new
        //            {
        //                student_id = x.pp.student_id,
        //                institute = x.i.name,
        //                student = x.st.name,
        //                position = x.pp.position
        //            }).ToList()
        //        }).ToListAsync();

        //    // Apply filters
        //    if (from_date.HasValue)
        //    {
        //        data = data.Where(g => g.date.HasValue && g.date.Value.Date >= from_date.Value.Date).ToList();
        //    }

        //    if (to_date.HasValue)
        //    {
        //        data = data.Where(g => g.date.HasValue && g.date.Value <= to_date).ToList();
        //    }



        //    using (var workbook = new XLWorkbook())
        //    {
        //        var ws = workbook.Worksheets.Add("Results");

        //        // Headers
        //        ws.Cell(1, 1).Value = "Stage";
        //        ws.Cell(1, 2).Value = "Program";
        //        ws.Cell(1, 3).Value = "Type";
        //        ws.Cell(1, 4).Value = "Participant Type";
        //        ws.Cell(1, 5).Value = "Gender";
        //        ws.Cell(1, 6).Value = "Date";
        //        ws.Cell(1, 7).Value = "Student ID";
        //        ws.Cell(1, 8).Value = "Student";
        //        ws.Cell(1, 9).Value = "Institute";
        //        ws.Cell(1, 10).Value = "Position";

        //        int row = 2;
        //        foreach (var item in data)
        //        {
        //            foreach (var p in item.participants)
        //            {
        //                ws.Cell(row, 1).Value = item.stage_name;
        //                ws.Cell(row, 2).Value = item.program_name;
        //                ws.Cell(row, 3).Value = item.program_type;
        //                ws.Cell(row, 4).Value = item.participant_type;
        //                ws.Cell(row, 5).Value = item.gender;
        //                ws.Cell(row, 6).Value = item.date?.ToString("yyyy-MM-dd");
        //                ws.Cell(row, 7).Value = p.student_id;
        //                ws.Cell(row, 8).Value = p.student;
        //                ws.Cell(row, 9).Value = p.institute;
        //                ws.Cell(row, 10).Value = p.position;
        //                row++;
        //            }
        //        }

        //        // Auto-fit
        //        ws.Columns().AdjustToContents();

        //        using (var stream = new MemoryStream())
        //        {
        //            workbook.SaveAs(stream);
        //            var content = stream.ToArray();
        //            return File(content,
        //                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        //                        "ResultsReport.xlsx");
        //        }
        //    }
        //}

        public async Task<IActionResult> get_results_report(DateTime? from_date, DateTime? to_date,int?institute_id,string? category,string? program_type,int?ac_year_id)
        {
            try
            {
                // 🔹 Step 1: Get solo program stats per student
                var soloStats = await (
                    from pp in _context.tbl_prgm_participants
                    join pr in _context.tbl_program on pp.prgm_id equals pr.id
                    join st in _context.tbl_student on pp.student_id equals st.id
                    join i in _context.tbl_institute on st.institute equals i.id
                    join ac in _context.tbl_academic_year on pp.ac_year_id equals ac.id
                    where pr.program_type == "Solo" &&
                          (pr.status.ToLower() == "result published" || pr.status == "Transferred to Media")
                    select new
                    {
                        st.name,
                        st.admsn_no, // q_id
                        st.institute,
                        institute_name=i.name,
                        pr.participant_type,
                        pr.program_type,
                        pp.student_id,
                        pp.average_point,
                        pr.date,
                        pp.ac_year_id,
                        ac.year

                    }
                ).ToListAsync();

                // 🔹 Step 2: Apply date filters
                if (from_date.HasValue)
                    soloStats = soloStats.Where(x => x.date.HasValue && x.date.Value.Date >= from_date.Value.Date).ToList();

                if (to_date.HasValue)
                    soloStats = soloStats.Where(x => x.date.HasValue && x.date.Value.Date <= to_date.Value.Date).ToList();

                // 🔹 Step 3: Apply institute filter (if provided)
                if (institute_id.HasValue)
                    soloStats = soloStats.Where(x => x.institute == institute_id.Value).ToList();

                if (!string.IsNullOrEmpty(category))
                    soloStats = soloStats.Where(g => g.participant_type == category).ToList();

                if (!string.IsNullOrEmpty(program_type))
                    soloStats = soloStats.Where(g => g.program_type == program_type).ToList();
                if (ac_year_id.HasValue)
                    soloStats = soloStats.Where(x => x.ac_year_id == ac_year_id.Value).ToList();


                // 🔹 Step 3: Group by student to calculate stats
                var grouped = soloStats
                    .GroupBy(x => new { x.student_id, x.name, x.admsn_no,x.institute_name ,x.participant_type,x.program_type,x.year })
                    .Select(g => new
                    {
                        student_name = g.Key.name,
                        q_id = g.Key.admsn_no,
                        institute_name = g.Key.institute_name,
                        participant_type = g.Key.participant_type,
                        program_type = g.Key.program_type,

                        solo_program_count = g.Count(),
                        solo_program_points = g.Sum(x => x.average_point ?? 0),
                        year=g.Key.year
                    })
                    .OrderBy(x => x.student_name)
                    .ToList();

                // 🔹 Step 4: Generate Excel
                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("Solo Results");

                    // Headers
                    ws.Cell(1, 1).Value = "Student Name";
                    ws.Cell(1, 2).Value = "Q ID";
                    ws.Cell(1, 3).Value = "Institute";
                    ws.Cell(1, 4).Value = "Solo Program Count";
                    ws.Cell(1, 5).Value = "Solo Program Points";
                    ws.Cell(1, 6).Value = "Category";
                    ws.Cell(1, 7).Value = "Program Type";
                    ws.Cell(1, 8).Value = "Academic Year";


                    int row = 2;
                    foreach (var s in grouped)
                    {
                        ws.Cell(row, 1).Value = s.student_name;
                        ws.Cell(row, 2).Value = s.q_id;
                        ws.Cell(row, 3).Value = s.institute_name;
                        ws.Cell(row, 4).Value = s.solo_program_count;
                        ws.Cell(row, 5).Value = Math.Round(s.solo_program_points, 2);
                        ws.Cell(row, 6).Value = s.participant_type;
                        ws.Cell(row, 7).Value = s.program_type;
                        ws.Cell(row, 8).Value = s.year;
                        row++;
                    }

                    ws.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        return File(content,
                                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                    "SoloResultsReport.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                return Ok(new { status = false, message = ex.Message });
            }
        }

        //
        //



        [HttpGet]
        [Route("judges_completed_prgrm")]
        public async Task<ActionResult> judges_completed_prgrm(int? program_id)
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



            var rawData = await (from pt in _context.tbl_prgm_point

                                 join jc in _context.tbl_judgement_criteria
                                                      on pt.judgement_criteria_id equals jc.id into jcJoin
                                 from jc in jcJoin.DefaultIfEmpty()
                                 where pt.prgm_id == program_id && pt.judge_id == ref_id
                                 select new
                                 {
                                     pt.chess_no,
                                     jc.name,
                                     pt.point,
                                     pt.comments
                                 }).ToListAsync();


            if (rawData == null || !rawData.Any())
            {
                return Ok(new { status = false, message = "No data found" });
            }

            var list = rawData
                .GroupBy(r => r.chess_no)
                .Select(g => new
                {
                    chess_no = g.Key,
                    criteria = g.ToDictionary(x => x.name, x => x.point),
                    comments = g
            .Where(x => !string.IsNullOrEmpty(x.comments))
            .Select(x => x.comments)
            .FirstOrDefault() ?? "",
                    points = g.Sum(x => x.point)
                })
                .ToList();
            var progm = await (from p in _context.tbl_program
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
                                     }).FirstOrDefaultAsync();

            return Ok(new { status = true, message = "success",
                program_id = progm.program_id,
                program_name = progm.program_name,
                stage_name = progm.stage_name,
                participant_type = progm.participant_type,
                item_code = progm.item_code,
                gender = progm.gender,
                program_type = progm.program_type, data = list });
        }

        //
        [HttpGet]
        [Route("kalathilakam_kalaprathiba_details")]
        public async Task<ActionResult> kalathilakam_kalaprathiba_details(int? student_id)
        {
            if (student_id == null)
            {
                return Ok(new { status = false, message = "student_id is required" });
            }

            // Join student with institute to get institute name
            var studentWithInstitute = await (
                from s in _context.tbl_student
                join i in _context.tbl_institute on s.institute equals i.id
                where s.id == student_id
                select new
                {
                    s.id,
                    s.name,
                    s.admsn_no,
                    s.category,
                    institute_id = s.institute,
                    institute_name = i.name
                }).FirstOrDefaultAsync();

            if (studentWithInstitute == null)
            {
                return Ok(new { status = false, message = "Student not found" });
            }

            // Fetch solo programs
            var soloPrograms = await (
                from pp in _context.tbl_prgm_participants
                join p in _context.tbl_program on pp.prgm_id equals p.id
                where pp.status != "deleted"
                      && (p.status.ToLower() == "result published" || p.status.ToLower() == "transferred to media")
                      && pp.student_id == student_id
                      && p.program_type.ToLower() == "solo"
                select new
                {
                    item_code = p.item_code,
                    program_name = p.program_name,
                    program_type = "Solo",
                    group_name = "",
                    grade = pp.grade,
                    points = pp.total_point,
                    average_point=pp.average_point,
                    position=pp.position,
                    position_point=pp.position_point,
                    total_point=pp.total_point,
                }).ToListAsync();

            // Fetch group programs where the student is part of the group
            //var groupPrograms = await (
            //    from gm in _context.tbl_group_members
            //    join pp in _context.tbl_prgm_participants
            //        on new { gm.prgm_id, gm.group_name } equals new { pp.prgm_id, pp.group_name }
            //    join p in _context.tbl_program on pp.prgm_id equals p.id
            //    where gm.student_id == student_id
            //          && pp.status != "deleted"
            //          && p.program_type.ToLower() == "group"
            //          && (p.status.ToLower() == "result published" || p.status.ToLower() == "transferred to media")
            //    select new
            //    {
            //        item_code = p.item_code,
            //        program_name = p.program_name,
            //        program_type = "Group",
            //        group_name = gm.group_name,
            //        grade = pp.grade,
            //        points = pp.total_point // main participant's points
            //    }).Distinct().ToListAsync();

            var groupPrograms = await (
    from gm in _context.tbl_group_members
    join pp in _context.tbl_prgm_participants
        on new { gm.prgm_id, gm.group_name }
        equals new { pp.prgm_id, pp.group_name }
    join p in _context.tbl_program
        on pp.prgm_id equals p.id
    where gm.student_id == student_id
          && pp.status != "deleted"
          && p.program_type.ToLower() == "group"
          && (p.status.ToLower() == "result published"
              || p.status.ToLower() == "transferred to media")
    group new { pp, p, gm } by new
    {
        p.item_code,
        p.program_name,
        gm.group_name
    } into g
    select new
    {
        item_code = g.Key.item_code,
        program_name = g.Key.program_name,
        program_type = "Group",
        group_name = g.Key.group_name,
        grade = g.OrderByDescending(x => x.pp.grade_point)
                  .Select(x => x.pp.grade)
                  .FirstOrDefault(),
        points = g.Max(x => x.pp.total_point),
        average_point = g.Max(x => x.pp.average_point),
        position = g.Max(x => x.pp.position),
        position_point = g.Max(x => x.pp.position_point),
        total_point = g.Max(x => x.pp.total_point)
    }
).ToListAsync();

            // Combine solo and group programs
            //var allPrograms = soloPrograms.Concat(groupPrograms)
            //                              .OrderBy(p => p.item_code)
            //                              .ToList();

            //if (!allPrograms.Any())
            //{
            //    return Ok(new { status = false, message = "No programs found for student" });
            //}
            if (!soloPrograms.Any() && !groupPrograms.Any())
            {
                return Ok(new { status = false, message = "No programs found for student" });
            }
            return Ok(new
            {
                status = true,
                message = "Success",
                student_name = studentWithInstitute.name,
                q_id = studentWithInstitute.admsn_no,
                category = studentWithInstitute.category,
                institute = studentWithInstitute.institute_id,
                institute_name = studentWithInstitute.institute_name,
                programs = new List<object>
    {
        new
        {
            solo = soloPrograms.OrderBy(x => x.item_code).ToList(),
            group = groupPrograms.OrderBy(x => x.item_code).ToList()
        }
    }
            });
        }


        //


      
        //

        [HttpGet]
        [Route("academic_year")]
        public async Task<IActionResult>academic_year()
        {
            if (_context.tbl_academic_year == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }
            var data = await _context.tbl_academic_year
                .Select(x => new
                {
                    id = x.id,
                    year = x.year,
                    x.is_active
                })
                .ToListAsync();
            return Ok(new { status = true, message = "Success", data });
        }


        [HttpPut]
        [Route("change_academic_year")]
        public async Task<IActionResult> ChangeAcademicYear([FromForm]int?id,[FromForm]int? ac_year_id)
        {

            if (_context.tbl_academic_year == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }

            var user = await _context.tbl_user.FindAsync(id);
            if (user == null)   
                {
                return Ok(new { status = false, message = "User not found" });
            }
            var updted_user = await _context.tbl_user
                .Where(u => u.id == id)
                .FirstOrDefaultAsync();

            if (updted_user != null)
            {
                updted_user.logged_ac_year = ac_year_id;

                _context.tbl_user.Update(updted_user);
                await _context.SaveChangesAsync();
            }
            return Ok(new { status = true, message = "Data updated Successfully"});

        }

        // Add this diagnostic endpoint inside the dashboardController class

        // Replace the existing debug_kalathilakam method with this improved diagnostic version.

        [HttpGet]
        [Route("debug_kalathilakam")]
        public async Task<ActionResult> debug_kalathilakam(int? ac_year_id)
        {
            // programs where every participant has a chest_no (and optional academic year)
            var programsWithAllChessNumbers = await _context.tbl_prgm_participants
                .GroupBy(pp => pp.prgm_id)
                .Where(g =>
                    g.All(pp => !string.IsNullOrEmpty(pp.chess_no)) &&
                    g.All(pp => !ac_year_id.HasValue || pp.ac_year_id == ac_year_id)
                )
                .Select(g => g.Key)
                .ToListAsync();

            // eligible programs (case-insensitive status check)
            var eligibleProgramIds = await _context.tbl_program
                .Where(p =>
                    programsWithAllChessNumbers.Contains(p.id) &&
                    p.status != null &&
                    (p.status.ToLower() == "result published" || p.status.ToLower() == "transferred to media") &&
                    (!ac_year_id.HasValue || p.ac_year_id == ac_year_id)
                )
                .Select(p => p.id)
                .ToListAsync();

            // solo participation and counts per student for eligible programs
            var soloParticipation = await (
                from pp in _context.tbl_prgm_participants
                join p in _context.tbl_program on pp.prgm_id equals p.id
                where p.program_type != null && p.program_type.ToLower() == "solo"
                      && eligibleProgramIds.Contains(p.id)
                group pp by pp.student_id into g
                select new
                {
                    student_id = g.Key,
                    solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
                    solo_points = g.Sum(x => x.average_point ?? 0)
                }).ToListAsync();

            // group participation counts per student (members)
            var groupParticipation = await (
                from gm in _context.tbl_group_members
                join p in _context.tbl_program on gm.prgm_id equals p.id
                where p.program_type != null && p.program_type.ToLower() == "group"
                      && eligibleProgramIds.Contains(p.id)
                group gm by gm.student_id into g
                select new
                {
                    student_id = g.Key,
                    group_count = g.Select(x => x.prgm_id).Distinct().Count()
                }).ToListAsync();

            // group points aggregated per student by institute (materialized)
            var groupPointsByInstitute = await (
                from pp in _context.tbl_prgm_participants
                join p in _context.tbl_program on pp.prgm_id equals p.id
                join s in _context.tbl_student on pp.student_id equals s.id
                where p.program_type != null && p.program_type.ToLower() == "group"
                      && eligibleProgramIds.Contains(p.id)
                group pp by new { s.institute, pp.prgm_id } into g
                select new
                {
                    institute_id = g.Key.institute,
                    prgm_id = g.Key.prgm_id,
                    group_points = g.Sum(x => x.average_point ?? 0)
                }).ToListAsync();

            var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
                                      join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
                                      join gpi in groupPointsByInstitute on new { institute = s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, prgm_id = gpi.prgm_id }
                                      group gpi by gm.student_id into g
                                      select new
                                      {
                                          student_id = g.Key,
                                          group_points = g.Sum(x => x.group_points)
                                      }).ToList();

            // base students participating in eligible programs
            var baseStudents = await (
                from s in _context.tbl_student
                join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
                join p in _context.tbl_program on pp.prgm_id equals p.id
                where eligibleProgramIds.Contains(p.id)
                select new
                {
                    s.id,
                    s.name,
                    s.gender,
                    s.admsn_no,
                    s.institute
                }).Distinct().ToListAsync();

            // build per-student diagnostics (solo_count, group_count, points)
            var perStudent = baseStudents.Select(bs =>
            {
                var sp = soloParticipation.FirstOrDefault(x => x.student_id == bs.id);
                var gp = groupParticipation.FirstOrDefault(x => x.student_id == bs.id);
                var sgp = studentGroupPoints.FirstOrDefault(x => x.student_id == bs.id);

                return new
                {
                    student_id = bs.id,
                    name = bs.name,
                    gender = bs.gender,
                    q_id = bs.admsn_no,
                    institute = bs.institute,
                    solo_count = sp?.solo_count ?? 0,
                    solo_points = Math.Round(sp?.solo_points ?? 0, 2),
                    group_count = gp?.group_count ?? 0,
                    group_points = Math.Round(sgp?.group_points ?? 0, 2),
                    total_programs = (sp?.solo_count ?? 0) + (gp?.group_count ?? 0),
                    total_points = Math.Round((sp?.solo_points ?? 0) + (sgp?.group_points ?? 0), 2),
                    meets_solo_threshold = (sp?.solo_count ?? 0) >= 3,
                    meets_group_threshold = (gp?.group_count ?? 0) >= 2
                };
            }).ToList();

            var eligibleStudents = perStudent.Where(s => s.meets_solo_threshold && s.meets_group_threshold).ToList();

            var failsSoloOnly = perStudent.Where(s => !s.meets_solo_threshold && s.meets_group_threshold).ToList();
            var failsGroupOnly = perStudent.Where(s => s.meets_solo_threshold && !s.meets_group_threshold).ToList();
            var failsBoth = perStudent.Where(s => !s.meets_solo_threshold && !s.meets_group_threshold).ToList();

            return Ok(new
            {
                status = true,
                diagnostics = new
                {
                    programsWithAllChessNumbersCount = programsWithAllChessNumbers.Count,
                    eligibleProgramIdsCount = eligibleProgramIds.Count,
                    sampleEligibleProgramIds = eligibleProgramIds.Take(20),
                    baseStudentsCount = baseStudents.Count,
                    soloParticipationCount = soloParticipation.Count,
                    groupParticipationCount = groupParticipation.Count,
                    perStudent,
                    eligibleStudentsCount = eligibleStudents.Count,
                    eligibleStudents = eligibleStudents,
                    failsSoloOnlyCount = failsSoloOnly.Count,
                    failsGroupOnlyCount = failsGroupOnly.Count,
                    failsBothCount = failsBoth.Count,
                    failsSoloOnly = failsSoloOnly.Take(20),
                    failsGroupOnly = failsGroupOnly.Take(20),
                    failsBoth = failsBoth.Take(20)
                }
            });
        }



        [HttpGet]
        [Route("dashboard_kalathilakam_kalaprathibha_new")]


        //    public async Task<ActionResult> dashboard_kalathilakam_kalaprathibha_new(int ac_year_id)
        //    {
        //        if (_context.tbl_prgm_participants == null)
        //        {
        //            return Ok(new { status = false, message = "Data not found" });
        //        }
        //        var baseUrl = $"{Request.Scheme}://{Request.Host}/";
        //        // Step 0: Filter programs where all participants have a chess_no
        //        var programsWithAllChessNumbers = _context.tbl_prgm_participants
        //            .GroupBy(pp => pp.prgm_id)
        //            .Where(g => g.Any(pp => !string.IsNullOrEmpty(pp.chess_no))
        //            && g.All(pp => pp.ac_year_id == ac_year_id)) // all have chess_no
        //            .Select(g => g.Key)
        //            .ToList();


        //        // Step 1: From those, get only programs that are Published or Transferred to Media
        //        var eligiblePrograms = _context.tbl_program
        //            .Where(p => programsWithAllChessNumbers.Contains(p.id) &&
        //                   (p.status == "Result Published" || p.status == "Transferred to Media") &&
        //                   (p.ac_year_id == ac_year_id))
        //            .Select(p => p.id)
        //            .ToList();
        //        // Step 2: If no eligible programs, return empty result
        //        if (!eligiblePrograms.Any())
        //        {
        //            return Ok(new
        //            {
        //                status = true,
        //                message = "Success",
        //                data = new { kalathilakam = new List<object>(), kalaprathibha = new List<object>() }
        //            });
        //        }



        //        // Step 1: Load solo participation
        //        var soloParticipation = (from pp in _context.tbl_prgm_participants
        //                                 join p in _context.tbl_program on pp.prgm_id equals p.id
        //                                 where p.program_type == "solo" && eligiblePrograms.Contains(p.id)
        //                                 group pp by pp.student_id into g
        //                                 select new
        //                                 {
        //                                     student_id = g.Key,
        //                                     solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
        //                                     solo_points = g.Sum(x => x.average_point ?? 0)
        //                                 }).ToList();

        //        // Step 2: Load group participation counts
        //        var groupParticipation = (from gm in _context.tbl_group_members
        //                                  join p in _context.tbl_program on gm.prgm_id equals p.id
        //                                  where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                                  group gm by gm.student_id into g
        //                                  select new
        //                                  {
        //                                      student_id = g.Key,
        //                                      group_count = g.Select(x => x.prgm_id).Distinct().Count()
        //                                  }).ToList();

        //        // Step 3: Load group points per institute per program (materialize in memory)
        //        var groupPointsByInstitute = (from pp in _context.tbl_prgm_participants
        //                                      join p in _context.tbl_program on pp.prgm_id equals p.id
        //                                      join s in _context.tbl_student on pp.student_id equals s.id
        //                                      where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                                      group pp by new { s.institute, pp.prgm_id } into g
        //                                      select new
        //                                      {
        //                                          institute_id = g.Key.institute,
        //                                          prgm_id = g.Key.prgm_id,
        //                                          group_points = g.Sum(x => x.average_point ?? 0)
        //                                      }).ToList(); // materialize

        //        // Step 4: Assign group points to each student based on their institute (in memory)
        //        var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
        //                                  join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
        //                                  join gpi in groupPointsByInstitute on new { s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, gpi.prgm_id }
        //                                  group gpi by gm.student_id into g
        //                                  select new
        //                                  {
        //                                      student_id = g.Key,
        //                                      group_points = g.Sum(x => x.group_points)
        //                                  }).ToList();


        //        // Step 5: Base student data
        //        var baseStudents = (from s in _context.tbl_student
        //                            join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
        //                            join p in _context.tbl_program on pp.prgm_id equals p.id
        //                            join i in _context.tbl_institute on s.institute equals i.id into instJoin
        //                            from inst in instJoin.DefaultIfEmpty()
        //                            select new
        //                            {
        //                                s.id,
        //                                s.name,
        //                                image = s.image,
        //                                imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
        //                                s.admsn_no,
        //                                institute_name = inst.name,
        //                                s.gender,
        //                                p.participant_type
        //                            }).ToList();

        //        // Step 6: Merge participation + points
        //        var qualifiedStudents = (from bs in baseStudents
        //                                 join sp in soloParticipation on bs.id equals sp.student_id into spJoin
        //                                 from sp in spJoin.DefaultIfEmpty()
        //                                 join gp in studentGroupPoints on bs.id equals gp.student_id into gpJoin
        //                                 from gp in gpJoin.DefaultIfEmpty()
        //                                 join gm in groupParticipation on bs.id equals gm.student_id into gmJoin
        //                                 from gm in gmJoin.DefaultIfEmpty()
        //                                 select new
        //                                 {
        //                                     student_id = bs.id,
        //                                     student_name = bs.name,
        //                                     image = bs.image,
        //                                     imageurl = bs.imageurl,
        //                                     q_id = bs.admsn_no,
        //                                     institute_name = bs.institute_name,
        //                                     gender = bs.gender,
        //                                     participant_type = bs.participant_type,
        //                                     total_points = (sp?.solo_points ?? 0),
        //                                     solo_count = sp?.solo_count ?? 0,
        //                                     group_count = gm?.group_count ?? 0,
        //                                     total_programs = (sp?.solo_count ?? 0) + (gm?.group_count ?? 0) // ✅ New addition

        //                                 }).ToList();

        //        //   Step 7: Filter eligible students
        //        var eligibleStudents = qualifiedStudents
        //   .Where(s => s.solo_count >= 3 && s.group_count >= 2)
        //   .ToList();

        //        var studentsForRanking = eligibleStudents.Any()
        //            ? eligibleStudents
        //            : qualifiedStudents;

        //        // Step 8: Assign Kalathilakam/Kalaprathibha
        //        var studentData = qualifiedStudents
        //            .GroupBy(x => new
        //            {
        //                x.student_id,
        //                x.participant_type
        //            })
        //            .Select(g => new
        //            {
        //                student_id = g.Key.student_id,
        //                student_name = g.First().student_name,
        //                image = g.First().image,
        //                imageurl = g.First().imageurl,
        //                q_id = g.First().q_id,
        //                institute_name = g.First().institute_name,
        //                participant_type = g.Key.participant_type,
        //                total_points = g.First().total_points,

        //                total_programs = $"{g.First().total_programs} ({g.First().solo_count} Solo, {g.First().group_count} Group)",

        //                category = g.First().gender.ToLower() == "girl"
        //                    ? "Kalathilakam"
        //                    : "Kalaprathibha"
        //            })
        //            .ToList();

        //        // Top 10 Girls in each category
        //        var kalathilakamList = studentData
        //.Where(x => x.category == "Kalathilakam")
        //.GroupBy(x => x.participant_type)
        //.Select(g => new
        //{
        //    participant_type = g.Key,
        //    students = g
        //.OrderByDescending(x => x.total_programs.Contains("(3 Solo, 2 Group)"))
        //.ThenByDescending(x => x.total_programs.Contains("(3 Solo, 1 Group)"))
        //.ThenByDescending(x => x.total_points)
        //.Take(10)
        //                .Select(s => new
        //                {
        //                    s.student_id,
        //                    s.student_name,
        //                    s.image,
        //                    s.imageurl,
        //                    s.q_id,
        //                    institution = s.institute_name,
        //                    points = Math.Round(s.total_points, 2),
        //                    s.total_programs
        //                })
        //                .ToList()
        //})
        //.OrderBy(x => x.participant_type)
        //.ToList();
        //        // Top 10 Boys in each category
        //        var kalaprathibhaList = studentData
        // .Where(x => x.category == "Kalaprathibha")
        // .GroupBy(x => x.participant_type)
        // .Select(g => new
        // {
        //     participant_type = g.Key,
        //     students = g
        //.OrderByDescending(x => x.total_programs.Contains("(3 Solo, 2 Group)"))
        //.ThenByDescending(x => x.total_programs.Contains("(3 Solo, 1 Group)"))
        //.ThenByDescending(x => x.total_points)
        //.Take(10)
        //                 .Select(s => new
        //                 {
        //                     s.student_id,
        //                     s.student_name,
        //                     s.image,
        //                     s.imageurl,
        //                     s.q_id,
        //                     institution = s.institute_name,
        //                     points = Math.Round(s.total_points, 2),
        //                     s.total_programs
        //                 })
        //                 .ToList()
        // })
        // .OrderBy(x => x.participant_type)
        // .ToList();

        //        // Step 10: Final JSON response
        //        return Ok(new
        //        {
        //            status = true,
        //            message = "Success",
        //            data = new
        //            {
        //                kalathilakam = kalathilakamList,
        //                kalaprathibha = kalaprathibhaList
        //            }
        //        });
        //    }


        public async Task<ActionResult> dashboard_kalathilakam_kalaprathibha_new(int ac_year_id)
        {
            if (_context.tbl_prgm_participants == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";
            // Step 0: Filter programs where all participants have a chess_no
            var programsWithAllChessNumbers = _context.tbl_prgm_participants
                .GroupBy(pp => pp.prgm_id)
                .Where(g => g.Any(pp => !string.IsNullOrEmpty(pp.chess_no))
                && g.All(pp => pp.ac_year_id == ac_year_id)) // all have chess_no
                .Select(g => g.Key)
                .ToList();


            // Step 1: From those, get only programs that are Published or Transferred to Media
            var eligiblePrograms = _context.tbl_program
                 .Where(p => programsWithAllChessNumbers.Contains(p.id) &&
                        (p.status == "Result Published" || p.status == "Transferred to Media") && p.offstage_onstage == "Onstage" &&
                        (p.ac_year_id == ac_year_id))
                 .Select(p => p.id)
                 .ToList();
            // Step 2: If no eligible programs, return empty result
            if (!eligiblePrograms.Any())
            {
                return Ok(new
                {
                    status = true,
                    message = "Success",
                    data = new { kalathilakam = new List<object>(), kalaprathibha = new List<object>() }
                });
            }



            // Step 1: Load solo participation
            var soloParticipation = (from pp in _context.tbl_prgm_participants
                                     join p in _context.tbl_program on pp.prgm_id equals p.id
                                     where p.program_type == "solo" && eligiblePrograms.Contains(p.id)
                                     group pp by pp.student_id into g
                                     select new
                                     {
                                         student_id = g.Key,
                                         solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
                                         solo_points = g.Sum(x => x.average_point ?? 0)
                                     }).ToList();

            // Step 2: Load group participation counts
            var groupParticipation = (from gm in _context.tbl_group_members
                                      join p in _context.tbl_program on gm.prgm_id equals p.id
                                      where p.program_type == "group" && eligiblePrograms.Contains(p.id)
                                      group gm by gm.student_id into g
                                      select new
                                      {
                                          student_id = g.Key,
                                          group_count = g.Select(x => x.prgm_id).Distinct().Count()
                                      }).ToList();

            // Step 3: Load group points per institute per program (materialize in memory)
            var groupPointsByInstitute = (from pp in _context.tbl_prgm_participants
                                          join p in _context.tbl_program on pp.prgm_id equals p.id
                                          join s in _context.tbl_student on pp.student_id equals s.id
                                          where p.program_type == "group" && eligiblePrograms.Contains(p.id)
                                          group pp by new { s.institute, pp.prgm_id } into g
                                          select new
                                          {
                                              institute_id = g.Key.institute,
                                              prgm_id = g.Key.prgm_id,
                                              group_points = g.Sum(x => x.average_point ?? 0)
                                          }).ToList(); // materialize

            // Step 4: Assign group points to each student based on their institute (in memory)
            var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
                                      join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
                                      join gpi in groupPointsByInstitute on new { s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, gpi.prgm_id }
                                      group gpi by gm.student_id into g
                                      select new
                                      {
                                          student_id = g.Key,
                                          group_points = g.Sum(x => x.group_points)
                                      }).ToList();


            // Step 5: Base student data
            var baseStudents = (from s in _context.tbl_student
                                join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
                                join p in _context.tbl_program on pp.prgm_id equals p.id
                                join i in _context.tbl_institute on s.institute equals i.id into instJoin
                                from inst in instJoin.DefaultIfEmpty()
                                where eligiblePrograms.Contains(p.id)   // <-- add this
                                select new
                                {
                                    s.id,
                                    s.name,
                                    image = s.image,
                                    imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
                                    s.admsn_no,
                                    institute_name = inst.name,
                                    s.gender,
                                    p.participant_type
                                }).ToList();

            // Step 6: Merge participation + points
            var qualifiedStudents = (from bs in baseStudents
                                     join sp in soloParticipation on bs.id equals sp.student_id into spJoin
                                     from sp in spJoin.DefaultIfEmpty()
                                     join gp in studentGroupPoints on bs.id equals gp.student_id into gpJoin
                                     from gp in gpJoin.DefaultIfEmpty()
                                     join gm in groupParticipation on bs.id equals gm.student_id into gmJoin
                                     from gm in gmJoin.DefaultIfEmpty()
                                     select new
                                     {
                                         student_id = bs.id,
                                         student_name = bs.name,
                                         image = bs.image,
                                         imageurl = bs.imageurl,
                                         q_id = bs.admsn_no,
                                         institute_name = bs.institute_name,
                                         gender = bs.gender,
                                         participant_type = bs.participant_type,
                                         total_points = (sp?.solo_points ?? 0),
                                         solo_count = sp?.solo_count ?? 0,
                                         group_count = gm?.group_count ?? 0,
                                         total_programs = (sp?.solo_count ?? 0) + (gm?.group_count ?? 0) // ✅ New addition

                                     }).ToList();

            //   Step 7: Filter eligible students
            var eligibleStudents = qualifiedStudents
       .Where(s => s.solo_count >= 3 && s.group_count >= 2)
       .ToList();

            var studentsForRanking = eligibleStudents.Any()
                ? eligibleStudents
                : qualifiedStudents;

            // Step 8: Assign Kalathilakam/Kalaprathibha
            var studentData = qualifiedStudents
                .GroupBy(x => new
                {
                    x.student_id,
                    x.participant_type
                })
                .Select(g => new
                {
                    student_id = g.Key.student_id,
                    student_name = g.First().student_name,
                    image = g.First().image,
                    imageurl = g.First().imageurl,
                    q_id = g.First().q_id,
                    institute_name = g.First().institute_name,
                    participant_type = g.Key.participant_type,
                    total_points = g.First().total_points,

                    total_programs = $"{g.First().total_programs} ({g.First().solo_count} Solo, {g.First().group_count} Group)",

                    category = g.First().gender.ToLower() == "girl"
                        ? "Kalathilakam"
                        : "Kalaprathibha"
                })
                .ToList();

            // Top 10 Girls in each category
            var kalathilakamList = studentData
    .Where(x => x.category == "Kalathilakam")
    .GroupBy(x => x.participant_type)
    .Select(g => new
    {
        participant_type = g.Key,
        students = g.OrderByDescending(x => x.total_points)
                    .Take(10)
                    .Select(s => new
                    {
                        s.student_id,
                        s.student_name,
                        s.image,
                        s.imageurl,
                        s.q_id,
                        institution = s.institute_name,
                        points = Math.Round(s.total_points, 2),
                        s.total_programs
                    })
                    .ToList()
    })
    .OrderBy(x => x.participant_type)
    .ToList();
            // Top 10 Boys in each category
            var kalaprathibhaList = studentData
     .Where(x => x.category == "Kalaprathibha")
     .GroupBy(x => x.participant_type)
     .Select(g => new
     {
         participant_type = g.Key,
         students = g.OrderByDescending(x => x.total_points)
                     .Take(10)
                     .Select(s => new
                     {
                         s.student_id,
                         s.student_name,
                         s.image,
                         s.imageurl,
                         s.q_id,
                         institution = s.institute_name,
                         points = Math.Round(s.total_points, 2),
                         s.total_programs
                     })
                     .ToList()
     })
     .OrderBy(x => x.participant_type)
     .ToList();

            // Step 10: Final JSON response
            return Ok(new
            {
                status = true,
                message = "Success",
                data = new
                {
                    kalathilakam = kalathilakamList,
                    kalaprathibha = kalaprathibhaList
                }
            });
        }
        [HttpGet]
        [Route("get_program_count")]
        public IActionResult GetProgramCount(string status, int ac_year_id)
        {
             var details = _context.tbl_program
                    .Where(x => x.status == status && x.ac_year_id == ac_year_id)
                    .Select(x => new
                    {
                        x.id,
                        x.program_name,
                        x.item_code, 
                        x.program_type,
                        x.participant_type,
                        x.status
                    })
                    .ToList();
            if (details != null)
            {
                return Ok(new
                {
                    status = true,
                    message = "Success",
                    program_status = status,
                    count = details.Count,
                    details = details
                });
            }

            else
            {
                return Ok(new
                {
                    status = false,
                    message = "No data found!"
                });
            }
        }

        // [HttpGet]
        // [Route("dashboard_kalathilakam_kalaprathibha_TEST")]
        // public async Task<ActionResult> dashboard_kalathilakam_kalaprathibha_TEST(int ac_year_id)
        // {
        //     if (_context.tbl_prgm_participants == null)
        //     {
        //         return Ok(new { status = false, message = "Data not found" });
        //     }
        //     var baseUrl = $"{Request.Scheme}://{Request.Host}/";
        //     // Step 0: Filter programs where all participants have a chess_no
        //     var programsWithAllChessNumbers = _context.tbl_prgm_participants
        //         .GroupBy(pp => pp.prgm_id)
        //         .Where(g => g.Any(pp => !string.IsNullOrEmpty(pp.chess_no))
        //         && g.All(pp => pp.ac_year_id == ac_year_id)) // all have chess_no
        //         .Select(g => g.Key)
        //         .ToList();


        //     // Step 1: From those, get only programs that are Published or Transferred to Media
        //     var eligiblePrograms = _context.tbl_program
        //         .Where(p => programsWithAllChessNumbers.Contains(p.id) &&
        //                (p.status == "Result Published" || p.status == "Transferred to Media") &&
        //                (p.ac_year_id == ac_year_id))
        //         .Select(p => p.id)
        //         .ToList();
        //     // Step 2: If no eligible programs, return empty result
        //     if (!eligiblePrograms.Any())
        //     {
        //         return Ok(new
        //         {
        //             status = true,
        //             message = "Success",
        //             data = new { kalathilakam = new List<object>(), kalaprathibha = new List<object>() }
        //         });
        //     }



        //     // Step 1: Load solo participation
        //     var soloParticipation = (from pp in _context.tbl_prgm_participants
        //                              join p in _context.tbl_program on pp.prgm_id equals p.id
        //                              where p.program_type == "solo" && eligiblePrograms.Contains(p.id)
        //                              group pp by pp.student_id into g
        //                              select new
        //                              {
        //                                  student_id = g.Key,
        //                                  solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
        //                                  solo_points = g.Sum(x => x.average_point ?? 0)
        //                              }).ToList();

        //     // Step 2: Load group participation counts
        //     var groupParticipation = (from gm in _context.tbl_group_members
        //                               join p in _context.tbl_program on gm.prgm_id equals p.id
        //                               where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                               group gm by gm.student_id into g
        //                               select new
        //                               {
        //                                   student_id = g.Key,
        //                                   group_count = g.Select(x => x.prgm_id).Distinct().Count()
        //                               }).ToList();

        //     // Step 3: Load group points per institute per program (materialize in memory)
        //     var groupPointsByInstitute = (from pp in _context.tbl_prgm_participants
        //                                   join p in _context.tbl_program on pp.prgm_id equals p.id
        //                                   join s in _context.tbl_student on pp.student_id equals s.id
        //                                   where p.program_type == "group" && eligiblePrograms.Contains(p.id)
        //                                   group pp by new { s.institute, pp.prgm_id } into g
        //                                   select new
        //                                   {
        //                                       institute_id = g.Key.institute,
        //                                       prgm_id = g.Key.prgm_id,
        //                                       group_points = g.Sum(x => x.average_point ?? 0)
        //                                   }).ToList(); // materialize

        //     // Step 4: Assign group points to each student based on their institute (in memory)
        //     var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
        //                               join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
        //                               join gpi in groupPointsByInstitute on new { s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, gpi.prgm_id }
        //                               group gpi by gm.student_id into g
        //                               select new
        //                               {
        //                                   student_id = g.Key,
        //                                   group_points = g.Sum(x => x.group_points)
        //                               }).ToList();


        //     // Step 5: Base student data
        //     var baseStudents = (from s in _context.tbl_student
        //                         join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
        //                         join p in _context.tbl_program on pp.prgm_id equals p.id
        //                         join i in _context.tbl_institute on s.institute equals i.id into instJoin
        //                         from inst in instJoin.DefaultIfEmpty()
        //                         select new
        //                         {
        //                             s.id,
        //                             s.name,
        //                             image = s.image,
        //                             imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
        //                             s.admsn_no,
        //                             institute_name = inst.name,
        //                             s.gender,
        //                             p.participant_type
        //                         }).ToList();

        //     // Step 6: Merge participation + points
        //     var qualifiedStudents = (from bs in baseStudents
        //                              join sp in soloParticipation on bs.id equals sp.student_id into spJoin
        //                              from sp in spJoin.DefaultIfEmpty()
        //                              join gp in studentGroupPoints on bs.id equals gp.student_id into gpJoin
        //                              from gp in gpJoin.DefaultIfEmpty()
        //                              join gm in groupParticipation on bs.id equals gm.student_id into gmJoin
        //                              from gm in gmJoin.DefaultIfEmpty()
        //                              select new
        //                              {
        //                                  student_id = bs.id,
        //                                  student_name = bs.name,
        //                                  image = bs.image,
        //                                  imageurl = bs.imageurl,
        //                                  q_id = bs.admsn_no,
        //                                  institute_name = bs.institute_name,
        //                                  gender = bs.gender,
        //                                  participant_type = bs.participant_type,
        //                                  total_points = (sp?.solo_points ?? 0),
        //                                  solo_count = sp?.solo_count ?? 0,
        //                                  group_count = gm?.group_count ?? 0,
        //                                  total_programs = (sp?.solo_count ?? 0) + (gm?.group_count ?? 0) // ✅ New addition

        //                              }).ToList();

        //     //   Step 7: Filter eligible students
        //     var eligibleStudents = qualifiedStudents
        //.Where(s => s.solo_count >= 3 && s.group_count >= 2)
        //.ToList();

        //     var studentsForRanking = eligibleStudents.Any()
        //         ? eligibleStudents
        //         : qualifiedStudents;

        //     // Step 8: Assign Kalathilakam/Kalaprathibha
        //     var studentData = qualifiedStudents
        //         .GroupBy(x => new
        //         {
        //             x.student_id,
        //             x.participant_type
        //         })
        //         .Select(g => new
        //         {
        //             student_id = g.Key.student_id,
        //             student_name = g.First().student_name,
        //             image = g.First().image,
        //             imageurl = g.First().imageurl,
        //             q_id = g.First().q_id,
        //             institute_name = g.First().institute_name,
        //             participant_type = g.Key.participant_type,

        //             total_points = g.First().total_points,

        //             solo_count = g.First().solo_count,
        //             group_count = g.First().group_count,

        //             eligible = g.First().solo_count >= 3 &&
        //                        g.First().group_count >= 2,

        //             total_programs =
        //                 $"{g.First().total_programs} ({g.First().solo_count} Solo, {g.First().group_count} Group)",

        //             category = g.First().gender.ToLower() == "girl"
        //                 ? "Kalathilakam"
        //                 : "Kalaprathibha"
        //         })
        //         .ToList();


        //     // Top 10 Girls in each category
        //     var kalaprathibhaList = studentData
        //   .Where(x => x.category == "Kalaprathibha")
        //   .GroupBy(x => x.participant_type)
        //   .Select(g =>
        //   {
        //       var ranked = g
        //           .Where(x => x.eligible)
        //           .OrderByDescending(x => x.total_points)
        //           .Concat(
        //               g.Where(x => !x.eligible)
        //                .OrderByDescending(x => x.total_points)
        //           )
        //           .Take(10)
        //           .Select(s => new
        //           {
        //               s.student_id,
        //               s.student_name,
        //               s.image,
        //               s.imageurl,
        //               s.q_id,
        //               institution = s.institute_name,
        //               points = Math.Round(s.total_points, 2),
        //               s.total_programs
        //           })
        //           .ToList();

        //       return new
        //       {
        //           participant_type = g.Key,
        //           students = ranked
        //       };
        //   })
        //   .OrderBy(x => x.participant_type)
        //   .ToList();
        //     // Top 10 Boys in each category
        //     var kalathilakamList = studentData
        //         .Where(x => x.category == "Kalathilakam")
        //         .GroupBy(x => x.participant_type)
        //         .Select(g =>
        //         {
        //             var ranked = g
        //                 .Where(x => x.eligible)
        //                 .OrderByDescending(x => x.total_points)
        //                 .Concat(
        //                     g.Where(x => !x.eligible)
        //                      .OrderByDescending(x => x.total_points)
        //                 )
        //                 .Take(10)
        //                 .Select(s => new
        //                 {
        //                     s.student_id,
        //                     s.student_name,
        //                     s.image,
        //                     s.imageurl,
        //                     s.q_id,
        //                     institution = s.institute_name,
        //                     points = Math.Round(s.total_points, 2),
        //                     s.total_programs
        //                 })
        //                 .ToList();

        //             return new
        //             {
        //                 participant_type = g.Key,
        //                 students = ranked
        //             };
        //         })
        //         .OrderBy(x => x.participant_type)
        //         .ToList();
        //     // Step 10: Final JSON response
        //     return Ok(new
        //     {
        //         status = true,
        //         message = "Success",
        //         data = new
        //         {
        //             kalathilakam = kalathilakamList,
        //             kalaprathibha = kalaprathibhaList
        //         }
        //     });
        // }


   


        [HttpGet]
        [Route("dashboard_kalathilakam_kalaprathibha_TEST")]
        public async Task<ActionResult> dashboard_kalathilakam_kalaprathibha_TEST(int ac_year_id)
        {
            if (_context.tbl_prgm_participants == null)
            {
                return Ok(new { status = false, message = "Data not found" });
            }
            var baseUrl = $"{Request.Scheme}://{Request.Host}/";

            // Step 0: Filter programs where all participants have a chess_no
            var programsWithAllChessNumbers = _context.tbl_prgm_participants
                .GroupBy(pp => pp.prgm_id)
                .Where(g => g.Any(pp => !string.IsNullOrEmpty(pp.chess_no))
                && g.All(pp => pp.ac_year_id == ac_year_id))
                .Select(g => g.Key)
                .ToList();

            // Step 1: From those, get only programs that are Published or Transferred to Media
            var eligiblePrograms = _context.tbl_program
                .Where(p => programsWithAllChessNumbers.Contains(p.id) &&
                       (p.status == "Result Published" || p.status == "Transferred to Media") &&
                       (p.ac_year_id == ac_year_id))
                .Select(p => p.id)
                .ToList();

            if (!eligiblePrograms.Any())
            {
                return Ok(new
                {
                    status = true,
                    message = "Success",
                    data = new { kalathilakam = new List<object>(), kalaprathibha = new List<object>() }
                });
            }

            // Step 1: Load solo participation
            var soloParticipation = (from pp in _context.tbl_prgm_participants
                                     join p in _context.tbl_program on pp.prgm_id equals p.id
                                     where p.program_type == "solo" && eligiblePrograms.Contains(p.id)
                                     group pp by pp.student_id into g
                                     select new
                                     {
                                         student_id = g.Key,
                                         solo_count = g.Select(x => x.prgm_id).Distinct().Count(),
                                         solo_points = g.Sum(x => x.average_point ?? 0)
                                     }).ToList();

            // Step 2: Load group participation counts
            var groupParticipation = (from gm in _context.tbl_group_members
                                      join p in _context.tbl_program on gm.prgm_id equals p.id
                                      where p.program_type == "group" && eligiblePrograms.Contains(p.id)
                                      group gm by gm.student_id into g
                                      select new
                                      {
                                          student_id = g.Key,
                                          group_count = g.Select(x => x.prgm_id).Distinct().Count()
                                      }).ToList();

            // Step 3: Load group points per institute per program
            var groupPointsByInstitute = (from pp in _context.tbl_prgm_participants
                                          join p in _context.tbl_program on pp.prgm_id equals p.id
                                          join s in _context.tbl_student on pp.student_id equals s.id
                                          where p.program_type == "group" && eligiblePrograms.Contains(p.id)
                                          group pp by new { s.institute, pp.prgm_id } into g
                                          select new
                                          {
                                              institute_id = g.Key.institute,
                                              prgm_id = g.Key.prgm_id,
                                              group_points = g.Sum(x => x.average_point ?? 0)
                                          }).ToList();

            // Step 4: Assign group points to each student based on their institute
            var studentGroupPoints = (from gm in _context.tbl_group_members.AsEnumerable()
                                      join s in _context.tbl_student.AsEnumerable() on gm.student_id equals s.id
                                      join gpi in groupPointsByInstitute on new { s.institute, gm.prgm_id } equals new { institute = gpi.institute_id, gpi.prgm_id }
                                      group gpi by gm.student_id into g
                                      select new
                                      {
                                          student_id = g.Key,
                                          group_points = g.Sum(x => x.group_points)
                                      }).ToList();

            // Step 5: Base student data
            var baseStudents = (from s in _context.tbl_student
                                join pp in _context.tbl_prgm_participants on s.id equals pp.student_id
                                join p in _context.tbl_program on pp.prgm_id equals p.id
                                join i in _context.tbl_institute on s.institute equals i.id into instJoin
                                from inst in instJoin.DefaultIfEmpty()
                                select new
                                {
                                    s.id,
                                    s.name,
                                    image = s.image,
                                    imageurl = !string.IsNullOrEmpty(s.image) ? $"{baseUrl}uploads/student/{s.image}" : null,
                                    s.admsn_no,
                                    institute_name = inst.name,
                                    s.gender,
                                    p.participant_type
                                }).ToList();

            // Step 6: Merge participation + points
            var qualifiedStudents = (from bs in baseStudents
                                     join sp in soloParticipation on bs.id equals sp.student_id into spJoin
                                     from sp in spJoin.DefaultIfEmpty()
                                     join gp in studentGroupPoints on bs.id equals gp.student_id into gpJoin
                                     from gp in gpJoin.DefaultIfEmpty()
                                     join gm in groupParticipation on bs.id equals gm.student_id into gmJoin
                                     from gm in gmJoin.DefaultIfEmpty()
                                     select new
                                     {
                                         student_id = bs.id,
                                         student_name = bs.name,
                                         image = bs.image,
                                         imageurl = bs.imageurl,
                                         q_id = bs.admsn_no,
                                         institute_name = bs.institute_name,
                                         gender = bs.gender,
                                         participant_type = bs.participant_type,
                                         total_points = (sp?.solo_points ?? 0) + (gp?.group_points ?? 0),
                                         solo_count = sp?.solo_count ?? 0,
                                         group_count = gm?.group_count ?? 0,
                                         total_programs = (sp?.solo_count ?? 0) + (gm?.group_count ?? 0)
                                     }).ToList();

            // Step 7: Group by student + participant_type, compute tier
            var studentData = qualifiedStudents
                .GroupBy(x => new { x.student_id, x.participant_type })
                .Select(g =>
                {
                    var first = g.First();
                    return new
                    {
                        student_id = g.Key.student_id,
                        student_name = first.student_name,
                        image = first.image,
                        imageurl = first.imageurl,
                        q_id = first.q_id,
                        institute_name = first.institute_name,
                        participant_type = g.Key.participant_type,
                        total_points = first.total_points,
                        solo_count = first.solo_count,
                        group_count = first.group_count,
                        tier = GetTier(first.solo_count, first.group_count),
                        total_programs = $"{first.total_programs} ({first.solo_count} Solo, {first.group_count} Group)",
                        category = first.gender.ToLower() == "girl" ? "Kalathilakam" : "Kalaprathibha"
                    };
                })
                .ToList();

            // Top 10 Girls in each category (Kalaprathibha)
            var kalaprathibhaList = studentData
                .Where(x => x.category == "Kalaprathibha")
                .GroupBy(x => x.participant_type)
                .Select(g => new
                {
                    participant_type = g.Key,
                    students = g
                        .OrderBy(x => x.tier)
                        .ThenByDescending(x => x.total_points)
                        .ThenByDescending(x => x.group_count) // tie-breaker (group position/participation)
                        .Take(10)
                        .Select(s => new
                        {
                            s.student_id,
                            s.student_name,
                            s.image,
                            s.imageurl,
                            s.q_id,
                            institution = s.institute_name,
                            points = Math.Round(s.total_points, 2),
                            s.total_programs
                        })
                        .ToList()
                })
                .OrderBy(x => x.participant_type)
                .ToList();

            // Top 10 Boys in each category (Kalathilakam)
            var kalathilakamList = studentData
                .Where(x => x.category == "Kalathilakam")
                .GroupBy(x => x.participant_type)
                .Select(g => new
                {
                    participant_type = g.Key,
                    students = g
                        .OrderBy(x => x.tier)
                        .ThenByDescending(x => x.total_points)
                        .ThenByDescending(x => x.group_count) // tie-breaker (group position/participation)
                        .Take(10)
                        .Select(s => new
                        {
                            s.student_id,
                            s.student_name,
                            s.image,
                            s.imageurl,
                            s.q_id,
                            institution = s.institute_name,
                            points = Math.Round(s.total_points, 2),
                            s.total_programs
                        })
                        .ToList()
                })
                .OrderBy(x => x.participant_type)
                .ToList();

            return Ok(new
            {
                status = true,
                message = "Success",
                data = new
                {
                    kalathilakam = kalathilakamList,
                    kalaprathibha = kalaprathibhaList
                }
            });
        }

        // Priority tier: lower number = higher priority.
        // Relaxes requirements step-by-step: 3S+2G -> 3S+1G -> 3S+0G -> 2S+1G -> 2S+0G -> 1S+1G -> 1S+0G -> 0S+anyG -> everything else
        private static int GetTier(int solo, int group)
        {
            if (solo >= 3 && group >= 2) return 1;
            if (solo >= 3 && group == 1) return 2;
            if (solo >= 3 && group == 0) return 3;
            if (solo == 2 && group >= 2) return 4;
            if (solo == 2 && group >= 1) return 5;
            if (solo == 2 && group == 0) return 6;
            if (solo == 1 && group >= 1) return 7;
            if (solo == 1 && group == 0) return 8;
            if (solo == 0 && group >= 1) return 9;
            return 9;
        }

    }
}
