using System.Drawing;
using System.Globalization;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;


namespace kalanjali_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class event_registrationController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;

        public event_registrationController(kalanjaliDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Postevent_registration")]
        public async Task<ActionResult> Postevent_registration([FromBody] event_registration request)
        {
            if (_context.tbl_event_registration == null)
            {
                return Problem("Entity set '_context.tbl_event_registration' is null.");
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


            var division = new event_registration
            {
                name = request.name,

                mobile = request.mobile,
                email = request.email,

                addedon = DateTime.Now,

                status = "active",
               
            };

            _context.tbl_event_registration.Add(division);
            // return Ok(division);
            await _context.SaveChangesAsync();

            return Ok(new { status = true, message = "registered successfully" });
        }


        //excel

        [HttpGet]
        [Route("event_registration_excel")]
        public async Task<IActionResult> event_registration_excel(DateTime? from_date, DateTime? to_date,int?ac_year_id)
        {
            // Base query
            var query = _context.tbl_event_registration.AsQueryable();

            // Apply filters if provided
            if (from_date.HasValue)
                query = query.Where(r => r.addedon >= from_date.Value);

            if (to_date.HasValue)
                query = query.Where(r => r.addedon <= to_date.Value);
            if(ac_year_id.HasValue)
            {
                query= query.Where(r => r.addedon.Value.Year == ac_year_id.Value);
            }
            var regList = await query.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Event Registrations");

                // Handpick the fields you want from the model
                var selectedFields = new List<string>
        {
            "id",
            "name",
            "email",
            "phone",
            "addedon",
            "status"
        };

                var properties = typeof(event_registration).GetProperties()
                                    .Where(p => selectedFields.Contains(p.Name))
                                    .ToList();

                // Set headers (row 1)
                for (int i = 0; i < properties.Count; i++)
                {
                    string formattedHeader = CultureInfo.CurrentCulture.TextInfo
                        .ToTitleCase(properties[i].Name.Replace("_", " ").ToLower());

                    worksheet.Cell(1, i + 1).Value = formattedHeader;
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Fill data rows
                for (int rowIndex = 0; rowIndex < regList.Count; rowIndex++)
                {
                    var rowData = regList[rowIndex];
                    for (int colIndex = 0; colIndex < properties.Count; colIndex++)
                    {
                        var value = properties[colIndex].GetValue(rowData, null) ?? "";
                        worksheet.Cell(rowIndex + 2, colIndex + 1).Value = value.ToString();
                    }
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    stream.Position = 0;
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"event_registrations_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                }
            }
        }

    }

}

