#!/bin/bash
# run_migrations.sh - Script to run EF Core migrations on the deployed application

# Enable strict error handling
set -eo pipefail

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

# Load environment variables if they exist
if [ -f ".deployment_vars" ]; then
  source ".deployment_vars"
else
  log error "Deployment variables file not found. Please run deploy.sh first."
  exit 1
fi

# Prompt user for IP address
log info "Current instance IP from deployment vars: ${INSTANCE_IP:-Not set}"
read -p "$(echo -e "${YELLOW}Enter the EC2 instance IP address [${INSTANCE_IP:-Enter IP}]: ${NC}")" USER_INPUT_IP

# Use user input if provided, otherwise use the one from deployment vars
if [ -n "$USER_INPUT_IP" ]; then
  INSTANCE_IP="$USER_INPUT_IP"
  log info "Using user-provided IP address: $INSTANCE_IP"
  
  # Update the deployment vars with the new IP
  if [ -f ".deployment_vars" ]; then
    if grep -q "INSTANCE_IP=" .deployment_vars; then
      sed -i "s/INSTANCE_IP=.*/INSTANCE_IP=$INSTANCE_IP/" .deployment_vars
    else
      echo "INSTANCE_IP=$INSTANCE_IP" >> .deployment_vars
    fi
    log info "Updated .deployment_vars with the new IP address."
  fi
elif [ -z "$INSTANCE_IP" ]; then
  log error "No IP address provided. Cannot continue."
  exit 1
else
  log info "Using IP address from deployment vars: $INSTANCE_IP"
fi

# Check if SSH key exists
SSH_KEY_PATH=~/.ssh/vockey.pem
if [ ! -f "$SSH_KEY_PATH" ]; then
  log error "SSH key not found at $SSH_KEY_PATH"
  exit 1
fi

# Verify EC2 instance is reachable
log info "Verifying connection to EC2 instance at $INSTANCE_IP..."
MAX_RETRIES=3
RETRY_DELAY=5
for i in $(seq 1 $MAX_RETRIES); do
  if ssh -o StrictHostKeyChecking=no -o ConnectTimeout=10 -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP "echo Connection successful" &>/dev/null; then
    log info "Connection to EC2 instance successful."
    break
  fi
  
  if [ $i -eq $MAX_RETRIES ]; then
    log error "Failed to connect to EC2 instance after $MAX_RETRIES attempts."
    log info "Please check that:"
    log info "  1. The EC2 instance is running"
    log info "  2. Security groups allow SSH access from your IP"
    log info "  3. The IP address is correct"
    
    # Ask if user wants to try a different IP
    read -p "$(echo -e "${YELLOW}Would you like to try a different IP address? (y/n): ${NC}")" TRY_DIFFERENT_IP
    if [[ "$TRY_DIFFERENT_IP" =~ ^[Yy]$ ]]; then
      read -p "$(echo -e "${YELLOW}Enter the new EC2 instance IP address: ${NC}")" NEW_IP
      if [ -n "$NEW_IP" ]; then
        INSTANCE_IP="$NEW_IP"
        # Update the deployment vars with the new IP
        if [ -f ".deployment_vars" ]; then
          sed -i "s/INSTANCE_IP=.*/INSTANCE_IP=$INSTANCE_IP/" .deployment_vars
          log info "Updated .deployment_vars with the new IP address: $INSTANCE_IP"
        fi
        log info "Retrying connection with new IP: $INSTANCE_IP"
        i=0  # Reset counter to try again
        continue
      else
        log error "No IP address provided. Cannot continue."
        exit 1
      fi
    else
      exit 1
    fi
  fi
  
  log warn "Connection attempt $i failed. Retrying in $RETRY_DELAY seconds..."
  sleep $RETRY_DELAY
done

# Get database information from Terraform output
log info "Retrieving database information from Terraform..."
cd terraform
DB_HOST=$(terraform output -raw db_endpoint 2>/dev/null || echo "")
DB_NAME=$(terraform output -raw db_name 2>/dev/null || echo "streamflix")
DB_USER=$(terraform output -raw db_username 2>/dev/null || echo "postgres")
DB_PASSWORD="admin123"  # Default password from Terraform configuration
cd ..

# Verify database information
if [ -z "$DB_HOST" ]; then
  log error "Failed to get database endpoint from Terraform output."
  log info "Checking if database information is in .deployment_vars..."
  
  # Try to get from deployment vars if not in Terraform output
  if [ -z "$DB_HOST" ] && [ -n "$DB_ENDPOINT" ]; then
    DB_HOST="$DB_ENDPOINT"
  fi
  
  if [ -z "$DB_HOST" ]; then
    log error "Database host information not found. Please check your deployment."
    exit 1
  fi
