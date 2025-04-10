using Streamflix.Model;

namespace Streamflix.DTOs
{
    public class ActorDto
    {
        public string Name { get; set; }
        public string Biography { get; set; }
        public DateTime? BirthDate { get; set; }
    }
    public class CreateVideoDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int Duration { get; set; }
        public string MaturityRating { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? ContentUrl { get; set; }

        // New property for genre selection
        public string Genre { get; set; }

        public List<ActorDto> Actors { get; set; } // Add list of actors

    }
    public class UpdateVideoDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public int Duration { get; set; }
        public string MaturityRating { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string ThumbnailUrl { get; set; }
        public string ContentUrl { get; set; }
        public string Genre { get; set; }
        public List<ActorDto> Actors { get; set; }
    }


}
