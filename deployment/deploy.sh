#!/bin/bash
set -e

# Directory paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TF_DIR="${SCRIPT_DIR}/terraform"
DOCKER_DIR="${SCRIPT_DIR}/docker"
SSH_DIR="${HOME}/.ssh/flix"  # Set this to the dynamic path for the key
KEY_FILE="vockey.pem"
KEY_PATH="${SSH_DIR}/${KEY_FILE}"
APP_PATH="$(dirname "$SCRIPT_DIR")"
echo "SCRIPT_DIR: $SCRIPT_DIR"
echo "TF_DIR: $TF_DIR"
echo "DOCKER_DIR: $DOCKER_DIR"
echo "SSH_DIR: $SSH_DIR"
echo "KEY_FILE: $KEY_FILE"
echo "KEY_PATH: $KEY_PATH"
echo "APP_PATH: $APP_PATH"
echo ""
# AWS credentials prompt
echo "Please enter your AWS credentials for your academic account:"
read -p "AWS Access Key ID: " AWS_ACCESS_KEY_ID
read -p "AWS Secret Access Key: " AWS_SECRET_ACCESS_KEY
echo
read -p "AWS Session Token: " AWS_SESSION_TOKEN
echo

# Hardcode AWS region
AWS_REGION="us-east-1"

# Database password prompt
read -p "Enter a password for the PostgreSQL database(admin123): " DB_PASSWORD

# Export AWS credentials
export AWS_ACCESS_KEY_ID
export AWS_SECRET_ACCESS_KEY
export AWS_SESSION_TOKEN
export AWS_REGION

# Ensure .ssh/flix directory exists
mkdir -p "${SSH_DIR}"

# Check if the source key exists before moving
SOURCE_KEY_PATH="${SCRIPT_DIR}/${KEY_FILE}"
if [ ! -f "${SOURCE_KEY_PATH}" ]; then
    echo "Error: Private key file not found at ${SOURCE_KEY_PATH}"
    echo "Please ensure ${KEY_FILE} is in the ${SCRIPT_DIR} directory."
    exit 1 # Exit if the key is missing
fi

# Move SSH key to the correct location (if not already present)
if [ ! -f "${KEY_PATH}" ]; then
    echo "Moving SSH key to ${KEY_PATH}..."
    mv "${SOURCE_KEY_PATH}" "${KEY_PATH}"
fi

# Set the correct permissions for the SSH key (only accessible by the user)
chmod 600 "${KEY_PATH}"

# Initialize and apply Terraform
cd "${TF_DIR}"
echo "Attempting to clean up previous Terraform state..."

# Try to change ownership of .terraform; ignore errors
sudo chown -R $USER:$USER .terraform 2>/dev/null || true

echo "Proceeding with terraform init..."
terraform init

# Add execute permissions to Terraform providers (fix for WSL/filesystem issues)
echo "Setting execute permissions for Terraform providers..."
find .terraform/providers -type f -name 'terraform-provider-*' -exec chmod +x {} \;

# Ask if user wants to create a new Elastic IP or use existing one
read -p "Create a new Elastic IP? (yes/no, default: yes): " CREATE_EIP
CREATE_EIP=${CREATE_EIP:-yes}

if [[ "${CREATE_EIP,,}" == "yes" ]]; then
  CREATE_EIP_VAR="true"
else
  CREATE_EIP_VAR="false"
fi

# In the terraform apply section, add the new variable
echo "Applying Terraform configuration..."
terraform apply -var="aws_region=${AWS_REGION}" -var="db_password=${DB_PASSWORD}" -var="create_elastic_ip=${CREATE_EIP_VAR}" -auto-approve

# Get outputs
echo "Retrieving Terraform outputs..."
EC2_PUBLIC_IP=$(terraform output -raw public_ip)
RDS_ENDPOINT=$(terraform output -raw db_endpoint)
DB_NAME_OUTPUT=$(terraform output -raw db_name)         # Get DB Name output
DB_USERNAME_OUTPUT=$(terraform output -raw db_username) # Get DB Username output
DB_PORT_OUTPUT="5432" # Standard PostgreSQL port
DOMAIN_NAME=api.streamsflix.online
echo "Waiting for the EC2 instance to be ready..."
sleep 60 # Keep a reasonable wait time

