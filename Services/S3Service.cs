//using Amazon;
//using Amazon.Runtime;
//using Amazon.S3;

//namespace Streamflix.Services
//{
//    public class S3Service
//    {
//        private readonly AmazonS3Client _s3Client;

//        public S3Service()
//        {
//            // Use the default credentials from AWS Academy Lab environment
//            var credentials = FallbackCredentialsFactory.GetCredentials();
//            _s3Client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);
//        }

//        public async Task ListBucketsAsync()
//        {
//            var response = await _s3Client.ListBucketsAsync();
//            foreach (var bucket in response.Buckets)
//            {
//                Console.WriteLine(bucket.BucketName);
//            }
//        }
//    }
//}
