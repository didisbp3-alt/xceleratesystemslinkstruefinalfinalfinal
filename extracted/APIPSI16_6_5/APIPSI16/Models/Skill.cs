using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Skill
{
    public int SkillId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
}
