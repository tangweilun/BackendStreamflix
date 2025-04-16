namespace Streamflix.DTOs
{
    public class UserProfileDTO
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string DateOfBirth { get; set; }
        public string PhoneNumber { get; set; }
        public string RegisteredOn { get; set; }
        public string SubscribedPlan { get; set; }
    }
}
