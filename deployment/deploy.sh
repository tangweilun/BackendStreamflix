#!/bin/bash
# deploy.sh - Enhanced Streamflix Deployment Script
# This script automates the deployment of the Streamflix application to AWS

# Enable strict error handling
set -eo pipefail

# Color codes for better readability
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color
BOLD='\033[1m'

# Configuration
SSH_KEY_NAME="vockey.pem"
SSH_KEY_PATH=~/.ssh/${SSH_KEY_NAME}
MAX_SSH_RETRIES=15
SSH_RETRY_DELAY=20
TERRAFORM_DIR="terraform"
APP_NAME="streamflix"

# Display banner
echo -e "${BOLD}${GREEN}====== Streamflix Deployment Tool ======${NC}"
echo "Starting deployment: $(date)"

# Function to display status messages
log() {
  local level=$1
  local message=$2
  case $level in
    info)  echo -e "${GREEN}[INFO]${NC} $message" ;;
    warn)  echo -e "${YELLOW}[WARNING]${NC} $message" ;;
    error) echo -e "${RED}[ERROR]${NC} $message" ;;
    *)     echo "$message" ;;
  esac
}

# Function to display steps
step() {
  echo -e "\n${BOLD}${GREEN}➤ $1${NC}"
}

# Function to check for required tools
check_prerequisites() {
  step "Checking prerequisites"
  
  local missing_tools=()
  
  for tool in terraform aws zip unzip; do
    if ! command -v $tool &> /dev/null; then
      missing_tools+=($tool)
    fi
  done
  
  if [ ${#missing_tools[@]} -ne 0 ]; then
    log error "The following required tools are missing: ${missing_tools[*]}"
    log info "Please install the required tools and try again."
    exit 1
  fi
  
  log info "All required tools are installed."
}

# Function to set up directory structure
setup_directories() {
  step "Setting up directories"
  
  # Ensure we're in the deployment directory
  cd "$(dirname "$0")"
  DEPLOYMENT_DIR=$(pwd)
  PROJECT_ROOT=$(dirname "$DEPLOYMENT_DIR")
  
  # Ensure SSH directory exists
  mkdir -p ~/.ssh
  chmod 700 ~/.ssh
  
  log info "Deployment directory: $DEPLOYMENT_DIR"
  log info "Project root: $PROJECT_ROOT"
}

# Function to handle AWS credentials
setup_aws_credentials() {
  step "Setting up AWS credentials"
  
  # Check if credentials are already set
  echo "Please enter your AWS Academy credentials:"
  read -p "AWS Access Key ID: " AWS_ACCESS_KEY_ID
  read -p "AWS Secret Access Key: " AWS_SECRET_ACCESS_KEY
  echo
  read -p "AWS Session Token: " AWS_SESSION_TOKEN
  
  # Export credentials
  export AWS_ACCESS_KEY_ID
  export AWS_SECRET_ACCESS_KEY
  export AWS_SESSION_TOKEN
  
  # Verify credentials
  log info "Verifying AWS credentials..."
  if ! aws sts get-caller-identity &> /dev/null; then
    log error "Invalid AWS credentials. Please check and try again."
    exit 1
  fi
  
  log info "AWS credentials verified successfully."
}

# Function to handle Terraform operations
provision_infrastructure() {
  step "Provisioning infrastructure with Terraform"
  
  # Change to Terraform directory
  cd "$DEPLOYMENT_DIR/$TERRAFORM_DIR"
  
  # Clean up old state
  log info "Cleaning up previous Terraform state..."
  rm -f terraform.tfstate terraform.tfstate.backup tfplan
  rm -rf .terraform .terraform.lock.hcl
  
  # Clear plugin cache if exists
  if [ -d ~/.terraform.d/plugin-cache ]; then
    log info "Clearing Terraform plugin cache..."
    rm -rf ~/.terraform.d/plugin-cache
  fi
  
  # Initialize Terraform
  log info "Initializing Terraform..."
  terraform init
  
  # Check if AWS provider plugin exists and set permissions
  log info "Checking for AWS provider plugin..."
  # Find the AWS provider plugin (version might vary)
  AWS_PROVIDER_PATH=$(find .terraform -name "terraform-provider-aws*" -type f | head -n 1)
  
  if [ -n "$AWS_PROVIDER_PATH" ] && [ -f "$AWS_PROVIDER_PATH" ]; then
    log info "AWS provider plugin found at: $AWS_PROVIDER_PATH"
    log info "Setting executable permissions..."
    chmod +x "$AWS_PROVIDER_PATH"
  else
    log warn "AWS provider plugin not found at expected location. This might cause issues."
  fi
  
  # Create and show execution plan
  log info "Creating Terraform execution plan..."
  terraform plan -out=tfplan
  
  # Apply Terraform configuration
  log info "Applying Terraform configuration..."
  if ! terraform apply -auto-approve tfplan; then
    log error "Terraform apply failed. Attempting to troubleshoot..."
    
    # Check for common AWS Academy permission issues
    if grep -q "UnauthorizedOperation" terraform.tfstate 2>/dev/null; then
      log error "Permission error detected. This is common with AWS Academy accounts."
      log info "Try running the script again with a fresh session or contact your instructor."
    else
      log error "Infrastructure deployment failed for an unknown reason."
      log info "Please check the AWS console and Terraform logs for more details."
    fi
    exit 1
  fi
  
  # Get the EC2 instance IP
  INSTANCE_IP=$(terraform output -raw public_ip 2>/dev/null)
  if [ -z "$INSTANCE_IP" ]; then
    log error "Failed to get instance IP from Terraform output."
    log info "Check the Terraform state and AWS console."
    exit 1
  fi
  
  log info "EC2 instance deployed with IP: $INSTANCE_IP"
  echo "INSTANCE_IP=$INSTANCE_IP" > "$DEPLOYMENT_DIR/.deployment_vars"
}

# Function to prepare the application package
prepare_application() {
  step "Preparing application package"
  
  # Load deployment variables if they exist
  if [ -f "$DEPLOYMENT_DIR/.deployment_vars" ]; then
    source "$DEPLOYMENT_DIR/.deployment_vars"
  fi
  
  # Verify instance IP is available
  if [ -z "$INSTANCE_IP" ]; then
    log error "Instance IP not found. Infrastructure provisioning may have failed."
    exit 1
  fi
  
  # Create a temporary build directory
  BUILD_DIR=$(mktemp -d)
  log info "Created temporary build directory: $BUILD_DIR"
  
  # Copy application files
  log info "Copying application files..."
  cp -r "$PROJECT_ROOT"/* "$BUILD_DIR/"
  
  # Copy Dockerfile if it exists in deployment directory
  if [ -f "$DEPLOYMENT_DIR/Dockerfile" ]; then
    cp "$DEPLOYMENT_DIR/Dockerfile" "$BUILD_DIR/"
  fi
  
  # Create application package
  log info "Creating application package..."
  cd "$BUILD_DIR"
  zip -rq "$DEPLOYMENT_DIR/app.zip" .
  
  log info "Application package created: $DEPLOYMENT_DIR/app.zip"
}

# Function to setup SSH access
setup_ssh_access() {
  step "Setting up SSH access"
  
  # Check for required SSH key
  if [ ! -f "$DEPLOYMENT_DIR/$SSH_KEY_NAME" ]; then
    log error "$SSH_KEY_NAME not found in $DEPLOYMENT_DIR"
    log info "Please place your AWS key file in the deployment directory."
    exit 1
  fi
  
  # Copy and set proper permissions for the key file
  log info "Configuring SSH key..."
  cp "$DEPLOYMENT_DIR/$SSH_KEY_NAME" "$SSH_KEY_PATH"
  chmod 600 "$SSH_KEY_PATH"
  
  # Verify SSH key was copied correctly and has proper permissions
  if [ -f "$SSH_KEY_PATH" ]; then
    log info "SSH key copied successfully to $SSH_KEY_PATH"
    
    # Check permissions
    KEY_PERMS=$(stat -c "%a" "$SSH_KEY_PATH" 2>/dev/null || stat -f "%Lp" "$SSH_KEY_PATH" 2>/dev/null)
    if [ "$KEY_PERMS" = "600" ]; then
      log info "SSH key has correct permissions (600)"
    else
      log warn "SSH key has incorrect permissions: $KEY_PERMS (should be 600)"
      log info "Attempting to fix permissions..."
      chmod 600 "$SSH_KEY_PATH"
    fi
  else
    log error "Failed to copy SSH key to $SSH_KEY_PATH"
    exit 1
  fi
  
  # Wait for SSH to become available
  log info "Waiting for SSH to become available on $INSTANCE_IP..."
  local SSH_AVAILABLE=false
  
  for i in $(seq 1 $MAX_SSH_RETRIES); do
    if ssh -o StrictHostKeyChecking=no -o ConnectTimeout=5 -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP echo "SSH is up" &> /dev/null; then
      SSH_AVAILABLE=true
      log info "SSH connection established successfully."
      break
    fi
    log warn "Waiting for SSH... (Attempt $i/$MAX_SSH_RETRIES)"
    sleep $SSH_RETRY_DELAY
  done
  
  if [ "$SSH_AVAILABLE" = false ]; then
    log error "SSH did not become available after $MAX_SSH_RETRIES attempts."
    log info "Check the instance status in the AWS console."
    log info "You may need to manually deploy after the instance is ready."
    log info "Manual deployment instructions:"
    log info "1. Upload app.zip to the instance: scp -i $SSH_KEY_PATH $DEPLOYMENT_DIR/app.zip ubuntu@$INSTANCE_IP:/tmp/"
    log info "2. SSH into the instance: ssh -i $SSH_KEY_PATH ubuntu@$INSTANCE_IP"
    log info "3. Extract and deploy the application manually"
    exit 1
  fi
}

# Function to deploy the application
deploy_application() {
  step "Deploying application to $INSTANCE_IP"
  
  # Upload application files
  log info "Uploading application package..."
  scp -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" "$DEPLOYMENT_DIR/app.zip" ubuntu@$INSTANCE_IP:/tmp/
  
  # Create remote deployment script
  cat > "$DEPLOYMENT_DIR/remote_deploy.sh" << 'REMOTE_SCRIPT'
#!/bin/bash
set -e

# Color codes for better readability
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to display status messages
log() {
  local level=$1
  local message=$2
  case $level in
    info)  echo -e "${GREEN}[INFO]${NC} $message" ;;
    warn)  echo -e "${YELLOW}[WARNING]${NC} $message" ;;
    error) echo -e "${RED}[ERROR]${NC} $message" ;;
    *)     echo "$message" ;;
  esac
}

# Function to install required packages
install_packages() {
  log info "Updating package lists..."
  sudo apt update -y
  
  log info "Installing required packages..."
  sudo apt install -y unzip docker.io docker-compose nginx
  
  sudo systemctl start docker
  sudo systemctl enable docker
  
  # Configure user permissions for Docker
  sudo usermod -aG docker $USER
  log info "Docker permissions set. You may need to reconnect to use Docker without sudo."
}

# Set up Docker command with or without sudo
setup_docker_cmd() {
  if docker ps &> /dev/null; then
    DOCKER_CMD="docker"
    DOCKER_COMPOSE_CMD="docker-compose"
  else
    log warn "Using sudo for Docker commands in this session"
    DOCKER_CMD="sudo docker"
    DOCKER_COMPOSE_CMD="sudo docker-compose"
  fi
}

# Extract application files
extract_application() {
  log info "Creating application directory..."
  sudo mkdir -p /app
  sudo chown $USER:$USER /app
  
  log info "Extracting application package..."
  unzip -oq /tmp/app.zip -d /app
  rm /tmp/app.zip
}

# Deploy the application
deploy_app() {
  cd /app
  
  # Check if custom deployment script exists
  if [ -f "./server_deploy.sh" ]; then
    log info "Running custom deployment script..."
 sudo chmod +x ./server_deploy.sh
    ./server_deploy.sh
  else
    log warn "No server_deploy.sh found. Deploying manually with Docker..."
    
    # Check if docker-compose.yml exists
    if [ -f "./docker-compose.yml" ]; then
      log info "Deploying with docker-compose..."
      $DOCKER_COMPOSE_CMD down || true
      $DOCKER_COMPOSE_CMD up -d
    else
      log info "Building and running Docker container manually..."
      $DOCKER_CMD build -t streamflix:latest .
      $DOCKER_CMD stop streamflix || true
      $DOCKER_CMD rm streamflix || true
      $DOCKER_CMD run -d --name streamflix -p 5000:5000 streamflix:latest
    fi
  fi
}

# Configure Nginx (if needed)
configure_nginx() {
  if [ -f "/app/nginx.conf" ]; then
    log info "Configuring Nginx with provided configuration..."
    sudo cp /app/nginx.conf /etc/nginx/sites-available/streamflix
    sudo ln -sf /etc/nginx/sites-available/streamflix /etc/nginx/sites-enabled/
    sudo rm -f /etc/nginx/sites-enabled/default
  else
    log info "Creating default Nginx configuration..."
    sudo tee /etc/nginx/sites-available/streamflix > /dev/null << EOF
server {
    listen 80;
    server_name _;

    location / {
        proxy_pass http://localhost:5000/;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF
    sudo ln -sf /etc/nginx/sites-available/streamflix /etc/nginx/sites-enabled/
    sudo rm -f /etc/nginx/sites-enabled/default
  fi
  
  # Check and reload Nginx
  log info "Testing Nginx configuration..."
  sudo nginx -t
  sudo systemctl restart nginx
}

# Verify deployment
verify_deployment() {
  log info "Verifying application status..."
  $DOCKER_CMD ps
  
  # Check if container is running
  if ! $DOCKER_CMD ps | grep -q streamflix; then
    log error "Application container is not running!"
    log info "Checking Docker logs..."
    $DOCKER_CMD logs streamflix
    log info "Attempting to start container..."
    cd /app
    if [ -f "./docker-compose.yml" ]; then
      $DOCKER_COMPOSE_CMD up -d
    else
      $DOCKER_CMD start streamflix
    fi
  fi
  
  # Test API endpoint
  log info "Testing API endpoint..."
  if curl -s http://localhost:5000/api/HellowWorld &> /dev/null; then
    log info "API is accessible on port 5000"
  else
    log warn "API not accessible on port 5000. Check application logs."
    $DOCKER_CMD logs streamflix
  fi
}

# Main deployment process
main() {
  log info "Starting server-side deployment..."
  
  install_packages
  setup_docker_cmd
  extract_application
  deploy_app
  configure_nginx
  verify_deployment
  
  log info "Server-side deployment completed successfully."
}

# Execute main function
main
REMOTE_SCRIPT

  # Upload and execute remote deployment script
  log info "Uploading deployment script..."
  scp -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" "$DEPLOYMENT_DIR/remote_deploy.sh" ubuntu@$INSTANCE_IP:/tmp/
  
  log info "Executing remote deployment script..."
  ssh -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP "chmod +x /tmp/remote_deploy.sh && /tmp/remote_deploy.sh"
  
  # Clean up
  rm -f "$DEPLOYMENT_DIR/remote_deploy.sh"
}

# Function to verify deployment
verify_deployment() {
  step "Verifying deployment"
  
  log info "Checking application status..."
  if ssh -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP "curl -s http://localhost:5000/api/HellowWorld" &> /dev/null; then
    log info "Application API is accessible."
  else
    log warn "Application API check failed. Please check the logs on the server."
    ssh -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP "sudo docker logs streamflix"
  fi
  
  log info "Checking Nginx status..."
  if ssh -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP "curl -s http://localhost:80" &> /dev/null; then
    log info "Nginx is properly configured and running."
  else
    log warn "Nginx check failed. Please check Nginx configuration."
    ssh -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP "sudo systemctl status nginx"
  fi
}

# Function to display deployment summary
deployment_summary() {
  step "Deployment Summary"
  
  echo -e "${BOLD}====== Streamflix Deployment Results ======${NC}"
  echo -e "${BOLD}Application URL:${NC} http://$INSTANCE_IP"
  echo -e "${BOLD}SSH Command:${NC} ssh -i $SSH_KEY_PATH ubuntu@$INSTANCE_IP"
  echo -e "${BOLD}Deployment Directory:${NC} $DEPLOYMENT_DIR"
  echo -e "${BOLD}Deployment Completed:${NC} $(date)"
  echo
  echo -e "${YELLOW}Note: If you need to run Docker commands, you may need to use sudo until you log out and log back in.${NC}"
  echo -e "${YELLOW}Example: sudo docker ps${NC}"
}

# Function to clean up temporary files
cleanup() {
  step "Cleaning up"
  
  # Remove temporary build directory if it exists
  if [ -n "$BUILD_DIR" ] && [ -d "$BUILD_DIR" ]; then
    log info "Removing temporary build directory..."
    rm -rf "$BUILD_DIR"
  fi
  
  log info "Cleanup completed."
}

# Main function to orchestrate the deployment
main() {
  check_prerequisites
  setup_directories
  setup_aws_credentials
  provision_infrastructure
  prepare_application
  setup_ssh_access
  deploy_application
  verify_deployment
  deployment_summary
  cleanup
  
  echo -e "\n${BOLD}${GREEN}====== Deployment Completed Successfully ======${NC}"
}

# Trap for cleanup on script exit
trap cleanup EXIT

# Execute main function
main