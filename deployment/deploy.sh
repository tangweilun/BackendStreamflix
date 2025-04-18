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
NEXT_PUBLIC_API_URL=http://${EC2_PUBLIC_IP}/api
EOF
echo ".env file created at ${ENV_FILE_PATH}"

# Create Next.js environment file
NEXT_ENV_FILE="${DOCKER_DIR}/.env"
cat > "${NEXT_ENV_FILE}" << EOF
NEXT_PUBLIC_API_URL=http://${EC2_PUBLIC_IP}/api
JWT_SECRET_KEY=gT1oV8xJdPb0x8Gb3XyXzjUG5KvRl9+BfayGmB/L7F1UJZnZPxxLQlVfdGzR5d1hQek0bsDzQy7VudZnFtzz5w==
NEXT_PUBLIC_S3_BUCKET_HOSTNAME=streamflixbucket.s3.amazonaws.com
NEXT_PUBLIC_S3_BUCKET_NAME=streamflixbucket

EOF
echo "Next.js .env file created at ${NEXT_ENV_FILE}"

# Copy the Next.js env file to the EC2 instance
scp -i "${KEY_PATH}" -o StrictHostKeyChecking=no \
    "${NEXT_ENV_FILE}" \
    "ubuntu@${EC2_PUBLIC_IP}:/home/ubuntu/frontend/.env"
# Copy Docker files (including the generated .env) to EC2 instance
# Create nginx config directory locally first (if needed, though nginx.conf is generated below)
mkdir -p "${DOCKER_DIR}/nginx"

# Create nginx config file locally
cat > "${DOCKER_DIR}/nginx/nginx.conf" << EOF
server {
    listen 80 default_server;
    server_name _;
    access_log /var/log/nginx/access.log main;

    # Frontend Next.js app
    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }

    # Backend API
    location /api/ {
        proxy_pass http://api:5000/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
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
    
ssh -i "${KEY_PATH}" \
    -o StrictHostKeyChecking=no \
    ubuntu@${EC2_PUBLIC_IP} \
    "git clone https://github.com/tangweilun/Streamflix.git /home/ubuntu/frontend"

# Setup Next.js frontend
ssh -i "${KEY_PATH}" -o StrictHostKeyChecking=no "ubuntu@${EC2_PUBLIC_IP}" << 'EOF'
# Navigate to frontend directory
cd /home/ubuntu/frontend

# Install Node.js and npm if not already installed
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
sudo apt-get install -y nodejs

# Install dependencies and build the Next.js app
npm install
npm run build

# Install PM2 to manage the Next.js process
sudo npm install -g pm2

# Start the Next.js app with PM2
pm2 start npm --name "nextjs-frontend" -- start
pm2 save
pm2 startup
sudo env PATH=$PATH:/usr/bin pm2 startup systemd -u ubuntu --hp /home/ubuntu
EOF
    
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
echo "Your frontend is available at: http://${EC2_PUBLIC_IP}"
echo "Your API is available at: http://${EC2_PUBLIC_IP}/api"
echo "PostgreSQL RDS is available at: ${RDS_ENDPOINT}"
