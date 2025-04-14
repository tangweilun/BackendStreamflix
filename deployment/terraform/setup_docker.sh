#!/bin/bash
# setup_docker.sh

# Update and install dependencies
apt update -y
apt install -y docker.io docker-compose nginx git unzip jq

# Start and enable services
systemctl start docker
systemctl enable docker
systemctl start nginx
systemctl enable nginx

# Configure Docker to run without sudo for the ubuntu user
usermod -aG docker ubuntu
# Create a docker group if it doesn't exist
groupadd -f docker

# Create Docker Compose file
mkdir -p /app
cat > /app/docker-compose.yml << 'EOL'
version: '3'
services:
  streamflix:
    image: ${DOCKER_IMAGE:-streamflix:latest}
    container_name: streamflix
    restart: always
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      # Database connection details will be injected by the deployment script
      - DB_HOST=${DB_HOST}
      - DB_NAME=${DB_NAME}
      - DB_USER=${DB_USER}
      - DB_PASSWORD=${DB_PASSWORD}
      - DB_INSTANCE_TIMESTAMP=${DB_INSTANCE_TIMESTAMP:-$(date +%Y%m%d%H%M%S)}
      # AWS credentials
      - AWS_ACCESS_KEY=${AWS_ACCESS_KEY}
      - AWS_SECRET_KEY=${AWS_SECRET_KEY}
      - AWS_SESSION_TOKEN=${AWS_SESSION_TOKEN}
      - AWS_REGION=${AWS_REGION:-us-east-1}
EOL

# Configure Nginx as reverse proxy
cat > /etc/nginx/sites-available/streamflix << 'EOL'
server {
    listen 80 default_server;
    server_name _;

    location / {
        proxy_pass http://localhost:5000/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Path $request_uri;
    }
}
EOL
# Remove default site if it exists
rm -f /etc/nginx/sites-enabled/default

# Enable Nginx site configuration
ln -sf /etc/nginx/sites-available/streamflix /etc/nginx/sites-enabled/

# Create deployment script with database configuration support
cat > /app/server_deploy.sh << 'EOL'
#!/bin/bash
set -e

# Function to display status messages
log() {
  local level=$1
  local message=$2
  case $level in
    info)  echo -e "\033[0;32m[INFO]\033[0m $message" ;;
    warn)  echo -e "\033[0;33m[WARNING]\033[0m $message" ;;
    error) echo -e "\033[0;31m[ERROR]\033[0m $message" ;;
    *)     echo "$message" ;;
  esac
}

# Generate timestamp for unique RDS instance name if not provided
if [ -z "$DB_INSTANCE_TIMESTAMP" ]; then
  DB_INSTANCE_TIMESTAMP=$(date +%Y%m%d%H%M%S)
  export DB_INSTANCE_TIMESTAMP
  log info "Generated timestamp for RDS instance: $DB_INSTANCE_TIMESTAMP"
fi

# Function to update appsettings.json with database connection string and AWS credentials
update_app_settings() {
  log info "Updating appsettings.json with configuration values"
  
  # Find all appsettings.json files
  find . -name "appsettings*.json" | while read -r config_file; do
    log info "Processing $config_file"
    
    # Check if file exists and is readable
    if [ ! -f "$config_file" ] || [ ! -r "$config_file" ]; then
      log warn "Cannot read $config_file. Skipping."
      continue
    fi
    
    # Create a backup
    cp "$config_file" "${config_file}.bak"
    
    # Update database connection if variables are set
    if [ ! -z "$DB_HOST" ] && [ ! -z "$DB_NAME" ] && [ ! -z "$DB_USER" ] && [ ! -z "$DB_PASSWORD" ]; then
      # Extract host and port from DB_HOST (format: hostname:port)
      DB_HOSTNAME=$(echo $DB_HOST | cut -d: -f1)
      DB_PORT=$(echo $DB_HOST | cut -d: -f2)
      
      # If no port was found, use default PostgreSQL port
      if [ "$DB_HOSTNAME" = "$DB_PORT" ]; then
        DB_PORT="5432"
      fi
      
      # Create PostgreSQL connection string
      CONNECTION_STRING="Host=${DB_HOSTNAME};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true;"
      
      log info "Updating database connection string"
      # Update the connection string in the config file
      TMP_FILE=$(mktemp)
      jq --arg conn "$CONNECTION_STRING" '.connectionstrings.dbconnection = $conn' "$config_file" > "$TMP_FILE"
      mv "$TMP_FILE" "$config_file"
    else
      log warn "Database environment variables not set. Skipping connection string update."
    fi
    
    # Update AWS credentials if variables are set
    if [ ! -z "$AWS_ACCESS_KEY" ] && [ ! -z "$AWS_SECRET_KEY" ] && [ ! -z "$AWS_SESSION_TOKEN" ]; then
      log info "Updating AWS credentials"
      
      TMP_FILE=$(mktemp)
      jq --arg accesskey "$AWS_ACCESS_KEY" \
         --arg secretkey "$AWS_SECRET_KEY" \
         --arg sessiontoken "$AWS_SESSION_TOKEN" \
         --arg region "${AWS_REGION:-us-east-1}" \
         '.aws.accesskey = $accesskey | .aws.secretkey = $secretkey | .aws.sessiontoken = $sessiontoken | .aws.region = $region' \
         "$config_file" > "$TMP_FILE"
      mv "$TMP_FILE" "$config_file"
    else
      log warn "AWS credentials not set. Skipping AWS configuration update."
    fi
    
    log info "Updated configuration in $config_file"
  done
}

