# Streamflix - Video Streaming Platform Backend

## 🚀 Overview

Streamflix is a comprehensive backend system for a video streaming platform, designed to deliver a robust and scalable viewing experience. It handles everything from user authentication and management to video processing, storage, and delivery, leveraging cloud technologies for high availability and performance. This project demonstrates proficiency in full-stack .NET development, cloud infrastructure management on AWS, containerization with Docker, and Infrastructure as Code (IaC) with Terraform.

## ✨ Features

- **User Management:**
  - Secure user registration and login (`AuthController.Register()`, `AuthController.Login()`).
  - JWT-based authentication and authorization.
  - Password reset functionality via email (`AuthController.ForgotPassword()`, `AuthController.ResetPassword()`).
  - User profile management (`UserController.GetUserProfile()`).
- **Admin Capabilities:**
  - Fetch all users (`AdminController.GetAllUsers()`).
  - Update user roles (e.g., grant admin privileges) (`AdminController.ChangeUserRole()`).
- **Video Content Management:**
  - CRUD operations for videos, genres, and actors (`VideosController` class).
  - Categorization of videos by genres.
  - Association of cast members with videos.
- **Video Streaming & Storage:**
  - Integration with AWS S3 for scalable video and thumbnail storage (`FilesController` class).
  - Management of S3 buckets (`BucketsController` class).
  - Functionality to create shows (folders in S3) and upload video files, including multipart uploads for large files.
- **User Experience Features:**
  - Subscription management with Stripe integration for payments (`SubscriptionController` class).
  - Watch history tracking for users (`WatchHistoryController.UpdateProgress()`).
  - Ability for users to mark videos as favorites (`FavoriteVideosController` class).
- **Notifications:**
  - Email notifications for events like registration and password reset using Resend API (inferred from `AuthController` class and `appsettings.json`).

## 🛠️ Tech Stack

- **Backend:** ASP.NET Core 8, C#
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core 8
- **Cloud Platform:** Amazon Web Services (AWS)
  - **Compute:** EC2 (Elastic Compute Cloud)
  - **Storage:** S3 (Simple Storage Service) for video assets
  - **Database:** RDS (Relational Database Service) for PostgreSQL
  - **Networking:** Elastic IP
