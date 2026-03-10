using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class ProfileEducation
{
    public int EducationId { get; set; }

    public int UserId { get; set; }

    public string School { get; set; } = null!;

    public string? Degree { get; set; }

    public string? FieldOfStudy { get; set; }

    public int? StartYear { get; set; }

    public int? EndYear { get; set; }

    public string? Description { get; set; }

    public virtual User User { get; set; } = null!;
}
