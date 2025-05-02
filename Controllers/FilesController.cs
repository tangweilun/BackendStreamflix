using Amazon.S3.Model;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Streamflix.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.EntityFrameworkCore;
using Streamflix.DTOs;

namespace Streamflix.Controllers
{
    [Route("api/files")]
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



        [DisableRequestSizeLimit]
        [HttpPost("upload-episode")]
        public async Task<IActionResult> UploadEpisodeAsync(
        [FromForm] string bucketName,
        [FromForm] string showTitle, // Receive showTitle instead of showId
        [FromForm] int episodeNumber,
        [FromForm] IFormFile file)
        {
            // 🏷️ Create Correct S3 Path
            string fileExtension = Path.GetExtension(file.FileName);
            string key = $"shows/{showTitle}/episodes/{episodeNumber}{fileExtension}";

            using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType
            };

            await _s3Client.PutObjectAsync(request);
            return Ok($"Episode {episodeNumber} uploaded successfully to Show '{showTitle}' as {key}!");
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
        public async Task<IActionResult> GetFileByKeyAsync(string bucketName, string key)
        {
            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists) return NotFound($"Bucket {bucketName} does not exist.");

            var s3Object = await _s3Client.GetObjectAsync(bucketName, key);
            return File(s3Object.ResponseStream, s3Object.Headers.ContentType);
        }
        [HttpGet("watch")]
        public async Task<IActionResult> WatchVideoAsync(string showName)
        {
            string bucketName = "streamflixbuckettest";
            string[] extensions = { ".mp4", ".mov", ".webm" }; // Add any other supported formats

            var episodeList = new List<object>();

            // List all objects (episodes) for the given show
            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = $"shows/{showName}/episodes/"
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest);

            if (listResponse.S3Objects.Count == 0)
            {
                return NotFound("No episodes found for the show.");
            }

            foreach (var s3Object in listResponse.S3Objects)
            {
                // Check if the file has a supported extension
                foreach (var ext in extensions)
                {
                    if (s3Object.Key.EndsWith(ext))
                    {
                        // Generate pre-signed URL for the episode
                        var urlRequest = new GetPreSignedUrlRequest
                        {
                            BucketName = bucketName,
                            Key = s3Object.Key,
                            Expires = DateTime.UtcNow.AddMinutes(5)
                        };

                        var presignedUrl = _s3Client.GetPreSignedURL(urlRequest);

                        episodeList.Add(new
                        {
                            episode = s3Object.Key.Split('/').Last().Replace(ext, ""),
                            url = presignedUrl
                        });
                        break;
                    }
                }
            }

            return Ok(new
            {
                show = showName,
                episodes = episodeList
            });
        }
        // Existing Function: Delete File
        [HttpDelete]
        public async Task<IActionResult> DeletePrefixAsync(string bucketName, string prefix)
        {
            if (!prefix.StartsWith("shows/"))
            {
                prefix = "shows/" + prefix;
            }

            var bucketExists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, bucketName);
            if (!bucketExists)
            {
                return NotFound($"Bucket {bucketName} does not exist.");
            }

            var listRequest = new ListObjectsV2Request()
            {
                BucketName = bucketName,
                Prefix = prefix
            };

            var listResult = await _s3Client.ListObjectsV2Async(listRequest);
            if (listResult.S3Objects.Count == 0)
            {
                return NotFound($"No objects found with prefix '{prefix}'.");
            }

            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName,
                Objects = listResult.S3Objects.Select(s => new KeyVersion { Key = s.Key }).ToList()
            };

            try
            {
                var deleteResult = await _s3Client.DeleteObjectsAsync(deleteRequest);

                if (deleteResult.DeleteErrors.Count > 0)
                {
                    return BadRequest($"Error deleting objects: {string.Join(", ", deleteResult.DeleteErrors.Select(e => e.Key))}");
                }

                return Ok($"Prefix '{prefix}' and all its objects have been deleted successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
