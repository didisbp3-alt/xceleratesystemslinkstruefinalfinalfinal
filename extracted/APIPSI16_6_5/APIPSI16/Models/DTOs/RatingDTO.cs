using System;

namespace APIPSI16.Models.DTOs
{
    public class RatingDTO
    {
        public int RatingId { get; set; }
        public int RatedByUserId { get; set; }
        public string? RatedByUserName { get; set; }
        public int RatedEntityId { get; set; }
        public string EntityType { get; set; } = null!; // "User", "Company"
        public int Score { get; set; } // 1-5
        public string? Review { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateRatingDTO
    {
        public int RatedEntityId { get; set; }
        public string EntityType { get; set; } = null!;
        public int Score { get; set; }
        public string? Review { get; set; }
    }

    public class RatingAggregateDTO
    {
        public int EntityId { get; set; }
        public string EntityType { get; set; } = null!;
        public double AverageRating { get; set; }
        public int TotalRatings { get; set; }
    }
}
