using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class InterviewRound
{
    public int InterviewRoundId { get; set; }

    public int JobApplicationId { get; set; }

    public byte RoundNumber { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public int? InterviewerUserId { get; set; }

    public string? Notes { get; set; }

    public byte? Outcome { get; set; }

    public virtual User? InterviewerUser { get; set; }

    public virtual JobApplication JobApplication { get; set; } = null!;
}