fi

log info "Using database: Host=$DB_HOST, Name=$DB_NAME, User=$DB_USER"

# Run migrations on the remote server
log info "Running migrations on the server at $INSTANCE_IP"

# Create environment variables file for the remote server
cat > remote_env_vars.sh << EOF
#!/bin/bash
# Environment variables for database connection
export DB_HOST="${DB_HOST}"
export DB_NAME="${DB_NAME}"
export DB_USER="${DB_USER}"
export DB_PASSWORD="${DB_PASSWORD}"
export AWS_ACCESS_KEY="${AWS_ACCESS_KEY}"
export AWS_SECRET_KEY="${AWS_SECRET_KEY}"
export AWS_SESSION_TOKEN="${AWS_SESSION_TOKEN}"
export AWS_REGION="${AWS_REGION:-us-east-1}"
EOF

# Upload environment variables to the server
log info "Uploading environment variables..."
scp -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" remote_env_vars.sh ubuntu@$INSTANCE_IP:/tmp/

# Create remote migration script
cat > migration_script.sh << 'EOF'
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

# Load environment variables from the uploaded file
if [ -f "/tmp/remote_env_vars.sh" ]; then
  log info "Loading environment variables from uploaded file..."
  source /tmp/remote_env_vars.sh
else
  # Try to find environment variables in other locations
  if [ -f "/app/env/env_vars.sh" ]; then
    log info "Loading environment variables from /app/env/env_vars.sh..."
    source /app/env/env_vars.sh
  else
    log error "Environment variables file not found."
    exit 1
  fi
fi

# Verify environment variables are loaded
log info "Verifying database connection variables..."
if [ -z "$DB_HOST" ] || [ -z "$DB_NAME" ] || [ -z "$DB_USER" ] || [ -z "$DB_PASSWORD" ]; then
  log error "Database connection variables are missing. Please check your deployment."
  log info "Available variables: DB_HOST=$DB_HOST, DB_NAME=$DB_NAME, DB_USER=$DB_USER"
  exit 1
fi

# Set connection string for EF Core
DB_HOSTNAME=$(echo $DB_HOST | cut -d: -f1)
DB_PORT=$(echo $DB_HOST | cut -d: -f2)

# If no port was found, use default PostgreSQL port
if [ "$DB_HOSTNAME" = "$DB_PORT" ]; then
  DB_PORT="5432"
fi

# Create PostgreSQL connection string
CONNECTION_STRING="Host=${DB_HOSTNAME};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true;"

log info "Using connection string: Host=${DB_HOSTNAME};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=******"

# Check if Docker is available
if ! command -v docker &> /dev/null; then
  log error "Docker is not installed. Please install Docker to run migrations."
  exit 1
fi

# Navigate to the application directory
cd /app

# Create a temporary Dockerfile for migrations
log info "Creating Dockerfile for migrations..."
cat > Dockerfile.migrations << EOL
FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /app

# Install EF Core tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="\${PATH}:/root/.dotnet/tools"

# Copy existing project files
COPY . .

# Set environment variables
ENV ConnectionStrings__DefaultConnection="${CONNECTION_STRING}"

# Run migrations
ENTRYPOINT ["sh", "-c", "dotnet ef migrations add InitialCreate --context ApplicationDbContext || true && dotnet ef database update --context ApplicationDbContext"]
EOL

# Build and run the Docker container
log info "Building Docker image for migrations..."
docker build -t streamflix-migrations -f Dockerfile.migrations .

log info "Running migrations using Docker..."
docker run --rm \
  -e ConnectionStrings__DefaultConnection="$CONNECTION_STRING" \
  streamflix-migrations

# Clean up
log info "Cleaning up temporary files..."
rm -f Dockerfile.migrations

log info "Migration process completed successfully"
EOF

# Upload migration script to server
scp -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" migration_script.sh ubuntu@$INSTANCE_IP:/tmp/

# Execute migration script on server
log info "Executing migration script..."
ssh -o StrictHostKeyChecking=no -i "$SSH_KEY_PATH" ubuntu@$INSTANCE_IP "chmod +x /tmp/migration_script.sh && sudo -E /tmp/migration_script.sh"

# Clean up
rm -f migration_script.sh remote_env_vars.sh

log info "Migration process completed."