# --- Generate .env file dynamically ---
echo "Generating .env file for Docker Compose..."
ENV_FILE_PATH="${DOCKER_DIR}/.env"
cat > "${ENV_FILE_PATH}" << EOF
DB_HOST=${RDS_ENDPOINT}
DB_PORT=${DB_PORT_OUTPUT}
DB_NAME=${DB_NAME_OUTPUT}
DB_USERNAME=${DB_USERNAME_OUTPUT}
DB_PASSWORD=${DB_PASSWORD}
AWS_ACCESS_KEY=${AWS_ACCESS_KEY_ID}
AWS_SECRET_KEY=${AWS_SECRET_ACCESS_KEY}
AWS_SESSION_TOKEN=${AWS_SESSION_TOKEN}
AWS_REGION=${AWS_REGION}
DOMAIN_NAME=${DOMAIN_NAME}
EOF
echo ".env file created at ${ENV_FILE_PATH}"

# First clone the repositories to create the directories
echo "Cloning repositories to EC2 instance..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" \
  "sudo apt-get update && sudo apt-get install -y git jq"

ssh -i "${KEY_PATH}" \
    -o StrictHostKeyChecking=no \
    ubuntu@${EC2_PUBLIC_IP} \
    "git clone https://github.com/tangweilun/BackendStreamflix.git /home/ubuntu/app"

# Create required directories and install dependencies on EC2 instance
echo "Setting up directories and installing dependencies on EC2 instance..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Create necessary directories
mkdir -p /home/ubuntu/app/nginx
mkdir -p /home/ubuntu/app/deployment/docker
sudo mkdir -p /var/www/certbot
sudo mkdir -p /etc/letsencrypt/live/api.streamsflix.online

# Install certbot and nginx (needed for certbot's nginx plugin)
sudo apt-get update
sudo apt-get install -y certbot python3-certbot-nginx nginx
EOF

# Copy the .env file for the backend
scp -i "${KEY_PATH}" -o StrictHostKeyChecking=no \
    "${DOCKER_DIR}/.env" \
    "ubuntu@${EC2_PUBLIC_IP}:/home/ubuntu/app/deployment/docker/"

# Set proper permissions for the .env file
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" \
    "chmod 600 /home/ubuntu/app/deployment/docker/.env"

# Use jq to replace environment variables in appsettings.json
echo "Updating appsettings.json with environment variables..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Source the .env file to get environment variables
source /home/ubuntu/app/deployment/docker/.env

# Use jq to replace variables in appsettings.json
jq --arg db_host "$DB_HOST" \
   --arg db_name "$DB_NAME" \
   --arg db_username "$DB_USERNAME" \
   --arg db_password "$DB_PASSWORD" \
   --arg aws_access_key "$AWS_ACCESS_KEY" \
   --arg aws_secret_key "$AWS_SECRET_KEY" \
   --arg aws_session_token "$AWS_SESSION_TOKEN" \
   '.ConnectionStrings.DbConnection = "Host=\($db_host);Database=\($db_name);Username=\($db_username);Password=\($db_password);TrustServerCertificate=true" | 
    .AWS.AccessKey = $aws_access_key | 
    .AWS.SecretKey = $aws_secret_key | 
    .AWS.SessionToken = $aws_session_token' \
   /home/ubuntu/app/appsettings.json > /home/ubuntu/app/appsettings.json.updated

# Backup the original file and replace with the updated one
mv /home/ubuntu/app/appsettings.json /home/ubuntu/app/appsettings.json.bak
mv /home/ubuntu/app/appsettings.json.updated /home/ubuntu/app/appsettings.json

echo "appsettings.json has been updated with actual values from .env file"
EOF

# Generate a temporary nginx configuration file for initial setup
# This configuration doesn't depend on SSL certificates
echo "Creating temporary nginx configuration for certificate generation..."
cat > "${DOCKER_DIR}/nginx/temp-nginx.conf" << EOF
server {
    listen 80 default_server;
    server_name api.streamsflix.online streamsflix.online;
    
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
        allow all;
    }
    
    location / {
        return 200 'Certification in progress';
    }
}
EOF

# Copy temporary nginx config to EC2
scp -i "${KEY_PATH}" -o StrictHostKeyChecking=no \
    "${DOCKER_DIR}/nginx/temp-nginx.conf" \
    "ubuntu@${EC2_PUBLIC_IP}:/home/ubuntu/app/nginx/nginx.conf"

# Configure and start Nginx for certificate generation
echo "Configuring nginx for certificate generation..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Stop any running nginx to free up port 80
sudo systemctl stop nginx || true
sudo docker stop $(sudo docker ps -q --filter "name=nginx") 2>/dev/null || true

