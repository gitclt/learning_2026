using System.Data;
using System.Net;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class loginController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public loginController(kalanjaliDbContext context)
        {
            _context = context;
        }
        [HttpPost]
        [Route("generate_tocken")]
        public async Task<string> generate_tocken()
        {
            // Generate a new GUID
            Guid enc_key = Guid.NewGuid();

            // Format the GUID and add the prefix
            string tokenString = "tocken" + enc_key.ToString().Replace("-", "").Substring(0, 10);

            // Check if the generated token exists in the database
            bool exists = await _context.tbl_user.AnyAsync(u => u.enc_key == tokenString);

            // If a duplicate is found, recursively generate a new token
            if (exists)
            {
                return await generate_tocken();
            }

            // Return the unique token
            return tokenString;
        }


        [HttpPost]

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromForm] string? data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return BadRequest(new
                {
                    status = false,
                    message = "No data provided"
                });
            }

            string decryptedData = _context.base64Decode(data);

            var parameters = decryptedData.Split('&');

            string username = parameters
                .FirstOrDefault(p => p.StartsWith("username="))
                ?.Split('=')[1];

            string password = parameters
                .FirstOrDefault(p => p.StartsWith("password="))
                ?.Split('=')[1];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return BadRequest(new
                {
                    status = false,
                    message = "Username or Password not provided"
                });
            }

            var user = await _context.tbl_user
                .Where(u =>
                    u.username == username &&
                    u.password == password &&
                    u.status == "active")
                .FirstOrDefaultAsync();

            if (user != null)
            {
                if (string.IsNullOrEmpty(user.enc_key))
                {
                    user.enc_key = await generate_tocken();
                    user.enc_key_date = DateTime.UtcNow;
                }
                else
                {
                    DateTime currentDate = DateTime.UtcNow.Date;

                    if (user.enc_key_date?.Date != currentDate)
                    {
                        user.enc_key = await generate_tocken();
                        user.enc_key_date = currentDate;
                    }
                }

                // Get active academic year
                var currentAcademicYear = await _context.tbl_academic_year
                    .Where(a => a.is_active == 1)
                    .FirstOrDefaultAsync();

                if (currentAcademicYear != null)
                {
                    user.logged_ac_year = currentAcademicYear.id;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = true,
                    message = "Successfully logged in",
                    data = new
                    {
                        token = user.enc_key,
                        logged_ac_year = user.logged_ac_year
                    }
                });
            }

            return Ok(new
            {
                status = false,
                message = "Username or Password Mismatch"
            });
        }

        [HttpGet]
        [Route("profile")]
       
        public async Task<ActionResult> profile(string? enc_key, string? fcm)

        {


            var user = await _context.tbl_user.FirstOrDefaultAsync(u => u.enc_key == enc_key);

            if (user == null)
            {
                return Ok(new { status = false, message = "User not found." });
            }
            if (!string.IsNullOrEmpty(fcm))
            {
                user.fcm = fcm;
                await _context.SaveChangesAsync();
            }

            object profileData = null;

            switch (user.type?.ToLower())
            {
                case "student":
                    profileData = (from u in _context.tbl_user
                                   join s in _context.tbl_student on u.reference_id equals s.id
                                   join c in _context.tbl_class on s.class_id equals c.id into cla

                                   from cl in cla.DefaultIfEmpty()
                                   join d in _context.tbl_division on s.division_id equals d.id into div
                                   from di in div.DefaultIfEmpty()

                                   join i in _context.tbl_institute on s.institute equals i.id into inst
                                   from ins in inst.DefaultIfEmpty()

                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()

                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.reference_id,
                                       u.stage_id,
                                       sta.stage_name,
                                       name = s.name,
                                       phone = s.phone_no,
                                       type = u.type,
                                       institute_id = s.institute,
                                       institute_name = ins.name,
                                       address = ins.address,
                                       admsn_no = s.admsn_no,
                                       class_id = s.class_id,
                                       division_id = s.division_id,
                                       division = di.division,
                                       class_name = cl != null ? cl.@class : null,
                                       u.logged_ac_year
                                   }).FirstOrDefault();
                    break;

                case "judge":
                    profileData = (from u in _context.tbl_user
                                   join j in _context.tbl_judge on u.reference_id equals j.id
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.reference_id,
                                       u.stage_id,
                                       sta.stage_name,
                                       name = j.judge_name,
                                       phone = j.phone_no,
                                       type = u.type,
                                       event_id = j.event_id,
                                       u.logged_ac_year
                                   }).FirstOrDefault();
                    break;

                case "institute":
                    profileData = (from u in _context.tbl_user
                                   join i in _context.tbl_institute on u.reference_id equals i.id
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       institute_id = u.reference_id,
                                       u.stage_id,
                                       sta.stage_name,
                                       name = i.name,
                                       phone = i.phone_no,
                                       username = i.username,
                                       address = i.address,
                                       contact_person = i.contact_person,
                                       type = u.type,
                                       u.logged_ac_year
                                   }).FirstOrDefault();
                    break;
                case "admin":
                    profileData = (from u in _context.tbl_user
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,
                                       u.logged_ac_year
                                   }).FirstOrDefault();
                    break;

                case "superadmin":
                    profileData = (from u in _context.tbl_user
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,
                                       u.logged_ac_year
                                   }).FirstOrDefault();
                    break;

                case "verification_counter":
                    profileData = (from u in _context.tbl_user
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       u.stage_id,
                                       sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;
                case "stagefront_manager":
                    profileData = (from u in _context.tbl_user
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       u.stage_id,
                                       sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;
                case "backstage":
                    profileData = (from u in _context.tbl_user
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       u.stage_id,
                                       sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;
                case "tabulation_team":
                    profileData = (from u in _context.tbl_user
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       //u.stage_id,
                                       //sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;

                //tabulation manager
                case "tabulation_manager":
                    profileData = (from u in _context.tbl_user
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       //u.stage_id,
                                       //sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;
                //
                //chief program manager
                case "chief_program_manager":
                    profileData = (from u in _context.tbl_user
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       //u.stage_id,
                                       //sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;
                //

                case "announcement_team":
                    profileData = (from u in _context.tbl_user
                                   join st in _context.tbl_stage on u.stage_id equals st.id into stg
                                   from sta in stg.DefaultIfEmpty()
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       //u.stage_id,
                                       //sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;

                case "editing_team":
                    profileData = (from u in _context.tbl_user
                                
                                   where u.enc_key == enc_key
                                   select new
                                   {
                                       id = u.id,
                                       //u.stage_id,
                                       //sta.stage_name,
                                       name = u.name,
                                       phone = u.phone_no,
                                       username=u.username,     
                                       password = u.password,
                                       type = u.type,u.logged_ac_year
                                   }).FirstOrDefault();
                    break;

                default:
                    return Ok(new { status = false, message = "Invalid user type." });
            }

            if (profileData != null)
            {
                return Ok(new { status = true, data = profileData });
            }

            return Ok(new { status = false, message = "Profile data not found." });
        }

        [HttpPost]
        [Route("sign_up")]
        public async Task<ActionResult> sign_up([FromBody] studentmodel request)
        {
            if (_context.tbl_student == null || _context.tbl_user == null)
            {
                return Problem("Entity set '_context.tbl_user' or '_context.tbl_student' is null.");
            }

            //  Check  admission number already exists
            var existingStudent = await _context.tbl_student
                .FirstOrDefaultAsync(s => s.admsn_no == request.admsn_no);

            if (existingStudent != null)
            {
                return Ok(new { status = false, message = "Admission number already exists." });
            }

            var division = new studentmodel
            {
                name = request.name,
                phone_no = request.phone_no,
                password = request.admsn_no,
                class_id = request.class_id,
                division_id = request.division_id,
                admsn_no = request.admsn_no,
                institute = request.institute,
                status = request.status ?? "active",
                added_by = request.added_by,
                addedtype = request.addedtype,
                addedon = DateTime.Now,
            };

            _context.tbl_student.Add(division);
            await _context.SaveChangesAsync();

            // Autogenerate username and password
            //string GenerateRandomSuffix(int length = 4)
            //{
            //    var random = new Random();
            //    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            //    return new string(Enumerable.Repeat(chars, length)
            //        .Select(s => s[random.Next(s.Length)]).ToArray());
            //}

            //var generatedUsername = "KAL" + division.admsn_no;
            //var generatedPassword = division.admsn_no + GenerateRandomSuffix();

            var user = new usermodel
            {
                type = "student",
                name = division.name,
                phone_no = division.phone_no,
                admsn_no = division.admsn_no,
                username = division.admsn_no,
                password = division.admsn_no,
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
                return BadRequest(new { status = false, message = "Failed to insert" });
            }

            return Ok(new { status = true, message = "Data added successfully", id = division.id });
        }


        //change password
        [HttpPost]
        [Route("change_password")]
        public async Task<ActionResult> change_password([FromForm] string? newPassword, [FromForm] string? repeatPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(repeatPassword))
            {
                return Ok(new { status = false, message = "New Password and Repeat Password are Required." });
            }

            var enc_key = Request.Headers["XapiKey"].ToString();

            var user = await _context.tbl_user.FirstOrDefaultAsync(u => u.enc_key == enc_key);
            if (user == null)
            {
                return Ok(new { status = false, message = "User not found" });
            }

            if (newPassword != repeatPassword)
            {
                return Ok(new { status = false, message = "New Password and Repeat Password do not match" });
            }

            // Update password
            user.password = newPassword;

            // Generate new enc_key and update date
           // string newEncKey = Guid.NewGuid().ToString();  // or any custom token logic you prefer
            user.enc_key = await generate_tocken();
            user.enc_key_date = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { status = true, message = "Password Changed Successfully" });

        }


        //

    }
}
