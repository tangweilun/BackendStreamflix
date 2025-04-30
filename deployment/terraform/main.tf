# Enhanced Streamflix AWS Infrastructure Configuration
# This Terraform configuration sets up the required AWS resources for the Streamflix application

# Provider configuration
provider "aws" {
  region = var.aws_region
  
  # Recommended best practice: explicitly set default tags at provider level
  default_tags {
    tags = {
      Project     = "Streamflix"
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  }
}

# Variables
variable "aws_region" {
  description = "AWS region to deploy resources"
  type        = string
  default     = "us-east-1"  # Default to AWS Academy lab region
}

variable "environment" {
  description = "Deployment environment"
  type        = string
  default     = "development"
}

variable "instance_type" {
  description = "EC2 instance type"
  type        = string
  default     = "t2.micro"
}

variable "key_name" {
  description = "SSH key name for EC2 instance access"
  type        = string
  default     = "vockey"  # Default AWS Academy key name
}

variable "ubuntu_ami" {
  description = "Ubuntu AMI ID for the specified region"
  type        = string
  default     = "ami-080e1f13689e07408"  # Ubuntu 22.04 LTS AMI for us-east-1
}

variable "volume_size" {
  description = "Root volume size in GB"
  type        = number
  default     = 20
}

variable "app_port" {
  description = "Application port"
  type        = number
  default     = 5000
}

variable "allowed_cidr" {
  description = "CIDR blocks allowed to access the instance"
  type        = list(string)
  default     = ["0.0.0.0/0"]  # Note: This is open to the world - consider restricting in production
}

# Locals for resource naming and tags
locals {
  timestamp     = formatdate("YYMMDDhhmmss", timestamp())
  name_prefix   = "streamflix-${var.environment}"
  resource_name = "${local.name_prefix}-${local.timestamp}"
  
  common_tags = {
    Application = "Streamflix"
    Version     = "1.0"
    CreatedAt   = local.timestamp
  }
}

# Security group resource with improved organization and documentation
resource "aws_security_group" "app_sg" {
  name        = "${local.resource_name}-sg"
  description = "Security group for Streamflix application"

  # HTTP access for web application (optional, can be removed if redirecting all to HTTPS)
  ingress {
    description = "HTTP traffic"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr
  }

  # HTTPS access for web application
  ingress {
    description = "HTTPS traffic"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr
  }

  # SSH access for management
  ingress {
    description = "SSH access"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr
  }

  # Application port access (Internal - Nginx proxies to this)
  # Consider restricting this to only the Nginx instance/security group if possible,
  # but allowing from all CIDRs is okay if Nginx is running on the same instance.
  ingress {
    description = "Application port access"
    from_port   = var.app_port
    to_port     = var.app_port
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr
  }

  # Allow all outbound traffic
  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # Add lifecycle policy to prevent accidental deletion
  lifecycle {
    create_before_destroy = true
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.resource_name}-sg"
    }
  )
}

# EC2 instance with improved configuration
resource "aws_instance" "app_server" {
  ami           = var.ubuntu_ami
  instance_type = var.instance_type
  key_name      = var.key_name
  vpc_security_group_ids = [aws_security_group.app_sg.id]
  # Load setup script from file - fixed path to point to the correct location
  user_data = file("${path.module}/../scripts/setup_docker.sh")

  # Configure the root volume
  root_block_device {
    volume_size           = var.volume_size
    volume_type           = "gp3"  # Better performance than gp2
    encrypted             = true  # Encrypt root volume for better security
    delete_on_termination = true
  }

  # Enable detailed monitoring
  monitoring = true

  # Add lifecycle policy to prevent accidental deletion
  lifecycle {
    create_before_destroy = true
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.resource_name}-server"
    }
  )
}


# Update these outputs to use the Elastic IP
output "ssh_command" {
  description = "SSH command to connect to the instance"
  value       = "ssh -i ${var.key_name}.pem ubuntu@${var.create_elastic_ip ? aws_eip.app_eip[0].public_ip : data.aws_eips.existing[0].public_ips[0]}"
}

output "website_url" {
  description = "URL to access the web application"
  value       = "http://${var.create_elastic_ip ? aws_eip.app_eip[0].public_ip : data.aws_eips.existing[0].public_ips[0]}"
}

