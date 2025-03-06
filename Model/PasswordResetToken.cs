namespace Streamflix.Model
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    namespace Streamflix.Model
    {
        public class PasswordResetToken
        {
            [Key]
            public int Id { get; set; }

            [Required]
            public string Token { get; set; }

            [Required]
            public DateTime ExpiryDate { get; set; }

            [Required]
            public int UserId { get; set; }

            [ForeignKey("UserId")]
            public User User { get; set; }
        }
    }
}
