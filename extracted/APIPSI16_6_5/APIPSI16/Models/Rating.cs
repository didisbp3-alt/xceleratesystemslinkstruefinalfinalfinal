using System;

namespace APIPSI16.Models
{
    public partial class Rating
    {
        public int RatingId { get; set; }
        public int RatedByUserId { get; set; }
        public int RatedEntityId { get; set; }
        public string EntityType { get; set; } = null!;
        public int Score { get; set; }
        public string? Review { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual User? RatedByUser { get; set; }
    }
}
