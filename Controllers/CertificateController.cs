using DocumentFormat.OpenXml.ExtendedProperties;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
namespace kalanjali_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CertificateController : ControllerBase
    {
        private readonly kalanjaliDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CertificateController(kalanjaliDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        [Route("certificate--")]
        public IActionResult GenerateCertificate([FromQuery] CertificateModel request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Certificate details are required.");

            string templatePath = Path.Combine(_env.WebRootPath, "templates", "Certificate-template.razor");

            if (!System.IO.File.Exists(templatePath))
                return NotFound($"Template not found at {templatePath}");

            string html = System.IO.File.ReadAllText(templatePath);

            string baseUrl = $"{Request.Scheme}://{Request.Host}";

            html = html.Replace("CSS_URL", $"{baseUrl}/templates")
                       .Replace("IMAGE_BASE_URL", $"{baseUrl}/images")
                       .Replace("{{Name}}", request.Name)
                       .Replace("{{Class}}", request.Class)
                       .Replace("{{School}}", request.School)
                       .Replace("{{ItemCode}}", request.ItemCode)
                       .Replace("{{Item}}", request.Item)
                       .Replace("{{Position}}", request.Position)
                       .Replace("{{Grade}}", request.Grade);

            return Content(html, "text/html");
        }

        [HttpGet("certificate")]
        public IActionResult Generate([FromQuery] CertificateModel req)
        {
            var templatePath = Path.Combine(_env.WebRootPath, "images", "STUDENTSCERTIFICATE.jpeg");
            using var bitmap = SKBitmap.Decode(templatePath);
            using var surface = SKSurface.Create(new SKImageInfo(bitmap.Width, bitmap.Height));
            var canvas = surface.Canvas;
            canvas.DrawBitmap(bitmap, 0, 0);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true
            };
            using var font = new SKFont
            {
                Size = 52,
                Typeface = SKTypeface.FromFamilyName("Times New Roman")
            };

            const float NameX = 730;
            const float NameY = 1660;
            const float ClassX = 2890;
            const float ClassY = 1660;
            const float SchoolX = 740;
            const float SchoolY = 1840;
            const float ItemCodeX = 2890;
            const float ItemCodeY = 1840;
            const float ItemX = 700;
            const float ItemY = 2010;
            const float PositionX = 2300;
            const float PositionY = 2010;
            const float GradeX = 2890;
            const float GradeY = 2010;

            // local helper: null-safe text draw
            void SafeDrawText(string? text, float x, float y)
                => canvas.DrawText(text ?? string.Empty, x, y, font, paint);

            SafeDrawText(req.Name?.ToUpperInvariant(), NameX, NameY);
            SafeDrawText(req.Class, ClassX, ClassY);
            SafeDrawText(req.School, SchoolX, SchoolY);
            SafeDrawText(req.ItemCode, ItemCodeX, ItemCodeY);
            SafeDrawText(req.Item, ItemX, ItemY);
            SafeDrawText(req.Position, PositionX, PositionY);
            SafeDrawText(req.Grade, GradeX, GradeY);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return File(data.ToArray(), "image/png");
        }
     
        [HttpGet("GenerateForProgram")]
        public async Task<IActionResult> GenerateForProgram(int program_id)
        {
            if (program_id <= 0)
            {
                return NotFound("No Program id found");
            }

            // Get program
            var program = await _context.tbl_program
                .FirstOrDefaultAsync(p => p.id == program_id);

            if (program == null)
            {
                return NotFound("Program not found.");
            }

            var soloStudents = await (
                from pp in _context.tbl_prgm_participants

                join s in _context.tbl_student
                    on pp.student_id equals s.id

                join i in _context.tbl_institute
                    on s.institute equals i.id into inst
                from institute in inst.DefaultIfEmpty()

                join p in _context.tbl_program
                    on pp.prgm_id equals p.id

                where pp.prgm_id == program_id
                && (
                    pp.position == "1"
                    || pp.position == "2"
                    || pp.position == "3"
                )
                orderby pp.average_point descending

                select new CertificateModel
                {
                    Name = s.name ?? "",
                    Class = s.category ?? "",
                    School = institute != null ? institute.name : "",
                    ItemCode = p.item_code ?? "",
                    Item = p.program_name ?? "",

                    Position = pp.position == "1" ? "First" :
                               pp.position == "2" ? "Second" :
                               pp.position == "3" ? "Third" : "",

                    Grade = pp.grade ?? ""
                }
            ).ToListAsync();



            var groupStudents = await (
       from pp in _context.tbl_prgm_participants

       join winner_gm in _context.tbl_group_members
           on new
           {
               prgm_id = pp.prgm_id,
               student_id = pp.student_id
           }
           equals new
           {
               prgm_id = winner_gm.prgm_id,
               student_id = winner_gm.student_id
           }

       join gm in _context.tbl_group_members
           on new
           {
               prgm_id = winner_gm.prgm_id,
               group_name = winner_gm.group_name,
               insti_id = winner_gm.instit_id
           }
           equals new
           {
               prgm_id = gm.prgm_id,
               group_name = gm.group_name,
               insti_id = gm.instit_id
           }

       join s in _context.tbl_student
           on gm.student_id equals s.id

       join i in _context.tbl_institute
           on s.institute equals i.id into inst
       from institute in inst.DefaultIfEmpty()

       join p in _context.tbl_program
           on pp.prgm_id equals p.id

       where pp.prgm_id == program_id
             && (pp.position == "1"
                 || pp.position == "2"
                 || pp.position == "3")

       orderby pp.average_point descending

       select new CertificateModel
       {
           Name = s.name ?? "",
           Class = s.category ?? "",
           School = institute != null ? institute.name : "",
           ItemCode = p.item_code ?? "",
           Item = p.program_name ?? "",

           Position = pp.position == "1" ? "First" :
                      pp.position == "2" ? "Second" :
                      pp.position == "3" ? "Third" : "",

           Grade = pp.grade ?? ""
       }
   ).Distinct().ToListAsync();

            //var students = soloStudents
            //    .Concat(groupStudents)
            //    .GroupBy(x => new
            //    {
            //        x.Name,
            //        x.ItemCode
            //    })
            //    .Select(g => g.First())
            //    .ToList();

            var students = soloStudents
    .Concat(groupStudents)
    .GroupBy(x => new
    {
        x.Name,
        x.ItemCode
    })
    .Select(g => g.First())
    .OrderBy(x =>
        x.Position == "First" ? 1 :
        x.Position == "Second" ? 2 :
        x.Position == "Third" ? 3 : 4)
    .ToList();
            if (students.Count == 0)
            {
                return NotFound(
                    $"No participants found for program '{program.item_code}'.");
            }


            // Certificate templates

            var goldTemplatePath = Path.Combine(
                _env.WebRootPath,
                "images",
                "gold.png");

            var silverTemplatePath = Path.Combine(
                _env.WebRootPath,
                "images",
                "silver.png");

            var bronzeTemplatePath = Path.Combine(
                _env.WebRootPath,
                "images",
                "bronze.png");


            // Check templates
            if (!System.IO.File.Exists(goldTemplatePath))
            {
                return NotFound(
                    "Gold certificate template not found.");
            }

            if (!System.IO.File.Exists(silverTemplatePath))
            {
                return NotFound(
                    "Silver certificate template not found.");
            }

            if (!System.IO.File.Exists(bronzeTemplatePath))
            {
                return NotFound(
                    "Bronze certificate template not found.");
            }


            // Load templates
            using var goldBitmap =
                SKBitmap.Decode(goldTemplatePath);

            using var silverBitmap =
                SKBitmap.Decode(silverTemplatePath);

            using var bronzeBitmap =
                SKBitmap.Decode(bronzeTemplatePath);

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true
            };


            // 
            // Font

            using var font = new SKFont
            {
                Size = 55,
                Typeface = SKTypeface.FromFamilyName(
                    "Times New Roman",
                    SKFontStyle.Bold)
            };


            // Create PDF

            using var pdfStream = new MemoryStream();

            using (var document = SKDocument.CreatePdf(pdfStream))
            {
                foreach (var student in students)
                {
                    SKBitmap selectedTemplate;

                    switch ((student.Position ?? "").Trim().ToUpper())
                    {
                        case "FIRST":
                            // Position 1 = Gold
                            selectedTemplate = goldBitmap;
                            break;

                        case "SECOND":
                            // Position 2 = Silver
                            selectedTemplate = silverBitmap;
                            break;

                        case "THIRD":
                            // Position 3 = Bronze
                            selectedTemplate = bronzeBitmap;
                            break;

                        default:
                            continue;
                    }

                    using var pdfCanvas = document.BeginPage(
                        selectedTemplate.Width,
                        selectedTemplate.Height);

                    DrawCertificate(
                        pdfCanvas,
                        selectedTemplate,
                        font,
                        paint,
                        student);

                    document.EndPage();
                }

                document.Close();
            }


         
            var fileName =
                $"certificates_of_{program.item_code}.pdf";


            return File(
                pdfStream.ToArray(),
                "application/pdf",
                fileName);
        }

        private static void DrawCertificate(
          SKCanvas canvas,
          SKBitmap bitmap,
          SKFont font,
          SKPaint paint,
          CertificateModel req)
        {
            // Background
            canvas.DrawBitmap(bitmap, 0, 0);

            font.Edging = SKFontEdging.SubpixelAntialias;

            // ========================================================
            // NAME   (line 650 → 2400, midpoint 1525, width ~1650)
            // ========================================================
            DrawCenteredTextFit(canvas, (req.Name ?? "").ToUpper(), 1525, 1398, 1650, font, paint);

            // ========================================================
            // CLASS  (line 2650 → 2900, midpoint 2775, width ~220)
            // ========================================================
            DrawCenteredTextFit(canvas, req.Class ?? "", 2775, 1397, 220, font, paint);

            // ========================================================
            // SCHOOL (line 650 → 2280, midpoint 1465, width ~1580)
            // ========================================================
            // SCHOOL
            DrawCenteredTextFit(canvas, req.School ?? "", 1552, 1560, 1450, font, paint);

            // ITEM CODE
            DrawCenteredTextFit(canvas, req.ItemCode ?? "", 2780, 1559, 250, font, paint);

            // ITEM
            DrawCenteredTextFit(canvas, req.Item ?? "", 1326, 1707, 1100, font, paint);

            // POSITION
            DrawCenteredTextFit(canvas, req.Position ?? "", 2292, 1707, 240, font, paint);

            // GRADE
            DrawCenteredTextFit(canvas, req.Grade ?? "", 2784, 1705, 250, font, paint);
        }


        private static void DrawCenteredTextFit(
    SKCanvas canvas,
    string text,
    float centerX,
    float centerY,
    float maxWidth,
    SKFont font,
    SKPaint paint,
    float minTextSize = 26f)
        {
            if (string.IsNullOrEmpty(text)) return;

            using var fitFont = new SKFont(font.Typeface, font.Size) { Edging = font.Edging };

            float textWidth = fitFont.MeasureText(text);
            while (textWidth > maxWidth && fitFont.Size > minTextSize)
            {
                fitFont.Size -= 2;
                textWidth = fitFont.MeasureText(text);
            }

            float x = centerX - (textWidth / 2f);
            canvas.DrawText(text, x, centerY, fitFont, paint);
        }






        [HttpGet("GenerateForInstitute")]
        public async Task<IActionResult> GenerateForInstitute(int institute_id, int ac_year_id,int?program_id)
        {
            if (institute_id <= 0)
            {
                return NotFound("No Institute id found");
            }


            var soloStudents = await (
                from pp in _context.tbl_prgm_participants

                join s in _context.tbl_student
                    on pp.student_id equals s.id

                join i in _context.tbl_institute
                    on s.institute equals i.id into inst
                from institute in inst.DefaultIfEmpty()

                join p in _context.tbl_program
                    on pp.prgm_id equals p.id

                where s.institute == institute_id
                      && pp.ac_year_id == ac_year_id
                      && pp.status == "active"
                            && (program_id == null || pp.prgm_id == program_id)

                      && (
                            pp.position == null
                            || (
                                pp.position != "1"
                                && pp.position != "2"
                                && pp.position != "3"
                            )
                         )

                select new CertificateModel
                {
                    Name = s.name ?? "",
                    Class = s.category ?? "",
                    School = institute != null ? institute.name : "",
                    ItemCode = p.item_code ?? "",
                    Item = p.program_name ?? ""
                }
            ).ToListAsync();



            var groupStudents = await (
        from pp in _context.tbl_prgm_participants

            // Find the group to which this participant belongs
        join winner_gm in _context.tbl_group_members
            on new
            {
                prgm_id = pp.prgm_id,
                student_id = pp.student_id
            }
            equals new
            {
                prgm_id = winner_gm.prgm_id,
                student_id = winner_gm.student_id
            }

            // Get members of ONLY that group
        join gm in _context.tbl_group_members
            on new
            {
                prgm_id = winner_gm.prgm_id,
                group_name = winner_gm.group_name,
                instit_id = winner_gm.instit_id
            }
            equals new
            {
                prgm_id = gm.prgm_id,
                group_name = gm.group_name,
                instit_id = gm.instit_id
            }

        join s in _context.tbl_student
            on gm.student_id equals s.id

        join i in _context.tbl_institute
            on s.institute equals i.id into inst
        from institute in inst.DefaultIfEmpty()

        join p in _context.tbl_program
            on pp.prgm_id equals p.id

        where s.institute == institute_id
              && pp.ac_year_id == ac_year_id
              && pp.status == "active"

              // Optional program filter
              && (program_id == null || pp.prgm_id == program_id)

              // Do not include 1st, 2nd or 3rd position
              // NULL position is allowed
              && (
                    pp.position == null
                    || (
                        pp.position != "1"
                        && pp.position != "2"
                        && pp.position != "3"
                    )
                 )

        select new CertificateModel
        {
            Name = s.name ?? "",
            Class = s.category ?? "",
            School = institute != null ? institute.name : "",
            ItemCode = p.item_code ?? "",
            Item = p.program_name ?? ""
        }
    ).ToListAsync();



            var students = soloStudents
                .Concat(groupStudents)
                .GroupBy(x => new
                {
                    x.Name,
                    x.ItemCode
                })
                .Select(g => g.First())
                .ToList();


            if (students.Count == 0)
            {
                return NotFound(
                    "No participants found for this institute."
                );
            }



            var templatePath = Path.Combine(
                _env.WebRootPath,
                "images",
                "Participation_certificate.png"
            );

            if (!System.IO.File.Exists(templatePath))
            {
                return NotFound("Certificate template not found.");
            }


            using var bitmap = SKBitmap.Decode(templatePath);

            if (bitmap == null)
            {
                return NotFound("Unable to load certificate template.");
            }

            var paint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black,
                Style = SKPaintStyle.StrokeAndFill,  // fills AND strokes the outline
                StrokeWidth = 1.2f                    // increase for heavier "fake bold" — try 1–2
            };

            using var font = new SKFont
            {
                Size = 58,
                Typeface = SKTypeface.FromFamilyName("Times New Roman")
            };

            font.Edging = SKFontEdging.SubpixelAntialias;

            using var pdfStream = new MemoryStream();

            using (var document = SKDocument.CreatePdf(pdfStream))
            {
                foreach (var model in students)
                {
                  
                    using var pdfCanvas = document.BeginPage(
                        bitmap.Width,
                        bitmap.Height
                    );


                    pdfCanvas.DrawBitmap(
                        bitmap,
                        0,
                        0
                    );

                    // NAME
                    var name = (model.Name ?? "").ToUpper();
                    float nameWidth = font.MeasureText(name);
                    pdfCanvas.DrawText(
                        name,
                        1540f - (nameWidth / 2f),
                        1427f,
                        font,
                        paint
                    );


               
                    var classText = model.Class ?? "";

                    float classWidth = font.MeasureText(classText);

                    pdfCanvas.DrawText(
                        classText,
                        2775f - (classWidth / 2f),
                        1431f,
                        font,
                        paint
                    );


                    var school = model.School ?? "";

                    float schoolWidth = font.MeasureText(school);

                    pdfCanvas.DrawText(
          school,
          1598f - (schoolWidth / 2f),
          1591f,
          font,
          paint
      );


                    var itemCode = model.ItemCode ?? "";

                    float itemCodeWidth = font.MeasureText(itemCode);

                    pdfCanvas.DrawText(
itemCode,
2795f - (itemCodeWidth / 2f),
1587f,
font,
paint
);


                    var item = model.Item ?? "";

                    float itemWidth = font.MeasureText(item);

 
                    pdfCanvas.DrawText(
                        item,
                        1793f - (itemWidth / 2f),
                        1735f,
                        font,
                        paint
                    );

                    // Finish page
                    document.EndPage();
                }

                document.Close();
            }


            var instituteName = await _context.tbl_institute
                .Where(i => i.id == institute_id)
                .Select(i => i.name)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(instituteName))
            {
                instituteName = "Institute";
            }

            //get itemcode for program if program_id is provided    
            string? itemcode = null;
            if (program_id.HasValue) 
            {
                itemcode = await _context.tbl_program.Where(p => p.id == program_id.Value).Select(p => p.item_code).FirstOrDefaultAsync();
            }
            //
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                instituteName = instituteName.Replace(
                    invalidChar.ToString(),
                    "_"
                );
            }



            //var fileName =
            //    $"participation_certificates_of_{instituteName}.pdf";

            string fileName;


            if (program_id.HasValue && !string.IsNullOrWhiteSpace(itemcode))
            {
                fileName =
                    $"participation_certificates_of_{instituteName}_{itemcode}.pdf";
            }
            else
            {
                fileName =
                    $"participation_certificates_of_{instituteName}.pdf";
            }


            return File(
                pdfStream.ToArray(),
                "application/pdf",
                fileName
            );
        }




        //for chest card download
        [HttpGet]
        [Route("DownloadChestCard")]
        public async Task<IActionResult> DownloadChestCard(int program_id, int student_id)
        {
            var participant = await _context.tbl_prgm_participants
                .FirstOrDefaultAsync(x =>
                    x.prgm_id == program_id &&
                    x.student_id == student_id);

            if (participant == null)
                return NotFound();

            var program = await _context.tbl_program
                .FirstOrDefaultAsync(x => x.id == program_id);

            if (program == null)
                return NotFound();

            var stage = await _context.tbl_stage
                .FirstOrDefaultAsync(x => x.id == program.stage_id);

            if (stage == null)
                return NotFound();

            //string imagePath = Path.Combine(
            //    _env.WebRootPath,
            //    "ChestCards",
            //    stage.chest_image);
            var imagePath = Path.Combine(_env.WebRootPath, "templates", stage.chest_image);

            if (!System.IO.File.Exists(imagePath))
                return NotFound("Template not found.");

            using var bitmap = SKBitmap.Decode(imagePath);

            using var canvas = new SKCanvas(bitmap);

            string text = participant.chess_no.ToString();

            using var paint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true
            };

            using var font = new SKFont
            {
                Size = 300,
                Typeface = SKTypeface.FromFamilyName(
                    "Times New Roman",
                    SKFontStyle.Bold)
            };

            // White box
            float boxLeft = 200;
            float boxTop = 450;
            float boxWidth = 1200;
            float boxHeight = 500;

            // Measure text
            SKRect textBounds;
            font.MeasureText(text, out textBounds);

            // Center horizontally
            float x = boxLeft + (boxWidth - textBounds.Width) / 2f - textBounds.Left;

            // Center vertically
            float y = boxTop + (boxHeight - textBounds.Height) / 2f - textBounds.Top;

            // Draw
            canvas.DrawText(text, x, y, font, paint);
            //------------------------------------

            using var image = SKImage.FromBitmap(bitmap);

            using var data = image.Encode(
                SKEncodedImageFormat.Png,
                100);

            //return File(
            //    data.ToArray(),
            //    "image/png",
            //    $"Chest_{participant.chess_no}.png");
            using var pdfStream = new MemoryStream();

            using (var document = SKDocument.CreatePdf(pdfStream))
            {
                // A5 Landscape
                const float pageWidth = 595f;
                const float pageHeight = 420f;

                using var pdfCanvas = document.BeginPage(pageWidth, pageHeight);

                // Scale while maintaining aspect ratio
                float scale = Math.Min(pageWidth / bitmap.Width,
                                       pageHeight / bitmap.Height);

                float drawWidth = bitmap.Width * scale;
                float drawHeight = bitmap.Height * scale;

                // Center on the page
                float left = (pageWidth - drawWidth) / 2f;
                float top = (pageHeight - drawHeight) / 2f;

                pdfCanvas.DrawBitmap(
                    bitmap,
                    new SKRect(left, top, left + drawWidth, top + drawHeight));

                document.EndPage();
                document.Close();
            }

            string itemCode = program.item_code ?? "";
            string fileName;
            if 
                (!string.IsNullOrWhiteSpace(itemCode))
            { 
                fileName = $"Chest_{participant.chess_no}_{itemCode}.pdf";
            }
            else
            { 
                fileName = $"Chest_{participant.chess_no}.pdf";
            }

            return File(pdfStream.ToArray(), "application/pdf", fileName);
            //return File(
            //    pdfStream.ToArray(),
            //    "application/pdf",
            //    $"Chest_{participant.chess_no}.pdf");


        }



        //        // Replaced implementation for addprgmparticipants with batched queries and single SaveChanges
        //        [HttpPost]
        //        [Route("addprgmparticipants")]
        //        public async Task<ActionResult> addprgmparticipants([FromBody] List<prgm_participants> requestList)
        //        {
        //            if (_context.tbl_prgm_participants == null)
        //                return Problem("Entity set 'tbl_prgm_participants' is null.");

        //            if (!ModelState.IsValid)
        //            {
        //                var errorMessages = ModelState.Values
        //                    .SelectMany(v => v.Errors)
        //                    .Select(e => e.ErrorMessage)
        //                    .ToList();

        //                return Ok(new { status = false, message = string.Join(", ", errorMessages) });
        //            }

        //            if (requestList == null || requestList.Count == 0)
        //                return Ok(new { status = false, message = "No data received" });

        //            // Collect ids
        //            var studentIds = requestList.Where(x => x.student_id.HasValue).Select(x => x.student_id!.Value).Distinct().ToList();
        //            var prgmIds = requestList.Where(x => x.prgm_id.HasValue).Select(x => x.prgm_id!.Value).Distinct().ToList();

        //            if (!studentIds.Any() || !prgmIds.Any())
        //                return Ok(new { status = false, message = "student_id and prgm_id are required" });

        //            // BATCH FETCH #1: students info
        //            var studentsById = await _context.tbl_student
        //                .AsNoTracking()
        //                .Where(s => studentIds.Contains(s.id))
        //                .Select(s => new { s.id, s.institute, s.gender, s.category })
        //                .ToDictionaryAsync(s => s.id, s => s);

        //            // Check missing students up front
        //            foreach (var item in requestList)
        //            {
        //                if (!item.student_id.HasValue || !studentsById.ContainsKey(item.student_id.Value) || studentsById[item.student_id.Value].institute == null)
        //                    return Ok(new { status = false, message = $"Student not found or has no institute: {item.student_id}" });
        //            }

        //            // Group participants by institute (in memory)
        //            var groupedParticipants = requestList
        //                .GroupBy(x => studentsById[x.student_id!.Value].institute!.Value)
        //                .ToDictionary(g => g.Key, g => g.ToList());

        //            var instituteIds = groupedParticipants.Keys.ToList();

        //            // BATCH FETCH #2: program details for all prgmIds
        //            var programsById = await _context.tbl_program
        //                .AsNoTracking()
        //                .Where(p => prgmIds.Contains(p.id) && p.delete_status != "deleted")
        //                .Select(p => new
        //                {
        //                    p.id,
        //                    p.program_type,
        //                    p.gender,
        //                    p.group_min_participants,
        //                    p.group_max_participants,
        //                    p.status,
        //                    p.participant_type,
        //                    p.no_of_group
        //                })
        //                .ToDictionaryAsync(p => p.id, p => p);

        //            // BATCH FETCH #3: existing solo counts per student
        //            var existingSoloCounts = await _context.tbl_prgm_participants
        //                .AsNoTracking()
        //.Where(p => p.student_id.HasValue && studentIds.Contains(p.student_id.Value) && p.status != "deleted")
        //.Join(_context.tbl_program, pp => pp.prgm_id, pr => pr.id, (pp, pr) => new { pp.student_id, pr.program_type })
        //                .Where(x => x.program_type.ToLower() == "solo")
        //                .GroupBy(x => x.student_id)
        //                .Select(g => new { student_id = g.Key, count = g.Count() })
        //                .ToDictionaryAsync(x => x.student_id, x => x.count);

        //            // BATCH FETCH #4: existing group counts per student (via group_members)
        //            var existingGroupCounts = await _context.tbl_group_members
        //                .AsNoTracking()
        //.Where(gm => gm.student_id.HasValue && studentIds.Contains(gm.student_id.Value))
        //.Join(_context.tbl_program, gm => gm.prgm_id, pr => pr.id, (gm, pr) => new { gm.student_id, pr.program_type })
        //                .Where(x => x.program_type.ToLower() == "group")
        //                .GroupBy(x => x.student_id)
        //                .Select(g => new { student_id = g.Key, count = g.Count() })
        //                .ToDictionaryAsync(x => x.student_id, x => x.count);

        //            // Participation limit validation per student (in-memory)
        //            foreach (var studentGroup in requestList.GroupBy(x => x.student_id))
        //            {
        //                int studentId = studentGroup.Key ?? 0;

        //                int requestedSolo = studentGroup.Count(item =>
        //                    item.prgm_id.HasValue &&
        //                    programsById.TryGetValue(item.prgm_id.Value, out var pt) &&
        //                    (pt.program_type ?? "").ToLower() == "solo");

        //                int requestedGroup = studentGroup.Count(item =>
        //                    item.prgm_id.HasValue &&
        //                    programsById.TryGetValue(item.prgm_id.Value, out var pg) &&
        //                    (pg.program_type ?? "").ToLower() == "group");

        //                int existingSolo = existingSoloCounts.GetValueOrDefault(studentId);
        //                int existingGroup = existingGroupCounts.GetValueOrDefault(studentId);

        //                if (requestedSolo + existingSolo > 3)
        //                    return Ok(new { status = false, message = "A student can only participate in up to 3 solo programs." });

        //                if (requestedGroup + existingGroup > 2)
        //                    return Ok(new { status = false, message = "A student can only participate in up to 2 group programs." });
        //            }

        //            // BATCH FETCH #5: existing exact registrations + group memberships for this batch's students+programs
        //            var existingRegistrations = await _context.tbl_prgm_participants
        //                .AsNoTracking()
        //.Where(x => x.student_id.HasValue && x.prgm_id.HasValue
        //    && studentIds.Contains(x.student_id.Value)
        //    && prgmIds.Contains(x.prgm_id.Value)
        //    && x.status != "deleted").Select(x => new { x.student_id, x.prgm_id })
        //                .ToListAsync();
        //            var existingRegSet = existingRegistrations.Select(x => (x.student_id, x.prgm_id)).ToHashSet();

        //            var existingGroupMemberships = await _context.tbl_group_members
        //         .AsNoTracking()
        //         .Where(gm => gm.student_id.HasValue && studentIds.Contains(gm.student_id.Value)
        //             && gm.prgm_id.HasValue && prgmIds.Contains(gm.prgm_id.Value))
        //         .Select(g => new { student_id = g.student_id.Value, prgm_id = g.prgm_id.Value })
        //         .ToListAsync();
        //            var existingGroupMemberSet = existingGroupMemberships.Select(x => (x.student_id, x.prgm_id)).ToHashSet();

        //            // BATCH FETCH #6: counts per (prgmId, institute) already registered
        //            var existingCountsPerPrgmInstitute = await (
        //                from p in _context.tbl_prgm_participants
        //                join s in _context.tbl_student on p.student_id equals s.id
        //                where prgmIds.Contains(p.prgm_id ?? 0) && instituteIds.Contains(s.institute ?? 0) && p.status != "deleted"
        //                select new { p.prgm_id, s.institute }
        //            )
        //            .ToListAsync();

        //            var existingCountsLookup = existingCountsPerPrgmInstitute
        //                .GroupBy(x => new { prgm = x.prgm_id, inst = x.institute })
        //                .ToDictionary(g => (g.Key.prgm, g.Key.inst), g => g.Count());

        //            // BATCH FETCH #7: current group counts for (prgmId, group_name, institute) combinations (only non-null group_name)
        //            var groupKeyTuples = groupedParticipants
        //                .SelectMany(kv => kv.Value.Select(v => new { prgm = v.prgm_id, groupName = v.group_name, inst = kv.Key }))
        //                .Where(x => x.prgm.HasValue && !string.IsNullOrEmpty(x.groupName))
        //                .Select(x => (prgm: x.prgm!.Value, groupName: x.groupName!, inst: x.inst))
        //                .Distinct()
        //                .ToList();

        //            var groupMemberCountsLookup = new Dictionary<(int prgm, string groupName, int inst), int>();
        //            if (groupKeyTuples.Any())
        //            {
        //                var prgmIdsForGroups = groupKeyTuples.Select(g => g.prgm).Distinct().ToList();
        //                var groupNames = groupKeyTuples.Select(g => g.groupName).Distinct().ToList();

        //                var gmCounts = await (
        //                    from gm in _context.tbl_group_members
        //                    join s in _context.tbl_student on gm.student_id equals s.id
        //                    where prgmIdsForGroups.Contains(gm.prgm_id ?? 0)
        //                          && groupNames.Contains(gm.group_name)
        //                          && instituteIds.Contains(s.institute ?? 0)
        //                    select new { gm.prgm_id, gm.group_name, s.institute }
        //                ).ToListAsync();

        //                groupMemberCountsLookup = gmCounts
        //                    .GroupBy(x => (prgm: x.prgm_id ?? 0, groupName: x.group_name ?? "", inst: x.institute ?? 0))
        //                    .ToDictionary(g => g.Key, g => g.Count());
        //            }

        //            // Prepare lists for bulk inserts
        //            var toAddGroupMembers = new List<group_members>();
        //            var toAddPrgmParticipants = new List<prgm_participants>();

        //            // Validation and prepare inserts (in-memory)
        //            foreach (var kv in groupedParticipants)
        //            {
        //                int institute = kv.Key;
        //                var participants = kv.Value;
        //                var prgmId = participants.First().prgm_id;
        //                var groupName = participants.First().group_name;

        //                if (!prgmId.HasValue)
        //                    return Ok(new { status = false, message = "prgm_id missing in request items." });

        //                if (!programsById.TryGetValue(prgmId.Value, out var program))
        //                    return Ok(new { status = false, message = $"Program not found for prgm_id: {prgmId}" });

        //                if ((program.status ?? "").ToLower() != "pending")
        //                    return Ok(new { status = false, message = $"Cannot add participants. Program status is '{program.status}'." });

        //                // Per-participant validations
        //                foreach (var participant in participants)
        //                {
        //                    if (!participant.student_id.HasValue)
        //                        return Ok(new { status = false, message = "student_id required." });

        //                    var st = studentsById[participant.student_id.Value];

        //                    // gender check
        //                    string programGender = (program.gender ?? "").Trim().ToLower().Replace("s", "");
        //                    string studentGenderNorm = (st.gender ?? "").Trim().ToLower();
        //                    if (!string.IsNullOrEmpty(programGender) && programGender != "common" && !string.IsNullOrEmpty(studentGenderNorm) && programGender != studentGenderNorm)
        //                        return Ok(new { status = false, message = $"This program is only for {program.gender} students." });

        //                    // category/participant_type check
        //                    if (!string.Equals(st.category?.Trim(), program.participant_type?.Trim(), StringComparison.OrdinalIgnoreCase))
        //                        return Ok(new { status = false, message = $"Student {participant.student_id} category '{st.category}' does not match program participant type '{program.participant_type}'." });

        //                    // existing registration/group membership check
        //                    if (existingRegSet.Contains((participant.student_id, participant.prgm_id)))
        //                        return Ok(new { status = false, message = $"Student {participant.student_id} is already registered for this program." });

        //                    if (existingGroupMemberSet.Contains((participant.student_id, participant.prgm_id)))
        //                        return Ok(new { status = false, message = $"Student {participant.student_id} is already a group member for this program." });
        //                }

        //                // Check max participants per institute for solo programs
        //                int existingCount = existingCountsLookup.GetValueOrDefault((prgmId.Value, institute));
        //                int newParticipantCount = participants.Count;
        //                int totalCount = existingCount + newParticipantCount;
        //                var programType = (program.program_type ?? "").ToLower();

        //                if (programType == "solo" && program.group_max_participants.HasValue && totalCount > program.group_max_participants.Value)
        //                {
        //                    return Ok(new { status = false, message = $"Maximum {program.group_max_participants} participants allowed from institute {institute}. Current: {existingCount}, Adding: {newParticipantCount}" });
        //                }

        //                if (programType == "group")
        //                {
        //                    var programDetails = program; // already fetched
        //                    int maxParticipants = programDetails.group_max_participants ?? int.MaxValue;
        //                    int noOfGroup = 0;
        //                    int.TryParse(programDetails.no_of_group?.ToString() ?? "0", out noOfGroup);

        //                    if (noOfGroup > 1 && !string.IsNullOrEmpty(groupName))
        //                    {
        //                        int currentCount = groupMemberCountsLookup.GetValueOrDefault((prgmId.Value, groupName ?? "", institute));
        //                        if (currentCount >= maxParticipants)
        //                        {
        //                            return Ok(new { status = false, message = $"Cannot add members '{groupName}' already has {currentCount} participants from institute." });
        //                        }
        //                    }

        //                    // Add group members to list
        //                    foreach (var member in participants)
        //                    {
        //                        var st = studentsById[member.student_id!.Value];
        //                        var gm = new group_members
        //                        {
        //                            prgm_id = member.prgm_id,
        //                            student_id = member.student_id,
        //                            verify_status = "pending",
        //                            group_name = groupName,
        //                            instit_id = st.institute
        //                        };
        //                        toAddGroupMembers.Add(gm);
        //                    }

        //                    // If no main participant record exists for this (prgm + group_name + institute), add one leader entry to prgm_participants
        //                    bool groupInstituteExists = await (from p in _context.tbl_prgm_participants
        //                                                       join s in _context.tbl_student on p.student_id equals s.id
        //                                                       where p.prgm_id == prgmId && p.group_name == groupName && s.institute == institute && p.status != "deleted"
        //                                                       select p).AnyAsync();

        //                    if (!groupInstituteExists && participants.Any())
        //                    {
        //                        var leader = participants.First();
        //                        toAddPrgmParticipants.Add(new prgm_participants
        //                        {
        //                            prgm_id = leader.prgm_id,
        //                            student_id = leader.student_id,
        //                            addedon = DateTime.Now,
        //                            status = "active",
        //                            program_status = "pending",
        //                            chess_no = leader.chess_no,
        //                            group_name = groupName,
        //                            ac_year_id = leader.ac_year_id
        //                        });
        //                    }
        //                }
        //                else
        //                {
        //                    // Solo participants: collect to add
        //                    foreach (var p in participants)
        //                    {
        //                        toAddPrgmParticipants.Add(new prgm_participants
        //                        {
        //                            prgm_id = p.prgm_id,
        //                            student_id = p.student_id,
        //                            addedon = DateTime.Now,
        //                            status = "active",
        //                            program_status = "pending",
        //                            chess_no = p.chess_no,
        //                            ac_year_id = p.ac_year_id
        //                        });
        //                    }
        //                }
        //            }

        //            // Persist all new records in bulk
        //            if (toAddGroupMembers.Any())
        //                await _context.tbl_group_members.AddRangeAsync(toAddGroupMembers);

        //            if (toAddPrgmParticipants.Any())
        //                await _context.tbl_prgm_participants.AddRangeAsync(toAddPrgmParticipants);

        //            await _context.SaveChangesAsync();

        //            return Ok(new { status = true, message = "Data added successfully." });
        //        }





        [HttpPost]
        [Route("addprgmparticipants")]
        public async Task<ActionResult> addprgmparticipants(
           [FromBody] List<prgm_participants> requestList)
        {
            if (_context.tbl_prgm_participants == null)
                return Problem("Entity set 'tbl_prgm_participants' is null.");

            if (requestList == null || !requestList.Any())
            {
                return Ok(new
                {
                    status = false,
                    message = "Participant list is empty."
                });
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

            // ============================================================
            // GET UNIQUE STUDENT AND PROGRAM IDS
            // ============================================================

            var studentIds = requestList
                .Where(x => x.student_id.HasValue)
                .Select(x => x.student_id.Value)
                .Distinct()
                .ToList();

            var programIds = requestList
                .Where(x => x.prgm_id.HasValue)
                .Select(x => x.prgm_id.Value)
                .Distinct()
                .ToList();


            // ============================================================
            // LOAD ALL STUDENTS - ONE QUERY
            // ============================================================

            var students = await _context.tbl_student
                .Where(s => studentIds.Contains(s.id))
                .Select(s => new
                {
                    s.id,
                    s.institute,
                    s.gender,
                    s.category
                })
                .ToListAsync();

            var studentDictionary = students.ToDictionary(x => x.id);


            // ============================================================
            // LOAD ALL REQUESTED PROGRAMS - ONE QUERY
            // SAME AS ORIGINAL: delete_status != "deleted"
            // ============================================================

            var programs = await _context.tbl_program
                .Where(p =>
                    programIds.Contains(p.id) &&
                    p.delete_status != "deleted")
                .Select(p => new
                {
                    p.id,
                    p.program_type,
                    p.gender,
                    p.group_min_participants,
                    p.group_max_participants,
                    p.status,
                    p.participant_type,
                    p.no_of_group
                })
                .ToListAsync();

            var programDictionary = programs.ToDictionary(x => x.id);


            // ============================================================
            // VALIDATE STUDENTS AND PROGRAMS
            // ============================================================

            foreach (var item in requestList)
            {
                if (!item.student_id.HasValue ||
                    !studentDictionary.TryGetValue(
                        item.student_id.Value,
                        out var student))
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Student not found or has no institute: {item.student_id}"
                    });
                }

                if (student.institute == null)
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Student not found or has no institute: {item.student_id}"
                    });
                }

                if (!item.prgm_id.HasValue ||
                    !programDictionary.ContainsKey(item.prgm_id.Value))
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Program not found for prgm_id: {item.prgm_id}"
                    });
                }
            }


            // ============================================================
            // LOAD ALL EXISTING SOLO/GROUP PARTICIPANT RECORDS
            // FOR REQUESTED STUDENTS
            //
            // Needed for 3 SOLO / 2 GROUP validation
            // ============================================================

            var existingParticipantProgramTypes = await
            (
                from pp in _context.tbl_prgm_participants
                join pr in _context.tbl_program
                    on pp.prgm_id equals pr.id
                where pp.student_id.HasValue
                      && studentIds.Contains(pp.student_id.Value)
                      && pp.status != "deleted"
                select new
                {
                    pp.student_id,
                    pp.prgm_id,
                    pp.group_name,
                    pr.program_type
                }
            ).ToListAsync();


            var existingGroupProgramTypes = await
            (
                from gm in _context.tbl_group_members
                join pr in _context.tbl_program
                    on gm.prgm_id equals pr.id
                where gm.student_id.HasValue
                      && studentIds.Contains(gm.student_id.Value)
                select new
                {
                    gm.student_id,
                    gm.prgm_id,
                    gm.group_name,
                    pr.program_type
                }
            ).ToListAsync();


            // ============================================================
            // LOAD EXISTING PARTICIPANTS FOR DUPLICATE CHECK
            // ============================================================

            var existingParticipants = await _context.tbl_prgm_participants
                .Where(x =>
                    x.student_id.HasValue &&
                    studentIds.Contains(x.student_id.Value) &&
                    x.prgm_id.HasValue &&
                    programIds.Contains(x.prgm_id.Value) &&
                    x.status != "deleted")
                .Select(x => new
                {
                    x.student_id,
                    x.prgm_id,
                    x.group_name
                })
                .ToListAsync();


            // ============================================================
            // LOAD EXISTING GROUP MEMBERS FOR DUPLICATE CHECK
            // ============================================================

            var existingGroupMembers = await _context.tbl_group_members
                .Where(x =>
                    x.student_id.HasValue &&
                    studentIds.Contains(x.student_id.Value) &&
                    x.prgm_id.HasValue &&
                    programIds.Contains(x.prgm_id.Value))
                .Select(x => new
                {
                    x.student_id,
                    x.prgm_id,
                    x.group_name
                })
                .ToListAsync();


            // ============================================================
            // HASHSETS FOR FAST DUPLICATE LOOKUP
            // ============================================================

            var participantSet = existingParticipants
                .Select(x => $"{x.student_id}_{x.prgm_id}")
                .ToHashSet();

            var groupMemberSet = existingGroupMembers
                .Select(x => $"{x.student_id}_{x.prgm_id}")
                .ToHashSet();


            // ============================================================
            // PARTICIPATION LIMIT VALIDATION
            // EXACT SAME LOGIC AS ORIGINAL
            // ============================================================

            foreach (var studentGroup in requestList.GroupBy(x => x.student_id))
            {
                int studentId = studentGroup.Key ?? 0;

                int requestedSoloPrograms = 0;
                int requestedGroupPrograms = 0;


                // Count requested programs in current batch
                foreach (var item in studentGroup)
                {
                    if (!item.prgm_id.HasValue)
                        continue;

                    if (!programDictionary.TryGetValue(
                            item.prgm_id.Value,
                            out var program))
                        continue;

                    if (program.program_type != null)
                    {
                        if (program.program_type.ToLower() == "solo")
                            requestedSoloPrograms++;

                        else if (program.program_type.ToLower() == "group")
                            requestedGroupPrograms++;
                    }
                }


                // Existing solo registrations
                int existingSolo = existingParticipantProgramTypes
                    .Count(x =>
                        x.student_id == studentId &&
                        x.program_type != null &&
                        x.program_type.ToLower() == "solo");


                // Existing group registrations
                int existingGroup = existingGroupProgramTypes
                    .Count(x =>
                        x.student_id == studentId &&
                        x.program_type != null &&
                        x.program_type.ToLower() == "group");


                // Maximum 3 solo programs
                if (requestedSoloPrograms + existingSolo > 3)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "A student can only participate in up to 3 solo programs."
                    });
                }


                // Maximum 2 group programs
                if (requestedGroupPrograms + existingGroup > 2)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "A student can only participate in up to 2 group programs."
                    });
                }
            }


            // ============================================================
            // GROUP PARTICIPANTS EXACTLY LIKE ORIGINAL
            // BY INSTITUTE
            // ============================================================

            var groupedParticipants = new Dictionary<int, List<prgm_participants>>();

            foreach (var item in requestList)
            {
                var student = studentDictionary[item.student_id!.Value];

                int institute = student.institute!.Value;

                if (!groupedParticipants.ContainsKey(institute))
                {
                    groupedParticipants[institute] =
                        new List<prgm_participants>();
                }

                groupedParticipants[institute].Add(item);
            }


            // ============================================================
            // PROCESS EACH INSTITUTE GROUP
            // ============================================================

            foreach (var group in groupedParticipants)
            {
                int institute = group.Key;

                var participants = group.Value;

                var prgmId = participants.First().prgm_id;

                var group_name = participants.First().group_name;


                if (!prgmId.HasValue)
                {
                    return Ok(new
                    {
                        status = false,
                        message = "Program ID is required."
                    });
                }


                // Get program from already loaded dictionary

                if (!programDictionary.TryGetValue(
                        prgmId.Value,
                        out var program))
                {
                    return Ok(new
                    {
                        status = false,
                        message = $"Program not found for prgm_id: {prgmId}"
                    });
                }


                // ========================================================
                // PROGRAM STATUS VALIDATION
                // SAME AS ORIGINAL
                // ========================================================

                if (program.status == null ||
                    program.status.ToLower() != "pending")
                {
                    return Ok(new
                    {
                        status = false,
                        message =
                            $"Cannot add participants. Program status is " +
                            $"'{program.status}'. Only programs with 'pending' " +
                            $"status allow participant additions."
                    });
                }


                // ========================================================
                // PARTICIPANT VALIDATION
                // ========================================================

                foreach (var participant in participants)
                {
                    if (!participant.student_id.HasValue)
                    {
                        return Ok(new
                        {
                            status = false,
                            message = "Student ID is required."
                        });
                    }


                    var student =
                        studentDictionary[participant.student_id.Value];


                    // ====================================================
                    // GENDER VALIDATION
                    // EXACT SAME NORMALIZATION AS ORIGINAL
                    // ====================================================

                    string programGender = program.gender?
                        .Trim()
                        .ToLower()
                        .Replace("s", "");

                    string studentGenderNorm = student.gender?
                        .Trim()
                        .ToLower();


                    if (!string.IsNullOrEmpty(programGender) &&
                        programGender != "common" &&
                        !string.IsNullOrEmpty(studentGenderNorm) &&
                        programGender != studentGenderNorm)
                    {
                        return Ok(new
                        {
                            status = false,
                            message =
                                $"This program is only for {program.gender} students."
                        });
                    }


                    // ====================================================
                    // CATEGORY VALIDATION
                    // ====================================================

                    if (!string.Equals(
                            student.category?.Trim(),
                            program.participant_type?.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return Ok(new
                        {
                            status = false,
                            message =
                                $"Student {participant.student_id} category " +
                                $"'{student.category} does not match program " +
                                $"participant type " +
                                $"'{program.participant_type}'."
                        });
                    }


                    // ====================================================
                    // ALREADY REGISTERED IN tbl_prgm_participants
                    // ====================================================

                    string participantKey =
                        $"{participant.student_id}_{participant.prgm_id}";

                    bool exists =
                        participantSet.Contains(participantKey);


                    if (exists)
                    {
                        return Ok(new
                        {
                            status = false,
                            message =
                                "Student is already registered for this program."
                        });
                    }


                    // ====================================================
                    // ALREADY EXISTS IN tbl_group_members
                    // ====================================================

                    bool existsInGroup =
                        groupMemberSet.Contains(participantKey);


                    if (existsInGroup)
                    {
                        return Ok(new
                        {
                            status = false,
                            message =
                                $"Student {participant.student_id} is already " +
                                $"a group member for this program."
                        });
                    }
                }


                // ========================================================
                // EXISTING PARTICIPANT COUNT FOR SOLO PROGRAM
                //
                // Original query:
                // tbl_prgm_participants
                // JOIN tbl_student
                // WHERE program + institute + status != deleted
                // ========================================================

                int existingCount = await
                (
                    from p in _context.tbl_prgm_participants
                    join s in _context.tbl_student
                        on p.student_id equals s.id
                    where p.prgm_id == prgmId.Value
                          && s.institute == institute
                          && p.status != "deleted"
                    select p
                ).CountAsync();


                int newParticipantCount = participants.Count;

                int totalCount =
                    existingCount + newParticipantCount;


                string programType =
                    program.program_type?.ToLower() ?? "";


                // ========================================================
                // SOLO VALIDATION
                // ========================================================

                if (programType == "solo")
                {
                    int maxParticipants =
                        Convert.ToInt32(
                            program.group_max_participants);

                    if (totalCount > maxParticipants)
                    {
                        return Ok(new
                        {
                            status = false,
                            message =
                                $"Maximum {program.group_max_participants} " +
                                $"participants allowed from institute {institute}. " +
                                $"Current: {existingCount}, " +
                                $"Adding: {newParticipantCount}"
                        });
                    }
                }


                // ========================================================
                // GROUP PROGRAM
                // ========================================================

                if (programType == "group")
                {
                    int noOfGroup =
                        Convert.ToInt32(program.no_of_group);

                    int maxParticipants =
                        Convert.ToInt32(
                            program.group_max_participants);


                    // ====================================================
                    // SAME VALIDATION AS YOUR ORIGINAL CODE
                    //
                    // IMPORTANT:
                    // We intentionally keep:
                    //
                    // currentCount >= maxParticipants
                    //
                    // and NOT:
                    //
                    // currentCount + participants.Count > maxParticipants
                    //
                    // because you asked to preserve your existing validation.
                    // ====================================================

                    if (noOfGroup > 1)
                    {
                        var currentCount = await
                        (
                            from gm in _context.tbl_group_members
                            join s in _context.tbl_student
                                on gm.student_id equals s.id
                            where gm.prgm_id == prgmId.Value
                                  && gm.group_name == group_name
                                  && s.institute == institute
                            select gm.id
                        ).CountAsync();


                        if (currentCount >= maxParticipants)
                        {
                            return Ok(new
                            {
                                status = false,
                                message =
                                    $"Cannot add members '{group_name}' already has " +
                                    $"{currentCount} participants from institute."
                            });
                        }
                    }


                    // ====================================================
                    // ADD ALL GROUP MEMBERS
                    // ====================================================

                    foreach (var member in participants)
                    {
                        var student =
                            studentDictionary[member.student_id!.Value];

                        _context.tbl_group_members.Add(
                            new group_members
                            {
                                prgm_id = member.prgm_id,
                                student_id = member.student_id,
                                verify_status = "pending",
                                group_name = group_name,
                                instit_id = student.institute
                            });
                    }


                    // ====================================================
                    // ONE REPRESENTATIVE ENTRY
                    // PER:
                    // PROGRAM + GROUP NAME + INSTITUTE
                    //
                    // SAME AS ORIGINAL
                    // ====================================================

                    bool groupInstituteExists = await
                    (
                        from p in _context.tbl_prgm_participants
                        join s in _context.tbl_student
                            on p.student_id equals s.id
                        where p.prgm_id == prgmId.Value
                              && p.group_name == group_name
                              && s.institute == institute
                              && p.status != "deleted"
                        select p
                    ).AnyAsync();


                    if (!groupInstituteExists && participants.Any())
                    {
                        var leader = participants.First();

                        _context.tbl_prgm_participants.Add(
                            new prgm_participants
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
                }


                // ========================================================
                // SOLO PARTICIPANT ADD
                //
                // SAME AS ORIGINAL ELSE BLOCK
                // ========================================================

                else
                {
                    foreach (var p in participants)
                    {
                        _context.tbl_prgm_participants.Add(
                            new prgm_participants
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
            }


            // ============================================================
            // SAVE ONCE
            //
            // Original code called SaveChangesAsync multiple times.
            // All records are now saved together.
            // ============================================================

            await _context.SaveChangesAsync();


            return Ok(new
            {
                status = true,
                message = "Data added successfully."
            });
        }

//[HttpGet]
//[Route("view_program")]
//public async Task<ActionResult> view_program(
//    int? id,
//    DateTime? date,
//    int? stage_id,
//    string? status,
//    int? institute_id,
//    string? participant_type,
//    string? program_type,
//    string? gender,
//    string? keyword,
//    string? item_code,
//    int? ac_year_id,
//    string? chest_no)
//        {
//            try
//            {
//                // ============================================================
//                // 1. GET JUDGE
//                // ============================================================

//                var enc_key = Request.Headers["XapiKey"].ToString();

//                var user = await _context.tbl_user
//                    .AsNoTracking()
//                    .Where(u => u.enc_key == enc_key && u.type == "judge")
//                    .Select(u => new
//                    {
//                        u.reference_id
//                    })
//                    .FirstOrDefaultAsync();

//                int ref_id = (int)(user?.reference_id ?? 0);

//                // ============================================================
//                // 2. PROGRAM QUERY
//                // ============================================================

//                if (_context.tbl_program == null)
//                {
//                    return NotFound(new
//                    {
//                        status = false,
//                        message = "Data not found"
//                    });
//                }

//                var query =
//                    from p in _context.tbl_program.AsNoTracking()

//                    join s in _context.tbl_stage.AsNoTracking()
//                        on p.stage_id equals s.id into stageJoin

//                    from st in stageJoin.DefaultIfEmpty()

//                    where p.delete_status == "active"

//                    select new ProgramViewModel
//                    {
//                        id = p.id,
//                        program_type = p.program_type,
//                        participant_type = p.participant_type,
//                        gender = p.gender,
//                        stage_id = p.stage_id,

//                        stage_name = st != null
//                            ? st.stage_name
//                            : "",

//                        program_name = p.program_name,
//                        color_code = p.color_code,
//                        date = p.date,
//                        time = p.time,
//                        added_by = p.added_by,
//                        addedtype = p.addedtype,
//                        addedon = p.addedon,
//                        status = p.status,
//                        item_code = p.item_code,
//                        offstage_onstage = p.offstage_onstage,
//                        group_min_participants = p.group_min_participants,
//                        group_max_participants = p.group_max_participants,
//                        ac_year_id = p.ac_year_id
//                    };

//                // ============================================================
//                // 3. APPLY FILTERS BEFORE DATABASE EXECUTION
//                // ============================================================

//                if (id.HasValue)
//                    query = query.Where(x => x.id == id.Value);

//                if (date.HasValue)
//                    query = query.Where(x => x.date == date.Value);

//                if (stage_id.HasValue)
//                    query = query.Where(x => x.stage_id == stage_id.Value);

//                if (!string.IsNullOrWhiteSpace(status))
//                    query = query.Where(x => x.status == status);

//                if (!string.IsNullOrWhiteSpace(participant_type))
//                    query = query.Where(x => x.participant_type == participant_type);

//                if (!string.IsNullOrWhiteSpace(program_type))
//                    query = query.Where(x => x.program_type == program_type);

//                if (!string.IsNullOrWhiteSpace(gender))
//                    query = query.Where(x => x.gender == gender);

//                if (!string.IsNullOrWhiteSpace(item_code))
//                    query = query.Where(x => x.item_code == item_code);

//                if (!string.IsNullOrWhiteSpace(keyword))
//                {
//                    query = query.Where(x =>
//                        x.program_name.Contains(keyword) ||
//                        x.item_code.Contains(keyword));
//                }

//                if (ac_year_id.HasValue)
//                    query = query.Where(x => x.ac_year_id == ac_year_id.Value);

//                var programs = await query.ToListAsync();

//                if (programs.Count == 0)
//                {
//                    return Ok(new
//                    {
//                        status = false,
//                        message = "Data not found"
//                    });
//                }

//                var programIds = programs
//                    .Select(x => x.id)
//                    .ToList();

//                // ============================================================
//                // 4. LOAD ALL RELATED DATA
//                // ============================================================

//                var judgementCriteriaData =
//                    await (
//                        from c in _context.tbl_prgm_judgement_criteria.AsNoTracking()

//                        join j in _context.tbl_judgement_criteria.AsNoTracking()
//                            on c.judgement_criteria equals j.id

//                        where c.prgm_id.HasValue &&
//                              programIds.Contains(c.prgm_id.Value)

//                        select new
//                        {
//                            c.id,
//                            c.prgm_id,
//                            judgement_criteria = c.judgement_criteria,
//                            name = j != null ? j.name : "",
//                            c.point
//                        }
//                    ).ToListAsync();

//                var judgementCriteriaByProgram =
//                    judgementCriteriaData
//                        .GroupBy(x => x.prgm_id)
//                        .ToDictionary(
//                            x => x.Key,
//                            x => x.ToList());

//                // ============================================================

//                var judgesData =
//                    await (
//                        from c in _context.tbl_judge_prgm.AsNoTracking()

//                        join j in _context.tbl_judge.AsNoTracking()
//                            on c.judge_id equals j.id

//                        where c.program_id.HasValue &&
//                              programIds.Contains(c.program_id.Value) &&
//                              c.status == "active"

//                        select new
//                        {
//                            c.id,
//                            c.program_id,
//                            c.judge_id,
//                            judge = j.judge_name
//                        }
//                    ).ToListAsync();

//                var judgesByProgram =
//                    judgesData
//                        .GroupBy(x => x.program_id)
//                        .ToDictionary(
//                            x => x.Key,
//                            x => x.ToList());

//                // ============================================================
//                // 5. LOAD PROGRAM PARTICIPANTS
//                // ============================================================

//                var participantsData =
//                    await _context.tbl_prgm_participants
//                        .AsNoTracking()
//                        .Where(x =>
//                            x.prgm_id.HasValue &&
//                            programIds.Contains(x.prgm_id.Value))
//                        .Select(x => new ParticipantData
//                        {
//                            id = x.id,
//                            prgm_id = x.prgm_id,
//                            student_id = x.student_id,
//                            chess_no = x.chess_no,
//                            status = x.status,
//                            program_status = x.program_status
//                        })
//                        .ToListAsync();

//                var participantsByProgram =
//                    participantsData
//                        .GroupBy(x => x.prgm_id)
//                        .ToDictionary(
//                            x => x.Key,
//                            x => x.ToList());

//                // ============================================================
//                // 6. IMPORTANT
//                //
//                // Preserve existing cross-product behavior.
//                //
//                // Only load columns actually used by the cross-product.
//                // ============================================================

//                var allParticipantsData =
//                    await _context.tbl_prgm_participants
//                        .AsNoTracking()
//                        .Select(x => new AllParticipantData
//                        {
//                            program_status = x.program_status
//                        })
//                        .ToListAsync();

//                // ============================================================
//                // 7. GROUP MEMBERS
//                // ============================================================

//                var groupMembersData =
//                    await _context.tbl_group_members
//                        .AsNoTracking()
//                        .Where(x =>
//                            x.prgm_id.HasValue &&
//                            programIds.Contains(x.prgm_id.Value))
//                        .Select(x => new GroupMemberData
//                        {
//                            id = x.id,
//                            prgm_id = x.prgm_id,
//                            student_id = x.student_id,
//                            chest_no = x.chest_no,
//                            group_name = x.group_name
//                        })
//                        .ToListAsync();

//                var groupMembersByProgram =
//                    groupMembersData
//                        .GroupBy(x => x.prgm_id)
//                        .ToDictionary(
//                            x => x.Key,
//                            x => x.ToList());

//                // ============================================================
//                // 8. LOAD STUDENTS
//                // ============================================================

//                var studentIds =
//                    groupMembersData
//                        .Where(x => x.student_id.HasValue)
//                        .Select(x => x.student_id.Value)
//                        .Concat(
//                            participantsData
//                                .Where(x => x.student_id.HasValue)
//                                .Select(x => x.student_id.Value))
//                        .Distinct()
//                        .ToList();

//                var studentsData =
//                    await _context.tbl_student
//                        .AsNoTracking()
//                        .Where(x => studentIds.Contains(x.id))
//                        .Select(x => new StudentData
//                        {
//                            id = x.id,
//                            name = x.name,
//                            institute = x.institute,
//                            phone_no = x.phone_no,
//                            admsn_no = x.admsn_no,
//                            email = x.email
//                        })
//                        .ToListAsync();

//                var studentsById =
//                    studentsData.ToDictionary(
//                        x => x.id,
//                        x => x);

//                // ============================================================
//                // 9. LOAD INSTITUTES
//                // ============================================================

//                var instituteIds =
//                    studentsData
//                        .Where(x => x.institute.HasValue)
//                        .Select(x => x.institute.Value)
//                        .Distinct()
//                        .ToList();

//                var institutesData =
//                    await _context.tbl_institute
//                        .AsNoTracking()
//                        .Where(x =>
//                            x.id.HasValue &&
//                            instituteIds.Contains(x.id.Value))
//                        .Select(x => new InstituteData
//                        {
//                            id = x.id,
//                            name = x.name
//                        })
//                        .ToListAsync();

//                var institutesById =
//                    institutesData.ToDictionary(
//                        x => x.id,
//                        x => x);

//                // ============================================================
//                // 10. LOAD JUDGE POINTS
//                // ============================================================

//                var judgePointsData =
//                    await _context.tbl_prgm_point
//                        .AsNoTracking()
//                        .Where(x =>
//                            x.prgm_id.HasValue &&
//                            programIds.Contains(x.prgm_id.Value) &&
//                            x.judge_id == ref_id)
//                        .Select(x => new JudgePointData
//                        {
//                            prgm_id = x.prgm_id,
//                            chess_no = x.chess_no,
//                            point = x.point,
//                            judgement_criteria_id = x.judgement_criteria_id
//                        })
//                        .ToListAsync();

//                var pointsByProgramChest =
//                    judgePointsData
//                        .GroupBy(x => new
//                        {
//                            x.prgm_id,
//                            x.chess_no
//                        })
//                        .ToDictionary(
//                            x => (
//                                x.Key.prgm_id,
//                                x.Key.chess_no),
//                            x => x.ToList());

//                // ============================================================
//                // 11. PARTICIPANT COUNTS
//                // ============================================================

//                var participantCountByProgram =
//                    participantsData
//                        .Where(x =>
//                            !string.Equals(
//                                x.status,
//                                "deleted",
//                                StringComparison.OrdinalIgnoreCase))
//                        .GroupBy(x => x.prgm_id)
//                        .ToDictionary(
//                            g => g.Key,
//                            g =>
//                            {
//                                var program =
//                                    programs.FirstOrDefault(p =>
//                                        p.id == g.Key);

//                                if (program != null &&
//                                    string.Equals(
//                                        program.program_type,
//                                        "group",
//                                        StringComparison.OrdinalIgnoreCase))
//                                {
//                                    var groupCount =
//                                        groupMembersData.Count(x =>
//                                            x.prgm_id == g.Key);

//                                    return groupCount * g.Count();
//                                }

//                                return g.Count();
//                            });

//                // ============================================================
//                // 12. BUILD GROUP STUDENT DETAILS
//                // ============================================================

//                var groupStudentDetailsByProgram =
//                    new Dictionary<int, List<StudentDetail>>();

//                foreach (var program in programs)
//                {
//                    if (!string.Equals(
//                            program.program_type,
//                            "group",
//                            StringComparison.OrdinalIgnoreCase))
//                    {
//                        continue;
//                    }

//                    var details =
//                        new List<StudentDetail>();

//                    if (!groupMembersByProgram.TryGetValue(
//                            program.id,
//                            out var groupMembers))
//                    {
//                        groupStudentDetailsByProgram[program.id] =
//                            details;

//                        continue;
//                    }

//                    foreach (var gd in groupMembers)
//                    {
//                        if (!gd.student_id.HasValue)
//                            continue;

//                        if (!studentsById.TryGetValue(
//                                gd.student_id.Value,
//                                out var student))
//                        {
//                            continue;
//                        }

//                        if (!student.institute.HasValue)
//                            continue;

//                        if (!institutesById.TryGetValue(
//                                student.institute.Value,
//                                out var instituteData))
//                        {
//                            continue;
//                        }

//                        if (institute_id.HasValue &&
//                            student.institute != institute_id)
//                        {
//                            continue;
//                        }

//                        if (!string.IsNullOrWhiteSpace(chest_no) &&
//                            gd.chest_no != chest_no)
//                        {
//                            continue;
//                        }

//                        // ----------------------------------------------------
//                        // POINTS ONLY DEPEND ON gd.prgm_id + gd.chest_no.
//                        // Calculate this once instead of looking it up for
//                        // every all-participant row.
//                        // ----------------------------------------------------

//                        pointsByProgramChest.TryGetValue(
//                            (gd.prgm_id, gd.chest_no),
//                            out var pointList);

//                        bool hasPoints =
//                            pointList != null &&
//                            pointList.Count > 0;

//                        // ----------------------------------------------------
//                        // Existing cross-product behavior preserved.
//                        // ----------------------------------------------------

//                        foreach (var sp in allParticipantsData)
//                        {
//                            if (hasPoints)
//                            {
//                                foreach (var pp in pointList)
//                                {
//                                    details.Add(new StudentDetail
//                                    {
//                                        student_id = student.id,
//                                        student_name = student.name,
//                                        institute = student.institute,
//                                        institute_name = instituteData.name,
//                                        phone_no = student.phone_no,
//                                        admsn_no = student.admsn_no,
//                                        email = student.email,
//                                        chest_no = gd.chest_no,
//                                        status = sp.program_status,
//                                        group_name = gd.group_name,
//                                        point = pp.point,
//                                        judgement_criteria_id =
//                                            pp.judgement_criteria_id
//                                    });
//                                }
//                            }
//                            else
//                            {
//                                details.Add(new StudentDetail
//                                {
//                                    student_id = student.id,
//                                    student_name = student.name,
//                                    institute = student.institute,
//                                    institute_name = instituteData.name,
//                                    phone_no = student.phone_no,
//                                    admsn_no = student.admsn_no,
//                                    email = student.email,
//                                    chest_no = gd.chest_no,
//                                    status = sp.program_status,
//                                    group_name = gd.group_name,
//                                    point = null,
//                                    judgement_criteria_id = null
//                                });
//                            }
//                        }
//                    }

//                    // No reflection
//                    details =
//                        details
//                            .OrderBy(x => x.student_name)
//                            .ToList();

//                    groupStudentDetailsByProgram[program.id] =
//                        details;
//                }

//                // ============================================================
//                // 13. BUILD NORMAL STUDENT DETAILS
//                // ============================================================

//                var normalStudentDetailsByProgram =
//                    new Dictionary<int, List<StudentDetail>>();

//                foreach (var program in programs)
//                {
//                    if (string.Equals(
//                            program.program_type,
//                            "group",
//                            StringComparison.OrdinalIgnoreCase))
//                    {
//                        continue;
//                    }

//                    var details =
//                        new List<StudentDetail>();

//                    if (!participantsByProgram.TryGetValue(
//                            program.id,
//                            out var programParticipants))
//                    {
//                        normalStudentDetailsByProgram[program.id] =
//                            details;

//                        continue;
//                    }

//                    foreach (var sp in programParticipants)
//                    {
//                        if (string.Equals(
//                                sp.status,
//                                "deleted",
//                                StringComparison.OrdinalIgnoreCase))
//                        {
//                            continue;
//                        }

//                        studentsById.TryGetValue(
//                            sp.student_id ?? 0,
//                            out var student);

//                        if (institute_id.HasValue)
//                        {
//                            if (student == null ||
//                                student.institute != institute_id)
//                            {
//                                continue;
//                            }
//                        }

//                        if (!string.IsNullOrWhiteSpace(chest_no) &&
//                            sp.chess_no != chest_no)
//                        {
//                            continue;
//                        }

//                        int? studentId =
//                            student != null
//                                ? student.id
//                                : (int?)null;

//                        string studentName =
//                            student != null
//                                ? student.name
//                                : "";

//                        int? institute =
//                            student != null
//                                ? student.institute
//                                : null;

//                        string instituteName = "";

//                        if (student != null &&
//                            student.institute.HasValue &&
//                            institutesById.TryGetValue(
//                                student.institute.Value,
//                                out var instituteData))
//                        {
//                            instituteName =
//                                instituteData.name;
//                        }

//                        string phoneNo =
//                            student != null
//                                ? student.phone_no
//                                : "";

//                        string admsnNo =
//                            student != null
//                                ? student.admsn_no
//                                : "";

//                        string email =
//                            student != null
//                                ? student.email
//                                : "";

//                        pointsByProgramChest.TryGetValue(
//                            (sp.prgm_id, sp.chess_no),
//                            out var pointList);

//                        if (pointList != null &&
//                            pointList.Count > 0)
//                        {
//                            foreach (var pp in pointList)
//                            {
//                                details.Add(new StudentDetail
//                                {
//                                    student_id = studentId,
//                                    student_name = studentName,
//                                    institute = institute,
//                                    institute_name = instituteName,
//                                    phone_no = phoneNo,
//                                    admsn_no = admsnNo,
//                                    email = email,
//                                    chest_no = sp.chess_no,
//                                    status = sp.program_status,
//                                    group_name = null,
//                                    point = pp.point,
//                                    judgement_criteria_id =
//                                        pp.judgement_criteria_id
//                                });
//                            }
//                        }
//                        else
//                        {
//                            details.Add(new StudentDetail
//                            {
//                                student_id = studentId,
//                                student_name = studentName,
//                                institute = institute,
//                                institute_name = instituteName,
//                                phone_no = phoneNo,
//                                admsn_no = admsnNo,
//                                email = email,
//                                chest_no = sp.chess_no,
//                                status = sp.program_status,
//                                group_name = null,
//                                point = null,
//                                judgement_criteria_id = null
//                            });
//                        }
//                    }

//                    // No reflection
//                    details =
//                        details
//                            .OrderBy(x => x.student_name)
//                            .ToList();

//                    normalStudentDetailsByProgram[program.id] =
//                        details;
//                }

//                // ============================================================
//                // 14. FINAL RESULT
//                // ============================================================

//                var result =
//                    programs.Select(p =>
//                    {
//                        IEnumerable<object> judgementCriteria =
//                            judgementCriteriaByProgram.TryGetValue(
//                                p.id,
//                                out var criteriaList)
//                                ? criteriaList.Cast<object>()
//                                : Enumerable.Empty<object>();

//                        IEnumerable<object> judges =
//                            judgesByProgram.TryGetValue(
//                                p.id,
//                                out var judgeList)
//                                ? judgeList.Cast<object>()
//                                : Enumerable.Empty<object>();

//                        List<StudentDetail> studentDetails;

//                        if (string.Equals(
//                                p.program_type,
//                                "group",
//                                StringComparison.OrdinalIgnoreCase))
//                        {
//                            studentDetails =
//                                groupStudentDetailsByProgram.TryGetValue(
//                                    p.id,
//                                    out var groupDetails)
//                                    ? groupDetails
//                                    : new List<StudentDetail>();
//                        }
//                        else
//                        {
//                            studentDetails =
//                                normalStudentDetailsByProgram.TryGetValue(
//                                    p.id,
//                                    out var normalDetails)
//                                    ? normalDetails
//                                    : new List<StudentDetail>();
//                        }

//                        var participantCount =
//                            participantCountByProgram.TryGetValue(
//                                p.id,
//                                out var count)
//                                ? count
//                                : 0;

//                        return new
//                        {
//                            p.id,
//                            p.program_type,
//                            p.participant_type,
//                            p.gender,
//                            p.stage_id,
//                            p.stage_name,
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

//                            participant_count =
//                                participantCount,

//                            judgement_criteria =
//                                judgementCriteria,

//                            judges =
//                                judges,

//                            prgm_student_detail =
//                                studentDetails
//                        };
//                    })
//                    .ToList();

//                return Ok(new
//                {
//                    status = true,
//                    message = "Success",
//                    data = result
//                });
//            }
//            catch (Exception ex)
//            {
//                return Ok(new
//                {
//                    status = false,
//                    message = ex.Message
//                });
//            }
//        }


//        // ================================================================
//        // SUPPORTING MODELS
//        // Put these inside your controller class or outside the controller.
//        // ================================================================

//        public class ProgramViewModel
//        {
//            public int id { get; set; }
//            public string? program_type { get; set; }
//            public string? participant_type { get; set; }
//            public string? gender { get; set; }
//            public int? stage_id { get; set; }
//            public string stage_name { get; set; } = "";
//            public string? program_name { get; set; }
//            public string? color_code { get; set; }
//            public DateTime? date { get; set; }
//            public string? time { get; set; }
//            public int? added_by { get; set; }
//            public string? addedtype { get; set; }
//            public DateTime? addedon { get; set; }
//            public string? status { get; set; }
//            public string? item_code { get; set; }
//            public string? offstage_onstage { get; set; }
//            public int? group_min_participants { get; set; }
//            public int? group_max_participants { get; set; }
//            public int? no_of_group { get; set; }
//            public int? ac_year_id { get; set; }
//        }

//        public class ParticipantData
//        {
//            public int? id { get; set; }
//            public int? prgm_id { get; set; }
//            public int? student_id { get; set; }
//            public string? chess_no { get; set; }
//            public string? status { get; set; }
//            public string? program_status { get; set; }
//        }

//        public class AllParticipantData
//        {
//            public string? program_status { get; set; }
//        }

//        public class GroupMemberData
//        {
//            public int? id { get; set; }
//            public int? prgm_id { get; set; }
//            public int? student_id { get; set; }
//            public string? chest_no { get; set; }
//            public string? group_name { get; set; }
//        }

//        public class StudentData
//        {
//            public int? id { get; set; }
//            public string? name { get; set; }
//            public int? institute { get; set; }
//            public string? phone_no { get; set; }
//            public string? admsn_no { get; set; }
//            public string? email { get; set; }
//        }

//        public class InstituteData
//        {
//            public int? id { get; set; }
//            public string? name { get; set; }
//        }

//        public class JudgePointData
//        {
//            public int? prgm_id { get; set; }
//            public string? chess_no { get; set; }
//            public double? point { get; set; }
//            public int? judgement_criteria_id { get; set; }
//        }

//        public class StudentDetail
//        {
//            public int? student_id { get; set; }
//            public string? student_name { get; set; }
//            public int? institute { get; set; }
//            public string? institute_name { get; set; }
//            public string? phone_no { get; set; }
//            public string? admsn_no { get; set; }
//            public string? email { get; set; }
//            public string? chest_no { get; set; }
//            public string? status { get; set; }
//            public string? group_name { get; set; }
//            public double? point { get; set; }
//            public int? judgement_criteria_id { get; set; }
//        }

    }
}