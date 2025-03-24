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
        public async Task<IActionResult> CreateShowAsync(string bucketName, string showId, IFormFile? thumbnail)
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
                string thumbnailExtension = Path.GetExtension(thumbnail.FileName);
                if (thumbnailExtension != ".jpg" && thumbnailExtension != ".png" && thumbnailExtension != ".jpeg")
                {
                    return BadRequest("Invalid thumbnail format. Only JPG, PNG, or JPEG allowed.");
                }

                string thumbnailKey = $"shows/{showId}/thumbnail{thumbnailExtension}";
                using var stream = thumbnail.OpenReadStream();
                var thumbnailRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = thumbnailKey,
                    InputStream = stream,
                    ContentType = thumbnail.ContentType
                };
                await _s3Client.PutObjectAsync(thumbnailRequest);
            }
            return Ok($"Show '{showId}' created successfully!");
        }
        // Function to get show details including thumbnail URL
        [HttpGet("get-show-thumbnail")]
        [EnableCors("AllowSpecificOrigin")]
        public IActionResult GetShowThumbnail(string bucketName, string showId)
        {
            string[] possibleExtensions = { ".jpg", ".jpeg", ".png" };

            foreach (var ext in possibleExtensions)
            {
                string thumbnailKey = $"shows/{showId}/thumbnail{ext}";
                try
                {
                    var urlRequest = new GetPreSignedUrlRequest()
                    {
                        BucketName = bucketName,
                        Key = thumbnailKey,
                        Expires = DateTime.UtcNow.AddMinutes(5)
                    };
                    string url = _s3Client.GetPreSignedURL(urlRequest);
                    return Ok(new { ThumbnailUrl = url });
                }
                catch (AmazonS3Exception)
                {
                    continue; // Try the next extension
                }
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
