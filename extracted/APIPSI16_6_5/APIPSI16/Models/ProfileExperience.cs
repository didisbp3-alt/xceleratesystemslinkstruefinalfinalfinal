using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class ProfileExperience
{
    public int ExperienceId { get; set; }

    public int UserId { get; set; }

    public string CompanyName { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Location { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsCurrent { get; set; }

    public string? Description { get; set; }

    public virtual User User { get; set; } = null!;
}