# Use the temporary nginx config
sudo cp /home/ubuntu/app/nginx/nginx.conf /etc/nginx/sites-available/default
sudo ln -sf /etc/nginx/sites-available/default /etc/nginx/sites-enabled/default
sudo systemctl start nginx
EOF

# Generate SSL certificates with standalone mode
echo "Generating SSL certificates..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Stop nginx to free port 80 for certbot
sudo systemctl stop nginx

# Run certbot in standalone mode
sudo certbot certonly --standalone \
  --non-interactive \
  --agree-tos \
  --register-unsafely-without-email \
  --domains api.streamsflix.online

# Fix permissions
sudo chmod -R 755 /etc/letsencrypt/{live,archive}
EOF

# Now create the final nginx config that uses SSL
echo "Creating final nginx configuration with SSL..."
cat > "${DOCKER_DIR}/nginx/nginx.conf" << EOF
server {
    listen 80 default_server;
    server_name api.streamsflix.online; 
    access_log /var/log/nginx/access.log main;

    client_header_timeout 60;
    client_body_timeout 60;
    keepalive_timeout 60;
    gzip off;
    gzip_comp_level 4;
    gzip_types text/plain text/css application/json application/javascript application/x-javascript text/xml application/xml application/xml+rss text/javascript;
    
    # Redirect HTTP to HTTPS
    location / {
        return 301 https://\$host\$request_uri;
    }
    
    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
        allow all;
    }

    # Include the Elastic Beanstalk generated locations (if applicable, otherwise remove)
    include conf.d/elasticbeanstalk/*.conf; 
}

# HTTPS server
server {
    listen 443 ssl;
    server_name api.streamsflix.online;

    ssl_certificate /etc/letsencrypt/live/api.streamsflix.online/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.streamsflix.online/privkey.pem;

    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    # Security headers (optional but recommended)
    add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;
    add_header X-Frame-Options DENY;
    add_header X-Content-Type-Options nosniff;

    # Main proxy or app location
    location / {
        proxy_pass http://api:5000; # Proxy to the 'api' service in docker-compose
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }
    
    # Health check endpoint
    location /health {
        access_log off;
        return 200 'healthy';
    }
}
EOF

# Copy final nginx config
scp -i "${KEY_PATH}" -o StrictHostKeyChecking=no \
    "${DOCKER_DIR}/nginx/nginx.conf" \
    "ubuntu@${EC2_PUBLIC_IP}:/home/ubuntu/app/nginx/"

# Starting the application with Docker Compose
echo "Starting Docker Compose on EC2 instance..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Stop nginx to free up port 80 for Docker
sudo systemctl stop nginx

# Ensure proper directories and permissions for Docker volumes
sudo mkdir -p /etc/letsencrypt
sudo mkdir -p /var/www/certbot

# Copy certificates to Docker-accessible location if needed
# (Only needed if Docker containers can't directly access /etc/letsencrypt)

# Start Docker Compose
cd /home/ubuntu/app/deployment/docker
# Make sure the .env file has the correct permissions
chmod 600 .env
# Export environment variables from .env file to ensure they're available
export $(grep -v '^#' .env | xargs)
# Use the -E flag to preserve the environment variables when using sudo
sudo -E docker compose up -d --remove-orphans
EOF

# Setup certbot renewal cron job
echo "Setting up automatic certificate renewal..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Create a renewal script
cat > /home/ubuntu/renew-certs.sh << 'INNER'
#!/bin/bash
# Stop nginx container
sudo docker stop $(sudo docker ps -q --filter "name=nginx") 2>/dev/null || true

# Renew certs
sudo certbot renew --standalone

# Restart nginx container
cd /home/ubuntu/app/deployment/docker
sudo -E docker compose up -d nginx
INNER

# Make the script executable
chmod +x /home/ubuntu/renew-certs.sh

# Add to crontab to run twice daily (standard for certbot)
(crontab -l 2>/dev/null || echo "") | grep -v "renew-certs.sh" | { cat; echo "0 0,12 * * * /home/ubuntu/renew-certs.sh > /home/ubuntu/certbot-renewal.log 2>&1"; } | crontab -
EOF

# Print SSH command and Elastic IP information
echo "=============================================="
echo "Connection Information:"
echo "SSH Command: ssh -i ${KEY_PATH} ubuntu@${EC2_PUBLIC_IP}"
echo "Elastic IP Address: ${EC2_PUBLIC_IP}"
echo "=============================================="

echo "Deployment complete!"
echo "Your API is available at: https://api.streamsflix.online"
echo "Your website is available at: https://streamsflix.online"
echo "PostgreSQL RDS is available at: ${RDS_ENDPOINT}"