
using kalanjali_api.Data;
using kalanjali_api.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace kalanjali_api.Services
{
    public class VerificationReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public VerificationReminderService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }
  


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckPrograms();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task CheckPrograms()
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<kalanjaliDbContext>();

            var today = DateTime.Today;

            var programs = await context.tbl_program
                .Where(x => x.date == today)
                .ToListAsync();

            foreach (var program in programs)
            {
                if (program.time == null)
                    continue;

                if (!TimeSpan.TryParse(program.time, out TimeSpan programTime))
                    continue;

                DateTime programStart = program.date.Value.Date.Add(programTime);

                if (programStart < DateTime.Now)
                    continue;

                if (programStart > DateTime.Now.AddMinutes(30))
                    continue;

                await CheckPendingVerification(context, program);
            }
        }

        private async Task CheckPendingVerification(
         kalanjaliDbContext context,
         programmodel program)
        {
            var institutes =
                await
                (
                    from p in context.tbl_prgm_participants

                    join s in context.tbl_student
                        on p.student_id equals s.id

                    join i in context.tbl_institute
                        on s.institute equals i.id

                    where p.prgm_id == program.id
                          && p.status != "deleted"
                          && (
                                p.program_status == null ||
                                p.program_status != "Verification Completed"
                             )

                    group p by new
                    {
                        i.id,
                        i.name,
                        i.contact_person,
                        i.phone_no
                    }
                    into g

                    select new
                    {
                        InstituteId = g.Key.id,
                        Institute = g.Key.name,
                        Mobile = g.Key.phone_no,
                        Pending = g.Count()
                    }

                ).ToListAsync();

            foreach (var institute in institutes)
            {
                //bool alreadySent =
                //    await context.tbl_program_reminder.AnyAsync(x =>
                //        x.program_id == program.id &&
                //        x.institute_id == institute.InstituteId);

                //if (alreadySent)
                //    continue;

                string message =
        $@"Reminder

Your participants for

{program.program_name}

are scheduled shortly.

Pending verification : {institute.Pending}

Please report to the verification counter immediately.

Thank you.";

                // Send WhatsApp here

                // await WhatsAppService.Send(institute.Mobile,message);

                //context.tbl_program_reminder.Add(
                //    new tbl_program_reminder
                //    {
                //        program_id = program.id,
                //        institute_id = institute.InstituteId,
                //        sent_on = DateTime.Now
                //    });

                await context.SaveChangesAsync();
            }
        }
    }
}