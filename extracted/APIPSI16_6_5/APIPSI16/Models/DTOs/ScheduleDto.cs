namespace APIPSI16.Models.DTOs
{
    public class ScheduleDto
    {
        public int JobApplicationId { get; set; }
        public byte RoundNumber { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int InterviewerUserId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public byte? Outcome { get; set; }
    }
}