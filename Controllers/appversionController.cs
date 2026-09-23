using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class appversionController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public appversionController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("Getappversionmodel")]
        public async Task<ActionResult<appversionmodel>> Getappversionmodel()
        {
            if (_context.tbl_app_version == null)
            {
                return Ok(new
                {
                    status = false,
                    message = "App version not found.",
                    data = (object?)null
                });
            }

            var latestVersion = await _context.tbl_app_version
                .OrderByDescending(v => v.id)
                .FirstOrDefaultAsync();

            if (latestVersion == null)
            {
                return Ok(new
                {
                    status = true,
                    message = "No data found.",
                    data = (object?)null
                });
            }

            return Ok(new
            {
                status = true,
                message = "Success",
                data = latestVersion
            });
        }

        [HttpPut]
        [Route("update_appversion")]
        public async Task<IActionResult> update_appversion([FromForm] int? id, [FromForm] string? version_name, [FromForm] string? version_code, [FromForm] string? ios_version_code, [FromForm] string? ios_url, [FromForm] string? android_url, [FromForm] string? web_version_code,[FromForm]string? ios_version_name, [FromForm] string? web_version_name)
        {
            if (id == null)
            {
                return BadRequest(new { status = false, message = "ID is required" });
            }

            var order = await _context.tbl_app_version.FindAsync(id);

            if (order == null)
            {
                return NotFound(new { status = false, message = "Record not found" });
            }

            // Update FCM token if provided
            if (!string.IsNullOrEmpty(version_name))
            {
                order.version_name = version_name;
            }
            if (!string.IsNullOrEmpty(version_code))
            {
                order.version_code = version_code;
            }
            if (!string.IsNullOrEmpty(ios_version_code))
            {
                order.ios_version_code = ios_version_code;
            }
            if (!string.IsNullOrEmpty(ios_version_name))
            {
                order.ios_version_name = ios_version_name;
            }
            if (!string.IsNullOrEmpty(web_version_code))
            {
                order.web_version_code = web_version_code;
            }
            if (!string.IsNullOrEmpty(web_version_name))
            {
                order.web_version_name = web_version_name;
            }
            if (!string.IsNullOrEmpty(ios_url))
            {
                order.ios_url = ios_url;
            }
            if (!string.IsNullOrEmpty(android_url))
            {
                order.android_url = android_url;
            }

            _context.tbl_app_version.Update(order);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "Data updated successfully" });
        }

        [HttpDelete]
        [Route("dashboard_kalathilakam_kalaprathibha_new")]
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
                       (p.status == "Result Published" || p.status == "Transferred to Media") &&
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
            var studentData = studentsForRanking
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
                                         total_points = (sp?.solo_points ?? 0) ,
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
            if (solo == 2 && group >= 1) return 4;
            if (solo == 2 && group == 0) return 5;
            if (solo == 1 && group >= 1) return 6;
            if (solo == 1 && group == 0) return 7;
            if (solo == 0 && group >= 1) return 8;
            return 9;
        }
    }
}
