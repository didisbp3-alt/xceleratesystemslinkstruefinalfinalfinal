using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class UserSkill
{
    public int UserSkillId { get; set; }

    public int UserId { get; set; }

    public int SkillId { get; set; }

    public int EndorsementCount { get; set; }

    public DateTime AddedAt { get; set; }

    public virtual Skill Skill { get; set; } = null!;

    public virtual ICollection<SkillEndorsement> SkillEndorsements { get; set; } = new List<SkillEndorsement>();

    public virtual User User { get; set; } = null!;
}
