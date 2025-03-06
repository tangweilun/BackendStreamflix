namespace Streamflix.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
    }
}
