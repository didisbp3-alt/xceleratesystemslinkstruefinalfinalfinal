using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class PostReaction
{
    public int ReactionId { get; set; }

    public int PostId { get; set; }

    public int UserId { get; set; }

    public byte ReactionType { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Post Post { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
