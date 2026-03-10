using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class PostComment
{
    public int CommentId { get; set; }

    public int PostId { get; set; }

    public int UserId { get; set; }

    public int? ParentCommentId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public bool IsModerated { get; set; }

    public int? ModeratedBy { get; set; }

    public DateTime? ModeratedAt { get; set; }

    public bool IsDeleted { get; set; }

    public virtual ICollection<PostComment> InverseParentComment { get; set; } = new List<PostComment>();

    public virtual PostComment? ParentComment { get; set; }

    public virtual Post Post { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
