using System;
using System.Collections.Generic;

namespace XcelerateLinks_DTOs;


public partial class UserDTO
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int? UserId { get; set; }

    public int? OpportunityId { get; set; }

    public virtual Opportunity? Opportunity { get; set; }

    public virtual User? User { get; set; }
}