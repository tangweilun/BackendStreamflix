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

  # HTTP access for web application
  ingress {
    description = "HTTP traffic"
    from_port   = 80
    to_port     = 80
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

  # Application port access
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

  # Load setup script from file
  user_data = file("${path.module}/setup_docker.sh")

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

# CloudWatch alarm to monitor instance status
resource "aws_cloudwatch_metric_alarm" "instance_status" {
  alarm_name          = "${local.resource_name}-status-alarm"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = "2"
  metric_name         = "StatusCheckFailed"
  namespace           = "AWS/EC2"
  period              = "60"
  statistic           = "Maximum"
  threshold           = "1"
  alarm_description   = "This metric monitors EC2 instance status checks"
  
  dimensions = {
    InstanceId = aws_instance.app_server.id
  }

  tags = local.common_tags
}

# Output values with improved documentation
output "public_ip" {
  description = "Public IP address of the deployed instance"
  value       = aws_instance.app_server.public_ip
}

output "public_dns" {
  description = "Public DNS of the deployed instance"
  value       = aws_instance.app_server.public_dns
}

output "ssh_command" {
  description = "SSH command to connect to the instance"
  value       = "ssh -i ${var.key_name}.pem ubuntu@${aws_instance.app_server.public_ip}"
}

output "website_url" {
  description = "URL to access the web application"
  value       = "http://${aws_instance.app_server.public_ip}"
}

output "application_url" {
  description = "URL to access the application directly"
  value       = "http://${aws_instance.app_server.public_ip}:${var.app_port}"
}