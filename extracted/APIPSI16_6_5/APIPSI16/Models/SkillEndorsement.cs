using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class SkillEndorsement
{
    public int EndorsementId { get; set; }

    public int UserSkillId { get; set; }

    public int EndorserUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User EndorserUser { get; set; } = null!;

    public virtual UserSkill UserSkill { get; set; } = null!;
}
