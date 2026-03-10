using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Country
{
    public int CountryId { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Location> Locations { get; set; } = new List<Location>();
}
