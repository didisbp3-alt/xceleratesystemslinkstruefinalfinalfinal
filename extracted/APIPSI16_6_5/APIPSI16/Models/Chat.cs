using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Chat
{
    public int ChatId { get; set; }

    public string Type { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public int CreatedByUserId { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<ChatUser> ChatUsers { get; set; } = new List<ChatUser>();

    public virtual User CreatedByUser { get; set; } = null!;
}
