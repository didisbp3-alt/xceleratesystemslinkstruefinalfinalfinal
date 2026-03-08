using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Nationality
{
    public int NationalityId { get; set; }

    public string Name { get; set; } = null!;

    public string? Isocode { get; set; }
}
