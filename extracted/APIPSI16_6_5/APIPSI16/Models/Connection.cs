using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Connection
{
    public int ConnectionId { get; set; }

    public int RequesterUserId { get; set; }

    public int AddresseeUserId { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? AcceptedAt { get; set; }
}