output "application_url" {
  description = "URL to access the application directly"
  value       = "http://${var.create_elastic_ip ? aws_eip.app_eip[0].public_ip : data.aws_eips.existing[0].public_ips[0]}:${var.app_port}"
}

# Add RDS PostgreSQL variables
variable "db_instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.t3.micro"
}

variable "db_name" {
  description = "PostgreSQL database name"
  type        = string
  default     = "streamflix"
}

variable "db_username" {
  description = "PostgreSQL master username"
  type        = string
  default     = "postgres"
}

variable "db_password" {
  description = "PostgreSQL master password"
  type        = string
  default     = "admin123"  # In production, use a more secure method like AWS Secrets Manager
  sensitive   = true
}

# Create a security group for RDS
resource "aws_security_group" "rds_sg" {
  name        = "${local.resource_name}-rds-sg"
  description = "Security group for RDS PostgreSQL instance"

  # Allow PostgreSQL traffic from the EC2 instance security group
  ingress {
    description     = "PostgreSQL from EC2"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.app_sg.id]
  }
  
  # Allow PostgreSQL traffic from any IPv4 address
  ingress {
    description = "PostgreSQL from any IPv4"
    from_port   = 5432
    to_port     = 5432
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    description = "Allow all outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.resource_name}-rds-sg"
    }
  )
}

# Create RDS PostgreSQL instance
resource "aws_db_instance" "postgres" {
  identifier             = "${local.name_prefix}-${local.timestamp}-db"
  engine                 = "postgres"
  engine_version         = "17.4" 
  instance_class         = var.db_instance_class
  allocated_storage      = 20
  max_allocated_storage  = 100
  storage_type           = "gp2"
  storage_encrypted      = true
  db_name                = var.db_name
  username               = var.db_username
  password               = var.db_password
  port                   = 5432
  publicly_accessible    = true
  vpc_security_group_ids = [aws_security_group.rds_sg.id]
  skip_final_snapshot    = true
  apply_immediately      = true
  backup_retention_period = 7
  deletion_protection    = false  # Set to true for production

  # Add lifecycle policy here too for consistency if desired
  lifecycle {
     create_before_destroy = true # Might help if identifier changes need replacement
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.resource_name}-postgres" # Still using timestamp here
    }
  )
}

# Add RDS outputs
output "db_endpoint" {
  description = "RDS PostgreSQL endpoint"
  value       = aws_db_instance.postgres.endpoint
}

output "db_name" {
  description = "RDS PostgreSQL database name"
  value       = aws_db_instance.postgres.db_name
}

output "db_username" {
  description = "RDS PostgreSQL master username"
  value       = aws_db_instance.postgres.username
}

output "connection_string" {
  description = "PostgreSQL connection string (without password)"
  value       = "Host=${aws_db_instance.postgres.endpoint};Database=${aws_db_instance.postgres.db_name};Username=${aws_db_instance.postgres.username};Password=<password>"
  sensitive   = false
}

# Variable to control whether to create a new Elastic IP or use existing one
variable "create_elastic_ip" {
  description = "Whether to create a new Elastic IP address"
  type        = bool
  default     = true
}

# Data source to find existing Elastic IP by tag if not creating a new one
data "aws_eips" "existing" {
  count = var.create_elastic_ip ? 0 : 1
  
  tags = {
    Application = "Streamflix"
  }
}

# Create a new Elastic IP if specified
resource "aws_eip" "app_eip" {
  count = var.create_elastic_ip ? 1 : 0
  
  domain = "vpc"
  
  tags = merge(
    local.common_tags,
    {
      Name = "${local.resource_name}-eip"
    }
  )
  
  lifecycle {
    prevent_destroy = false
  }
}

# Associate Elastic IP with EC2 instance
resource "aws_eip_association" "eip_assoc" {
  instance_id   = aws_instance.app_server.id
  allocation_id = var.create_elastic_ip ? aws_eip.app_eip[0].id : data.aws_eips.existing[0].allocation_ids[0]
}

# Update outputs to use the Elastic IP
output "public_ip" {
  description = "Public IP address of the deployed instance"
  value       = var.create_elastic_ip ? aws_eip.app_eip[0].public_ip : data.aws_eips.existing[0].public_ips[0]
}

output "public_dns" {
  description = "Public DNS of the deployed instance"
  value       = aws_instance.app_server.public_dns
}
