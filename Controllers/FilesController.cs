using Amazon.S3.Model;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Streamflix.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;

namespace Streamflix.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("AllowSpecificOrigin")]
    public class FilesController : ControllerBase
    {
        private readonly IAmazonS3 _s3Client;
        public FilesController(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        // Function 1: Create a Show (Folder)
        [HttpPost("create-show")]
        [EnableCors("AllowSpecificOrigin")]
        public async Task<IActionResult> CreateShowAsync(string bucketName, [FromForm] string showId, [FromForm] IFormFile? thumbnail)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");

            // Creating an empty object to represent the folder
            string folderKey = $"shows/{showId}/";

            var putRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = folderKey,
                ContentBody = "" // Empty body since S3 does not have real folders
            };
            await _s3Client.PutObjectAsync(putRequest);

            // Handle thumbnail upload
            if (thumbnail != null && thumbnail.Length > 0)
            {
                string thumbnailKey = $"shows/{showId}/thumbnail{Path.GetExtension(thumbnail.FileName)}";

                using var stream = thumbnail.OpenReadStream();
                var thumbnailRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = thumbnailKey,
                    InputStream = stream,
                    ContentType = thumbnail.ContentType // Preserve original content type
                };
                await _s3Client.PutObjectAsync(thumbnailRequest);
            }

            return Ok($"Show '{showId}' created successfully!");
        }

        [HttpGet("list-shows")]
        [EnableCors("AllowSpecificOrigin")]
        public async Task<IActionResult> ListShowsAsync(string bucketName)
        {
            try
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = "shows/", // List only folders inside "shows/"
                    Delimiter = "/" // Treat folders as separate items
                };

                var response = await _s3Client.ListObjectsV2Async(request);
                var shows = new List<object>();

                foreach (var item in response.CommonPrefixes) // Extracts only folders (show IDs)
                {
                    string folderName = item.Replace("shows/", "").Trim('/'); // Extract showId
                    string thumbnailKey = null;

                    // Find the actual thumbnail file (any format)
                    var thumbnailResponse = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
                    {
                        BucketName = bucketName,
                        Prefix = $"shows/{folderName}/" // List all files in the folder
                    });

                    foreach (var obj in thumbnailResponse.S3Objects)
                    {
                        if (obj.Key.Contains("thumbnail"))
                        {
                            thumbnailKey = obj.Key;
                            break;
                        }
                    }

                    string thumbnailUrl = thumbnailKey != null
                        ? $"https://{bucketName}.s3.amazonaws.com/{thumbnailKey}"
                        : "/placeholder.svg"; // Fallback image if no thumbnail exists

                    // Add to list
                    shows.Add(new
                    {
                        id = Guid.NewGuid(), // Unique ID for frontend use
                        title = folderName, // Use folder name as title
                        thumbnail = thumbnailUrl,
                        episodeCount = 0, // Default value (update later)
                        seasons = 1, // Default value
                        lastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd")
                    });
                }

                return Ok(shows);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving shows: {ex.Message}");
            }
        }


        [HttpGet("get-show-thumbnail")]
        [EnableCors("AllowSpecificOrigin")]
        public async Task<IActionResult> GetShowThumbnail(string bucketName, string showId)
        {
            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = $"shows/{showId}/thumbnail"
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest);
            var thumbnailObject = listResponse.S3Objects.FirstOrDefault();

            if (thumbnailObject != null)
            {
                var urlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = thumbnailObject.Key,
                    Expires = DateTime.UtcNow.AddMinutes(5)
                };
                string url = _s3Client.GetPreSignedURL(urlRequest);
                return Ok(new { ThumbnailUrl = url });
            }

            return NotFound("No thumbnail found for this show.");
        }




        // Function 2: Upload an Episode to a Show
        [HttpPost("upload-episode")]
        [EnableCors("AllowSpecificOrigin")]
        public async Task<IActionResult> UploadEpisodeAsync(string bucketName, string showId, int episodeNumber, IFormFile file)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");
            if (file == null || file.Length == 0) return BadRequest("Invalid file.");

            // Extract the original file extension
            string fileExtension = Path.GetExtension(file.FileName);
            string key = $"shows/{showId}/episodes/{episodeNumber}{fileExtension}";

            using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType
            };

            await _s3Client.PutObjectAsync(request);
            return Ok($"Episode {episodeNumber} uploaded successfully to Show '{showId}' as {key}!");
        }


        // Existing Function: Upload any File
        [HttpPost]
        public async Task<IActionResult> UploadFileAsync(IFormFile file, string bucketName, string? prefix)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");

            var key = string.IsNullOrEmpty(prefix) ? file.FileName : $"{prefix?.TrimEnd('/')}/{file.FileName}";

            using var stream = file.OpenReadStream();
            var request = new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType
            };

            await _s3Client.PutObjectAsync(request);
            return Ok($"File {key} uploaded to S3 successfully!");
        }

        //Existing Function: Get All Files
        [HttpGet]
        public async Task<IActionResult> GetAllFilesAsync(string bucketName, string? prefix)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");

            var request = new ListObjectsV2Request()
            {
                BucketName = bucketName,
                Prefix = prefix
            };

            var result = await _s3Client.ListObjectsV2Async(request);
            var s3Objects = result.S3Objects.Select(s =>
            {
                var urlRequest = new GetPreSignedUrlRequest()
                {
                    BucketName = bucketName,
                    Key = s.Key,
                    Expires = DateTime.UtcNow.AddMinutes(1)
                };
                return new S3ObjectDto()
                {
                    Name = s.Key.ToString(),
                    PresignedUrl = _s3Client.GetPreSignedURL(urlRequest),
                };
            });
            return Ok(s3Objects);
        }

        //Existing Function: Preview File
        [HttpGet("preview")]
        [EnableCors("AllowSpecificOrigin")]

        public async Task<IActionResult> GetFileByKeyAsync(string bucketName, string key)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");

            var s3Object = await _s3Client.GetObjectAsync(bucketName, key);
            return File(s3Object.ResponseStream, s3Object.Headers.ContentType);
        }

        // Existing Function: Delete File
        [HttpDelete]
        public async Task<IActionResult> DeleteFileAsync(string bucketName, string key)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");

            await _s3Client.DeleteObjectAsync(bucketName, key);
            return NoContent();
        }
    }
}
