using System.ComponentModel.DataAnnotations;
namespace Streamflix.Model
{
    public class User
    {
        [Key]  // Primary Key
        public int Id { get; set; }

        [Required]  // Field is required
        public string UserName { get; set; }

        [Required]
        [EmailAddress]  // Ensures valid email format
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public DateTime DateOfBirth { get; set; }

        [Required]
        public string PhoneNumber { get; set; }
    }
}