- **Containerization:** Docker, Docker Compose
- **Web Server/Reverse Proxy:** Nginx (with SSL termination using Let's Encrypt)
- **Infrastructure as Code (IaC):** Terraform
- **Payment Gateway:** Stripe API
- **Email Service:** Resend API
- **Authentication:** JSON Web Tokens (JWT)
- **Deployment Automation:** Bash Scripting (`deploy.sh`)
- **API Documentation:** Swagger/OpenAPI (configured in `Program.cs`)

## 🏗️ Project Structure

The project follows a standard ASP.NET Core Web API structure:

- `Controllers/`: Contains API controllers for handling HTTP requests.
- `Data/`: Includes the `ApplicationDbContext` for database interactions and migrations.
- `DTOs/`: Data Transfer Objects for request/response models.
- `Migrations/`: Entity Framework Core database migrations.
- `Middleware/`: Custom middleware (e.g., error handling).
- `Model/`: Defines the data entities.
- `Services/`: Business logic and service layer components (e.g., `TokenService`, `EmailService`).
- `Properties/`: Project settings, including `launchSettings.json`.
- `deployment/`: Contains all deployment-related scripts and configurations:
  - `docker/`: Dockerfile, docker-compose.yml, Nginx configurations.
  - `terraform/`: Terraform scripts (`main.tf`) for AWS infrastructure provisioning.
  - `scripts/`: Helper scripts (e.g., `setup_docker.sh` for EC2 instance).
  - `deploy.sh`: Master deployment script.
- `appsettings.json`: Configuration file for the application.
- `Program.cs`: Application entry point and service configuration.

## 🚀 Deployment

The application is designed for deployment on AWS using a combination of Terraform, Docker, and a custom deployment script.

1.  **Infrastructure Provisioning (Terraform):**
    - The `deployment/terraform/main.tf` script provisions the necessary AWS resources:
      - EC2 instance for hosting the application.
      - RDS PostgreSQL instance for the database.
      - Security Groups for network traffic control.
      - Elastic IP for a static public IP address.
2.  **Application Deployment (`deploy.sh`):**
    - The `deployment/deploy.sh` script automates the entire deployment process:
      - Prompts for AWS credentials and database password.
      - Initializes and applies Terraform configurations.
      - Retrieves outputs like EC2 public IP and RDS endpoint.
      - Configures DNS (manual step guided by the script).
      - Generates a `.env` file with dynamic configuration for Docker Compose.
      - Clones the application repository onto the EC2 instance.
      - Sets up Nginx and Certbot on the EC2 instance.
      - Obtains SSL certificates from Let's Encrypt.
      - Updates `appsettings.json` on the EC2 instance with actual database and AWS credentials.
      - Copies the final Nginx configuration (with SSL) to the EC2 instance.
      - Builds and starts the application using Docker Compose (`deployment/docker/docker-compose.yml`).
3.  **Containerization (Docker):**
    - The application and its dependencies (like Nginx) are containerized using Docker.
    - The `deployment/docker/Dockerfile` defines the build process for the ASP.NET Core application.
    - `deployment/docker/docker-compose.yml` orchestrates the `api`, `nginx`, and `db` services.
4.  **Reverse Proxy & SSL (Nginx & Certbot):**
    - Nginx (`deployment/docker/nginx/nginx.conf`) acts as a reverse proxy, handling incoming HTTP/HTTPS traffic and forwarding it to the ASP.NET Core application.
    - It also serves static files and handles SSL termination.
    - Certbot is used to automatically obtain and renew SSL certificates from Let's Encrypt, ensuring secure HTTPS communication.

## 💡 Key Technical Skills Demonstrated

- **Full-Stack .NET Development:** Proficient in C# and ASP.NET Core for building robust backend APIs.
- **Cloud Infrastructure Management:** Hands-on experience with AWS services (EC2, S3, RDS, VPC, Security Groups, Elastic IP).
- **Infrastructure as Code (IaC):** Automating infrastructure provisioning and management using Terraform.
- **Containerization & Orchestration:** Utilizing Docker for creating portable application environments and Docker Compose for managing multi-container applications.
- **Database Design & Management:** Working with PostgreSQL and Entity Framework Core for data persistence, including migrations and complex queries.
- **Secure API Development:** Implementing JWT-based authentication, HTTPS, and secure configuration management.
- **Third-Party API Integration:** Integrating with external services like Stripe for payments and Resend for email notifications.
- **DevOps & Automation:**
  - Automated deployment scripting using Bash.
  - Automated SSL certificate management with Certbot.
- **Scalable Architecture Design:** Considerations for scalability by using services like S3 for media storage and RDS for a managed database.
- **Networking & Security:** Configuring Nginx as a reverse proxy, setting up firewalls (Security Groups), and implementing SSL/TLS.
- **Problem Solving:** Designing and implementing a complex system with multiple interconnected components.

## ⚙️ Setup and Running Locally (Conceptual)

While this project is primarily designed for AWS deployment, a conceptual local setup would involve:

1.  **Prerequisites:**
    - .NET 8 SDK
    - Docker Desktop
    - PostgreSQL server (local instance or Docker container)
2.  **Configuration:**
    - Update `appsettings.Development.json` (create if not present) with local PostgreSQL connection string and any other necessary local configurations (e.g., mock API keys for Stripe/Resend if not testing full integration).
3.  **Database:**
    - Ensure PostgreSQL is running.
    - Apply EF Core migrations: `dotnet ef database update`
4.  **Run Application:**
    - `dotnet run` from the `Streamflix` project directory.

**Note:** For full functionality (S3, Stripe, Resend), you would need to configure local alternatives or connect to actual development accounts for these services. The `deploy.sh` script is the recommended way to experience the full application.

## 🌐 API Endpoints

The application exposes various API endpoints for interacting with its features. These are documented using Swagger, which can typically be accessed at `/swagger` on the deployed API URL (e.g., `https://api.streamsflix.online/swagger`).

Key endpoint groups include:

- `/api/auth/*` - Authentication and User Registration
- `/api/admin/*` - Admin functionalities
- `/api/videos/*` - Video management
- `/api/files/*` - S3 file and show management
- `/api/buckets/*` - S3 bucket management
- `/api/subscription/*` - Stripe subscription handling
- `/api/watch-history/*` - User watch history
- `/api/favorite-videos/*` - User favorite videos
- `/api/user/*` - User profile

## 🔮 Future Enhancements

- Implement a full CI/CD pipeline (e.g., using GitHub Actions, Jenkins).
- Add advanced video search and filtering capabilities.
- Develop a recommendation engine based on user preferences and watch history.
- Introduce user reviews and ratings for videos.
- Explore live streaming capabilities.
- Enhance admin dashboard with more analytics and content management tools.
- Implement comprehensive unit and integration tests.

---

This README aims to provide a thorough overview of the Streamflix backend project. For any questions or further details, please feel free to reach out.
