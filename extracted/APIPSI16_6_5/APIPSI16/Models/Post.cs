using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Post
{
    public int PostId { get; set; }

    public int UserId { get; set; }

    public string? Content { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte Visibility { get; set; }

    public virtual ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();

    public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();

    public virtual User User { get; set; } = null!;
}