# Function to run database migrations
run_migrations() {
  log info "Running database migrations"
  
  # Check if database environment variables are set
  if [ -z "$DB_HOST" ] || [ -z "$DB_NAME" ] || [ -z "$DB_USER" ] || [ -z "$DB_PASSWORD" ]; then
    log error "Database environment variables not set. Cannot run migrations."
    return 1
  fi
  
  # Install EF Core tools if not already installed
  if ! command -v dotnet-ef &> /dev/null; then
    log info "Installing Entity Framework Core tools..."
    dotnet tool install --global dotnet-ef
    export PATH="$PATH:$HOME/.dotnet/tools"
  fi
  
  # Create migrations directory if it doesn't exist
  mkdir -p Migrations
  
  # Set the connection string environment variable for EF Core
  DB_HOSTNAME=$(echo $DB_HOST | cut -d: -f1)
  DB_PORT=$(echo $DB_HOST | cut -d: -f2)
  
  # If no port was found, use default PostgreSQL port
  if [ "$DB_HOSTNAME" = "$DB_PORT" ]; then
    DB_PORT="5432"
  fi
  
  # Create PostgreSQL connection string
  CONNECTION_STRING="Host=${DB_HOSTNAME};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true;"
  export ConnectionStrings__DefaultConnection="$CONNECTION_STRING"
  
  # Run migrations
  log info "Adding initial migration..."
  if ! dotnet ef migrations add InitialCreate --context ApplicationDbContext; then
    log warn "Migration may already exist. Continuing..."
  fi
  
  log info "Updating database..."
  dotnet ef database update --context ApplicationDbContext
  
  log info "Database migrations completed successfully"
}

# Create Dockerfile if not exists
if [ ! -f Dockerfile ]; then
  log info "Creating Dockerfile"
  cat > Dockerfile << 'DOCKERFILE'
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"

# Copy csproj and restore dependencies
COPY *.csproj ./ 
RUN dotnet restore

# Copy and publish app and libraries
COPY . ./ 
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out . 
ENV ASPNETCORE_URLS=http://+:5000 
EXPOSE 5000

ENTRYPOINT ["dotnet", "Streamflix.dll"]
DOCKERFILE
fi

# Update appsettings.json with database connection string and AWS credentials
update_app_settings

# Run database migrations before building the Docker image
log info "Checking if we should run migrations..."
if [ ! -z "$DB_HOST" ] && [ ! -z "$DB_NAME" ] && [ ! -z "$DB_USER" ] && [ ! -z "$DB_PASSWORD" ]; then
  run_migrations
else
  log warn "Database environment variables not set. Skipping migrations."
fi

# Build the Docker image
log info "Building Docker image"
docker build -t streamflix:latest .

# Stop existing container if running
log info "Stopping existing containers"
docker-compose down || true

# Start the container with environment variables
log info "Starting container with configuration"
docker-compose up -d

# Check container status
log info "Container status:"
docker ps

# Reload Nginx
log info "Reloading Nginx"
systemctl reload nginx

log info "Deployment completed successfully!"
EOL

chmod +x /app/server_deploy.sh

# Reload Nginx configuration
systemctl reload nginx

# Create instructions file
cat > /home/$USER/README.txt << 'EOL'
# Streamflix Deployment Instructions

To deploy your .NET application:

1. Upload your code to /app directory
2. Set environment variables (if needed):
   # Database configuration
   export DB_HOST="your-db-host:5432"
   export DB_NAME="your-db-name"
   export DB_USER="your-db-user"
   export DB_PASSWORD="your-db-password"
   
   # AWS credentials
   export AWS_ACCESS_KEY="your-aws-access-key"
   export AWS_SECRET_KEY="your-aws-secret-key"
   export AWS_SESSION_TOKEN="your-aws-session-token"
   export AWS_REGION="us-east-1"

3. Run the deployment script:
   cd /app
   sudo -E ./server_deploy.sh

Your application will be accessible at:
- http://<public-ip>

EOL

chown $USER:$USER /home/$USER/README.txt

# Output completion message
echo "Server setup completed successfully!"