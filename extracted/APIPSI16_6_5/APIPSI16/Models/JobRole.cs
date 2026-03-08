using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class JobRole
{
    public int JobRoleId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<UserJobPreference> UserJobPreferences { get; set; } = new List<UserJobPreference>();
}
