provider "aws" {
  region = "us-east-1"  # Use the AWS Academy lab region
}

# Remove the data source that's causing permission issues
# Instead, always create a new security group with a unique name

# Create a timestamp-based unique identifier for resources
locals {
  timestamp = formatdate("YYMMDDhhmmss", timestamp())
  sg_name   = "streamflix-sg-${local.timestamp}"
}

# Security group resource
resource "aws_security_group" "web" {
  name        = local.sg_name
  description = "Allow HTTP, SSH and Docker traffic"

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 5000
    to_port     = 5000
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "streamflix-docker-sg"
  }
}

# EC2 instance
resource "aws_instance" "web" {
  ami           = "ami-080e1f13689e07408"  # Ubuntu 22.04 LTS AMI for us-east-1
  instance_type = "t2.micro"
  key_name      = "vockey"  # Default AWS Academy key

  vpc_security_group_ids = [aws_security_group.web.id]

  user_data = file("setup_docker.sh")

  tags = {
    Name = "streamflix-docker"
  }

  root_block_device {
    volume_size = 20  # GB
    volume_type = "gp2"
  }
}

# Output values
output "public_ip" {
  value = aws_instance.web.public_ip
}

output "ssh_command" {
  value = "ssh -i vockey.pem ubuntu@${aws_instance.web.public_ip}"
}

output "website_url" {
  value = "http://${aws_instance.web.public_ip}"
}