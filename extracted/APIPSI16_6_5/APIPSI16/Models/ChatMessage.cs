using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class ChatMessage
{
    public int MessageId { get; set; }

    public int ChatId { get; set; }

    public int SenderUserId { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Chat Chat { get; set; } = null!;

    public virtual User SenderUser { get; set; } = null!;
}
