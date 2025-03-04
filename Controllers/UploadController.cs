//using Microsoft.AspNetCore.Mvc;
//using Amazon.S3;
//using Amazon.S3.Model;
//using Microsoft.AspNetCore.Authorization;


//namespace Streamflix.Controllers
//{

//    [Route("api/[controller]")]
//    [ApiController]
//    public class UploadController : ControllerBase
//    {
//        private readonly IAmazonS3 _s3Client;
//        private readonly IConfiguration _configuration;

//        public UploadController(IAmazonS3 s3Client, IConfiguration configuration)
//        {
//            _s3Client = s3Client;
//            _configuration = configuration;
//        }

//        [HttpPost("upload")]
//        public async Task<IActionResult> UploadFile(IFormFile file)
//        {
//            if (file == null || file.Length == 0)
//                return BadRequest("No file was uploaded.");

//            try
//            {
//                // Get bucket name from configuration
//                string bucketName = _configuration["AWS:BucketName"];

//                // Create a unique file name
//                string fileName = $"{Guid.NewGuid()}-{file.FileName}";

//                // Upload file to S3
//                using (var stream = file.OpenReadStream())
//                {
//                    var request = new PutObjectRequest
//                    {
//                        BucketName = bucketName,
//                        Key = fileName,
//                        InputStream = stream,
//                        ContentType = file.ContentType
//                    };

//                    // Set public read access
//                    request.CannedACL = S3CannedACL.PublicRead;

//                    await _s3Client.PutObjectAsync(request);
//                }

//                // Construct the URL to the uploaded file
//                string fileUrl = $"https://{bucketName}.s3.{_configuration["AWS:Region"]}.amazonaws.com/{fileName}";

//                return Ok(new
//                {
//                    success = true,
//                    message = "File uploaded successfully",
//                    fileName = fileName,
//                    fileUrl = fileUrl
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, $"Internal server error: {ex.Message}");
//            }
//        }

//        [HttpPost("upload/multiple")]
//        public async Task<IActionResult> UploadMultipleFiles(List<IFormFile> files)
//        {
//            if (files == null || !files.Any())
//                return BadRequest("No files were uploaded.");

//            try
//            {
//                var uploadResults = new List<object>();
//                string bucketName = _configuration["AWS:BucketName"];

//                foreach (var file in files)
//                {
//                    if (file.Length > 0)
//                    {
//                        // Create a unique file name
//                        string fileName = $"{Guid.NewGuid()}-{file.FileName}";

//                        // Upload file to S3
//                        using (var stream = file.OpenReadStream())
//                        {
//                            var request = new PutObjectRequest
//                            {
//                                BucketName = bucketName,
//                                Key = fileName,
//                                InputStream = stream,
//                                ContentType = file.ContentType,
//                               // CannedACL = S3CannedACL.PublicRead
//                            };

//                            await _s3Client.PutObjectAsync(request);
//                        }

//                        // Construct the URL to the uploaded file
//                        string fileUrl = $"https://{bucketName}.s3.{_configuration["AWS:Region"]}.amazonaws.com/{fileName}";

//                        uploadResults.Add(new
//                        {
//                            originalName = file.FileName,
//                            fileName = fileName,
//                            fileUrl = fileUrl,
//                            contentType = file.ContentType,
//                            size = file.Length
//                        });
//                    }
//                }

//                return Ok(new
//                {
//                    success = true,
//                    message = $"{uploadResults.Count} files uploaded successfully",
//                    files = uploadResults
//                });
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, $"Internal server error: {ex.Message}");
//            }
//        }

//        [HttpDelete("delete/{fileName}")]
//        public async Task<IActionResult> DeleteFile(string fileName)
//        {
//            if (string.IsNullOrEmpty(fileName))
//                return BadRequest("File name is required.");

//            try
//            {
//                string bucketName = _configuration["AWS:BucketName"];

//                var deleteRequest = new DeleteObjectRequest
//                {
//                    BucketName = bucketName,
//                    Key = fileName
//                };

//                await _s3Client.DeleteObjectAsync(deleteRequest);

//                return Ok(new
//                {
//                    success = true,
//                    message = "File deleted successfully"
//                });
//            }
//            catch (AmazonS3Exception ex)
//            {
//                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
//                    return NotFound(new { success = false, message = "File not found" });

//                return StatusCode(500, $"S3 error: {ex.Message}");
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, $"Internal server error: {ex.Message}");
//            }
//        }
//    }

//}
