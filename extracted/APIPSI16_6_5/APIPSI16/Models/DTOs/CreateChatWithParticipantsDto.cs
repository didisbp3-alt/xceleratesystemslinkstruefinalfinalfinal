namespace APIPSI16.Models.DTOs
{
    public class CreateChatWithParticipantsDto
    {
        public List<int> ParticipantIds { get; set; } = new();
        public string? ChatName { get; set; }
    }
}
