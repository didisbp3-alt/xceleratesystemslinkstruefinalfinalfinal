using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class UserJobPreference
{
    public int UserJobPreferenceId { get; set; }

    public int UserId { get; set; }

    public int JobRoleId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual JobRole JobRole { get; set; } = null!;
}
