#!/bin/bash
# setup_docker.sh

# Update and install dependencies
apt update -y
apt install -y docker.io nginx git

# Start and enable services
systemctl start docker
systemctl enable docker
systemctl start nginx
systemctl enable nginx

# Configure Docker to run without sudo for the user
usermod -aG docker $USER

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
EOL

# Configure Nginx as reverse proxy
cat > /etc/nginx/sites-available/streamflix << 'EOL'
server {
    listen 80;
    server_name streamflix.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80 default_server;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
EOL

# Enable Nginx site configuration
ln -s /etc/nginx/sites-available/streamflix /etc/nginx/sites-enabled/

# Create deployment script
cat > /app/server_deploy.sh << 'EOL'
#!/bin/bash

# Create Dockerfile if not exists
if [ ! -f Dockerfile ]; then
  cat > Dockerfile << 'DOCKERFILE'
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

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

# Build the Docker image
docker build -t streamflix:latest .

# Stop existing container if running
docker-compose down

# Start the container
docker-compose up -d

# Check container status
docker ps

# Reload Nginx
systemctl reload nginx

echo "Deployment completed successfully!"
EOL

chmod +x /app/server_deploy.sh

# Reload Nginx configuration
systemctl reload nginx

# Create instructions file
cat > /home/$USER/README.txt << 'EOL'
# Streamflix Deployment Instructions

To deploy your .NET application:

1. Upload your code to /app directory
2. Run the deployment script:

   cd /app
   sudo ./server_deploy.sh

Your application will be accessible at:
- http://<public-ip>

EOL

chown $USER:$USER /home/$USER/README.txt

# Output completion message
echo "Server setup completed successfully!"
