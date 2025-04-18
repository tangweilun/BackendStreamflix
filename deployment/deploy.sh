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

# Try to remove .terraform; ignore permission denied errors
rm -rf .terraform 2>/dev/null || echo "Warning: Could not delete .terraform directory."

# Remove Terraform state files safely
rm -f terraform.tfstate terraform.tfstate.backup 2>/dev/null || echo "Warning: Could not delete state files."

# Remove the terraform.lock.hcl file to ensure a fresh provider initialization
echo "Removing terraform.lock.hcl file..."
rm -f "${TF_DIR}/.terraform.lock.hcl" 2>/dev/null || echo "Warning: Could not delete terraform.lock.hcl."

echo "Proceeding with terraform init..."
terraform init

# Add execute permissions to Terraform providers (fix for WSL/filesystem issues)
echo "Setting execute permissions for Terraform providers..."
find .terraform/providers -type f -name 'terraform-provider-*' -exec chmod +x {} \;

echo "Applying Terraform configuration..."
terraform apply -var="aws_region=${AWS_REGION}" -var="db_password=${DB_PASSWORD}" -auto-approve

# Get outputs
echo "Retrieving Terraform outputs..."
EC2_PUBLIC_IP=$(terraform output -raw public_ip)
RDS_ENDPOINT=$(terraform output -raw db_endpoint)
DB_NAME_OUTPUT=$(terraform output -raw db_name)         # Get DB Name output
DB_USERNAME_OUTPUT=$(terraform output -raw db_username) # Get DB Username output
DB_PORT_OUTPUT="5432" # Standard PostgreSQL port

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
AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}
AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}
AWS_SESSION_TOKEN=${AWS_SESSION_TOKEN}
AWS_REGION=${AWS_REGION}
EOF
echo ".env file created at ${ENV_FILE_PATH}"

# Copy Docker files (including the generated .env) to EC2 instance
# Create nginx config directory locally first (if needed, though nginx.conf is generated below)
mkdir -p "${DOCKER_DIR}/nginx"

# Create nginx config file locally
cat > "${DOCKER_DIR}/nginx/nginx.conf" << EOF
    server {
        listen        80 default_server;
        server_name streamflix.us-east-1.elasticbeanstalk.com;
        access_log    /var/log/nginx/access.log main;

        client_header_timeout 60;
        client_body_timeout   60;
        keepalive_timeout     60;
        gzip                  off;
        gzip_comp_level       4;
        gzip_types text/plain text/css application/json application/javascript application/x-javascript text/xml application/xml application/xml+rss text/javascript;
        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
            allow all;
        }

        # Include the Elastic Beanstalk generated locations
        include conf.d/elasticbeanstalk/*.conf;
    }
     # HTTPS server
    server {
        listen 443 ssl;
        server_name streamflix.us-east-1.elasticbeanstalk.com;

        ssl_certificate /etc/letsencrypt/live/streamflix.us-east-1.elasticbeanstalk.com/fullchain.pem;
        ssl_certificate_key /etc/letsencrypt/live/streamflix.us-east-1.elasticbeanstalk.com/privkey.pem;

        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers HIGH:!aNULL:!MD5;

	# Security headers (optional but recommended)
        add_header Strict-Transport-Security "max-age=63072000; includeSubDomains; preload" always;
        add_header X-Frame-Options DENY;
        add_header X-Content-Type-Options nosniff;

        # Main proxy or app location
        location / {
            proxy_pass http://localhost:5000;  # Change this port if your app listens elsewhere
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection 'upgrade';
            proxy_set_header Host $host;
            proxy_cache_bypass $http_upgrade;
        }
    }
EOF
echo "Create the /var/www/certbot directory if it doesn't exist"
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" \
  "sudo mkdir -p /var/www/certbot && sudo chmod 755 /var/www/certbot"

echo "Installing git and Certbot..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" \
  "sudo apt-get update && sudo apt-get install -y git certbot python3-certbot-nginx"

echo "Obtaining SSL certificate using webroot method..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" \
  "sudo certbot certonly --webroot -w /var/www/certbot -d streamflix.us-east-1.elasticbeanstalk.com --non-interactive --agree-tos -m your-email@example.com"

echo "Reloading NGINX..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" \
  "sudo systemctl reload nginx"

# Clone the repository (replace with actual repo URL)
ssh -i "${KEY_PATH}" \
    -o StrictHostKeyChecking=no \
    ubuntu@${EC2_PUBLIC_IP} \
    "git clone https://github.com/tangweilun/BackendStreamflix.git /home/ubuntu/app"

    
# Create nginx directory in app directory
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" "mkdir -p /home/ubuntu/app/nginx"

# Copy the .env file separately since it's generated dynamically
scp -i "${KEY_PATH}" -o StrictHostKeyChecking=no \
    "${DOCKER_DIR}/.env" \
    "ubuntu@${EC2_PUBLIC_IP}:/home/ubuntu/app/deployment/docker"

# Copy nginx config separately
scp -i "${KEY_PATH}" -o StrictHostKeyChecking=no \
    "${DOCKER_DIR}/nginx/nginx.conf" \
    "ubuntu@${EC2_PUBLIC_IP}:/home/ubuntu/app/nginx/"

echo "Starting Docker Compose on EC2 instance..."
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Start Docker Compose
cd /home/ubuntu/app/deployment/docker
sudo docker compose up -d --remove-orphans
EOF

echo "Deployment complete!"
echo "Your API is available at: http://${EC2_PUBLIC_IP}"
echo "PostgreSQL RDS is available at: ${RDS_ENDPOINT}"
