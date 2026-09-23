using System;
using System.Collections.Generic;

namespace kalanjali_api
{
    public static class StatusEnum
    {
        // ========================
        // Student Status Enum
        // ========================
        public enum StudentStatus
        {
            StudentRegistration ,
            StudentIDVerification ,
            ChestNumberGenerated ,
            JudgementSheetPrepared ,
            JudgementCompleted ,
            MarksheetPreparation ,
            ResultPreparation ,
            ResultAnnouncement 
        }

        // ========================
        // Program Status Enum
        // ========================
        public enum ProgramStatus
        {
            ProgramStarted ,
            ProgramOngoing ,
            JudgementCompleted,

            ProgramCompleted,
            ResultPublished 
        }

        // ========================
        // Student Status Labels
        // ========================
        private static readonly Dictionary<StudentStatus, string> StudentStatusDisplay = new()
        {
           // { StudentStatus.StudentRegistration, "Student Registration" },
            { StudentStatus.StudentIDVerification, "Verification Completed" },
            { StudentStatus.ChestNumberGenerated, "Chest Number Generated" },
            { StudentStatus.JudgementSheetPrepared, "judgement sheet prepared" },
            { StudentStatus.JudgementCompleted, "judgement completed" },
            { StudentStatus.MarksheetPreparation, "marksheet prepared" },
            { StudentStatus.ResultPreparation, "result prepared" },
            { StudentStatus.ResultAnnouncement, "result announced" }
        };

        // ========================
        // Program Status Labels
        // ========================
        private static readonly Dictionary<ProgramStatus, string> ProgramStatusDisplay = new()
        {
          //  { ProgramStatus.ProgramStarted, "Program Started" },
            { ProgramStatus.ProgramOngoing, "ongoing" },
            //{ ProgramStatus.ProgramCompleted, "Program Completed" },
            { ProgramStatus.JudgementCompleted, "judgement completed" },
            { ProgramStatus.ResultPublished, "result published" }
        };

        // ========================
        // Helper Methods
        // ========================
        public static string GetStudentLabel(StudentStatus status)
        {
            return StudentStatusDisplay.TryGetValue(status, out var label) ? label : status.ToString();
        }

        public static string GetProgramLabel(ProgramStatus status)
        {
            return ProgramStatusDisplay.TryGetValue(status, out var label) ? label : status.ToString();
        }

        public static string GetStudentLabel(int statusValue)
        {
            return Enum.IsDefined(typeof(StudentStatus), statusValue)
                ? GetStudentLabel((StudentStatus)statusValue)
                : statusValue.ToString();
        }

        public static string GetProgramLabel(int statusValue)
        {
            return Enum.IsDefined(typeof(ProgramStatus), statusValue)
                ? GetProgramLabel((ProgramStatus)statusValue)
                : statusValue.ToString();
        }
    }
}
