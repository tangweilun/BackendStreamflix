#!/bin/bash
# deploy.sh

set -e

echo "====== Starting Streamflix Deployment ======"

# Ensure current directory is the deployment directory
cd "$(dirname "$0")"
DEPLOYMENT_DIR=$(pwd)
PROJECT_ROOT=$(dirname "$DEPLOYMENT_DIR")

# Check for prerequisites
command -v terraform >/dev/null 2>&1 || { echo "Terraform is required but not installed. Aborting."; exit 1; }
command -v aws >/dev/null 2>&1 || { echo "AWS CLI is required but not installed. Aborting."; exit 1; }

# Change to terraform directory
cd "$DEPLOYMENT_DIR/terraform"

# Prompt for AWS Academy credentials
echo "Please enter your AWS Academy credentials:"
read -p "AWS Access Key ID: " AWS_ACCESS_KEY_ID
read -p "AWS Secret Access Key: " AWS_SECRET_ACCESS_KEY
read -p "AWS Session Token: " AWS_SESSION_TOKEN

# Set AWS credentials as environment variables
export AWS_ACCESS_KEY_ID
export AWS_SECRET_ACCESS_KEY
export AWS_SESSION_TOKEN

# Verify credentials
echo "Verifying AWS credentials..."
if ! aws sts get-caller-identity > /dev/null; then
  echo "Invalid AWS credentials. Please check and try again."
  exit 1
fi

echo "Initializing Terraform..."
terraform init

# Try to plan to see if there are any issues
echo "Planning infrastructure changes with Terraform..."
if ! terraform plan -out=tfplan; then
  echo "Terraform plan failed. This might be due to existing resources."
  
  # Ask user if they want to destroy existing resources
  read -p "Would you like to destroy existing resources and start fresh? (y/n): " destroy_choice
  if [[ "$destroy_choice" == "y" ]]; then
    echo "Destroying existing resources..."
    terraform destroy -auto-approve
    echo "Re-planning infrastructure..."
    terraform plan -out=tfplan
  else
    echo "Attempting to continue with existing resources..."
  fi
fi

# Apply Terraform configuration
echo "Deploying infrastructure with Terraform..."
if ! terraform apply -auto-approve; then
  echo "Terraform apply failed. Attempting to troubleshoot..."
  echo "Checking if resources already exist..."
  
  # Check AWS console for existing security group
  if aws ec2 describe-security-groups --group-names streamflix-sg --region us-east-1 >/dev/null 2>&1; then
    echo "Security group 'streamflix-sg' already exists."
    echo "Try running the script again or manually check your AWS resources."
  else
    echo "Infrastructure deployment failed for an unknown reason."
    echo "Please check the AWS console and Terraform logs for more details."
  fi
  exit 1
fi

# Get the EC2 instance IP
echo "Attempting to get public_ip output:"
INSTANCE_IP=$(terraform output -raw public_ip)
if [ -z "$INSTANCE_IP" ]; then
  echo "Error: Failed to get instance IP from Terraform output."
  echo "Check the Terraform state and AWS console."
  exit 1
fi
echo "EC2 instance deployed with IP: $INSTANCE_IP"

# Wait for instance to initialize
echo "Waiting for instance to initialize (this may take a few minutes)..."
sleep 90

# Prepare application package
echo "Preparing application package..."
cd "$PROJECT_ROOT"

# Create a temporary build directory
BUILD_DIR=$(mktemp -d)
cp -r * "$BUILD_DIR"
cp "$DEPLOYMENT_DIR/Dockerfile" "$BUILD_DIR"

# Create a zip of the application
cd "$BUILD_DIR"
zip -r "$DEPLOYMENT_DIR/app.zip" .
cd "$DEPLOYMENT_DIR"

# Check for required files
if [ ! -f "$DEPLOYMENT_DIR/vockey.pem" ]; then
  echo "Error: vockey.pem not found in $DEPLOYMENT_DIR"
  echo "Please place your AWS key file in the deployment directory."
  exit 1
fi


mkdir -p ~/.ssh
#Copy  key file to Linux filesystem (not the Windows filesystem mounted via WSL)
cp "$DEPLOYMENT_DIR/vockey.pem" ~/.ssh/vockey.pem
#Set proper permissions for the key file
chmod 600 ~/.ssh/vockey.pem


# Set proper permissions for the key file


# Wait for SSH to become available
echo "Waiting for SSH to become available..."
echo "Deployment directory: $DEPLOYMENT_DIR"
echo "Key file path: $DEPLOYMENT_DIR/vockey.pem"
echo "Instance IP: $INSTANCE_IP"
SSH_AVAILABLE=false
for i in $(seq 1 10); do
 if ssh -o StrictHostKeyChecking=no -o ConnectTimeout=5 -i ~/.ssh/vockey.pem ubuntu@$INSTANCE_IP echo "SSH is up" > /dev/null 2>&1; then
    SSH_AVAILABLE=true
    break
  fi
  echo "Waiting for SSH... (Attempt $i/10)"
  sleep 15
done

if [ "$SSH_AVAILABLE" = false ]; then
  echo "SSH did not become available. Check the instance status in the AWS console."
  echo "You may need to manually deploy after the instance is ready."
  echo "Upload the app.zip file to the instance and run the deployment script."
  exit 1
fi

# Upload application files
echo "Uploading application files..."
scp -o StrictHostKeyChecking=no -i ~/.ssh/vockey.pem "$DEPLOYMENT_DIR/app.zip" ubuntu@$INSTANCE_IP:/tmp/

# Deploy the application
echo "Deploying the application..."
ssh -o StrictHostKeyChecking=no -i ~/.ssh/vockey.pem ubuntu@$INSTANCE_IP << 'REMOTE_COMMANDS'
  set -e

  # Install unzip if not already installed
  if ! command -v unzip &> /dev/null; then
    echo "Installing unzip..."
    sudo apt update -y
    sudo apt install -y unzip
  fi

  sudo mkdir -p /app
  sudo chown ubuntu:ubuntu /app
  cd /app
  unzip -o /tmp/app.zip -d .
  rm /tmp/app.zip
  
  # Check if server_deploy.sh exists and run it instead of deploy.sh
  if [ -f "./server_deploy.sh" ]; then
    sudo chmod +x ./server_deploy.sh
    sudo ./server_deploy.sh
  else
    echo "Warning: server_deploy.sh not found. Please ensure your deployment includes the correct server setup script."
  fi
REMOTE_COMMANDS

echo "====== Deployment Complete ======"
echo "Your application is now available at: http://$INSTANCE_IP"
echo "SSH command: ssh -i vockey.pem ubuntu@$INSTANCE_IP"

# Clean up temp files
rm -rf "$BUILD_DIR"