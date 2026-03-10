using System;

namespace APIPSI16.Models;

public partial class Location
{
    public int LocationId { get; set; }

    public int CountryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Region { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Country Country { get; set; } = null!;
}
