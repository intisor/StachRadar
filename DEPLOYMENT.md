# 🚀 Deployment Guide

Complete instructions for deploying StackRadar in various environments.

---

## Table of Contents

1. [Local Development](#local-development)
2. [Windows Server](#windows-server)
3. [Linux Server](#linux-server)
4. [Docker](#docker)
5. [Cloud Platforms](#cloud-platforms)
6. [Production Considerations](#production-considerations)
7. [Monitoring & Maintenance](#monitoring--maintenance)

---

## Local Development

### Windows (Recommended)

**Duration**: 10 minutes

#### Step 1: Install Prerequisites
```powershell
# Install .NET 8 SDK
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
# Run installer and follow prompts

# Install Ollama (optional but recommended)
# Download from: https://ollama.ai/download
# Run installer

# Verify installation
dotnet --version
ollama --version  # If installed
```

#### Step 2: Clone Repository
```powershell
# Create project directory
mkdir C:\StackRadar
cd C:\StackRadar

# Clone repository
git clone https://github.com/yourusername/stackradar.git .

# Or download as ZIP and extract
```

#### Step 3: Setup Environment
```powershell
# Navigate to project
cd c:\Users\DELL\Desktop\Coded\stackTracer

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Verify build
# Should see: "Build succeeded with X warning(s)"
```

#### Step 4: Configure AI (Optional)
```powershell
# Start Ollama service (auto-starts)
ollama serve

# In another terminal, pull model
ollama pull phi:latest

# Verify
ollama list  # Should show phi:latest
```

#### Step 5: Run Application
```powershell
# Quick test - discover companies
dotnet run --project StackRadar.Cli -- scout --source dotnetjobs --limit 10

# Scan sample domains
# Create targets.txt with domains
# Then run:
dotnet run --project StackRadar.Cli -- scan --input targets.txt
```

---

## Windows Server

### Production Deployment

**Estimated Setup Time**: 30 minutes

#### Step 1: Server Prerequisites
```powershell
# Install .NET 8 Runtime or SDK
# Download: https://dotnet.microsoft.com/download/dotnet/8.0
# Choose Windows x64 runtime

# Install Ollama
# Download: https://ollama.ai/download

# Verify
dotnet --version
```

#### Step 2: Create Application Directory
```powershell
# Create dedicated directory
New-Item -ItemType Directory -Path "C:\Applications\StackRadar"
cd C:\Applications\StackRadar

# Clone or download application
git clone https://github.com/yourusername/stackradar.git .
```

#### Step 3: Prepare for Scheduled Runs
```powershell
# Build release version
dotnet build --configuration Release

# Create batch script for scanning
# File: run-scan.bat
@echo off
cd C:\Applications\StackRadar
dotnet run --project StackRadar.Cli --configuration Release -- scan --input targets.txt --output results.csv
echo Scan completed at %date% %time% >> scan-log.txt
```

#### Step 4: Configure Ollama Service
```powershell
# Install Ollama service (if not auto-installed)
# Start Ollama service
Start-Service "Ollama"

# Pull model (first run only)
ollama pull phi:latest

# Verify service
Get-Service "Ollama"
```

#### Step 5: Schedule Regular Runs (Optional)
```powershell
# Create scheduled task
$action = New-ScheduledTaskAction -Execute "C:\Applications\StackRadar\run-scan.bat"
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At 02:00AM
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount
$settings = New-ScheduledTaskSettingsSet -RunOnlyIfNetworkAvailable

Register-ScheduledTask -Action $action -Trigger $trigger -Principal $principal `
  -Settings $settings -TaskName "StackRadar-Weekly-Scan" -Description "Weekly ASP.NET discovery scan"
```

#### Step 6: Setup Logging
```powershell
# Create log directory
New-Item -ItemType Directory -Path "C:\Applications\StackRadar\logs"

# Configure log rotation (PowerShell script)
# File: cleanup-old-logs.ps1
Get-ChildItem "C:\Applications\StackRadar\logs" -Filter "*.log" | 
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | 
  Remove-Item

# Schedule cleanup task
$cleanupAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -File C:\Applications\StackRadar\cleanup-old-logs.ps1"
$cleanupTrigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 03:00AM
Register-ScheduledTask -Action $cleanupAction -Trigger $cleanupTrigger `
  -TaskName "StackRadar-Cleanup-Logs" -Description "Weekly log cleanup"
```

---

## Linux Server

### Deployment on Ubuntu/Debian

**Estimated Setup Time**: 30 minutes

#### Step 1: Install .NET Runtime
```bash
# Add Microsoft repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 8 runtime
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0
# Or install SDK for development
sudo apt-get install -y dotnet-sdk-8.0

# Verify
dotnet --version
```

#### Step 2: Install Ollama
```bash
# Download and install Ollama
curl -fsSL https://ollama.ai/install.sh | sh

# Start Ollama service
systemctl start ollama
systemctl enable ollama  # Auto-start

# Verify
ollama list
```

#### Step 3: Setup Application
```bash
# Create application directory
sudo mkdir -p /opt/stackradar
sudo chown $USER:$USER /opt/stackradar
cd /opt/stackradar

# Clone repository
git clone https://github.com/yourusername/stackradar.git .

# Build application
dotnet build --configuration Release

# Create symbolic link for logs
mkdir -p ./logs
```

#### Step 4: Create SystemD Service
```bash
# File: /etc/systemd/system/stackradar.service
sudo nano /etc/systemd/system/stackradar.service
```

```ini
[Unit]
Description=StackRadar ASP.NET Discovery Service
After=network.target
Requires=ollama.service

[Service]
Type=simple
User=ubuntu
WorkingDirectory=/opt/stackradar
ExecStart=/usr/bin/dotnet /opt/stackradar/StackRadar.Cli/bin/Release/net8.0/StackRadar.Cli.dll scout --source dotnetjobs --limit 100
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal
SyslogIdentifier=stackradar

[Install]
WantedBy=multi-user.target
```

```bash
# Enable service
sudo systemctl daemon-reload
sudo systemctl enable stackradar
sudo systemctl start stackradar

# Check status
sudo systemctl status stackradar
```

#### Step 5: Setup Cron Jobs
```bash
# Edit crontab
crontab -e

# Add weekly scan
0 2 * * 1 cd /opt/stackradar && /usr/bin/dotnet run --project StackRadar.Cli -- scout --source dotnetjobs >> ./logs/scan-$(date +\%Y\%m\%d).log 2>&1

# Add daily log cleanup
0 3 * * 0 find /opt/stackradar/logs -name "*.log" -mtime +30 -delete
```

#### Step 6: Monitoring
```bash
# View logs
sudo journalctl -u stackradar -f  # Follow live logs
sudo journalctl -u stackradar -n 50  # Last 50 lines

# Check application status
ps aux | grep StackRadar
```

---

## Docker

### Containerized Deployment

#### Step 1: Create Dockerfile
```dockerfile
# File: Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder
WORKDIR /app

# Copy source
COPY . .

# Build
RUN dotnet restore
RUN dotnet build --configuration Release
RUN dotnet publish --configuration Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# Install Ollama dependencies
RUN apt-get update && \
    apt-get install -y curl && \
    rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=builder /app/publish .

# Download Ollama
RUN curl -fsSL https://ollama.ai/install.sh | sh

# Create data directory
RUN mkdir -p /app/data /app/logs

# Set permissions
RUN chmod -R 755 /app

# Expose ports (if needed)
EXPOSE 11434

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD dotnet StackRadar.Cli.dll --help || exit 1

# Default command
ENTRYPOINT ["dotnet", "StackRadar.Cli.dll"]
CMD ["--help"]
```

#### Step 2: Create Docker Compose
```yaml
# File: docker-compose.yml
version: '3.8'

services:
  stackradar:
    build: .
    container_name: stackradar
    volumes:
      - ./data:/app/data
      - ./logs:/app/logs
      - ./config:/app/config
    environment:
      - DOTNET_ENVIRONMENT=Production
      - OLLAMA_URL=http://localhost:11434
    ports:
      - "11434:11434"
    restart: unless-stopped
    networks:
      - stackradar-network

  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    volumes:
      - ./ollama-data:/root/.ollama
    ports:
      - "11434:11434"
    restart: unless-stopped
    networks:
      - stackradar-network
    command: serve

networks:
  stackradar-network:
    driver: bridge
```

#### Step 3: Build and Run
```bash
# Build image
docker build -t stackradar:latest .

# Run with Docker Compose
docker-compose up -d

# Pull Ollama model
docker exec ollama ollama pull phi:latest

# View logs
docker logs -f stackradar

# Stop
docker-compose down
```

---

## Cloud Platforms

### Azure

```powershell
# Login to Azure
az login

# Create resource group
az group create --name stackradar-rg --location eastus

# Create app service plan
az appservice plan create --name stackradar-plan `
  --resource-group stackradar-rg --sku B2 --is-linux

# Create web app
az webapp create --resource-group stackradar-rg `
  --plan stackradar-plan --name stackradar-app `
  --runtime "DOTNET:8.0"

# Deploy
az webapp deployment source config-zip `
  --resource-group stackradar-rg --name stackradar-app `
  --src app.zip
```

### AWS

```bash
# Create EC2 instance
aws ec2 run-instances --image-id ami-0c55b159cbfafe1f0 \
  --count 1 --instance-type t3.medium \
  --key-name your-key-pair

# Connect and setup (same as Linux instructions)
ssh -i your-key.pem ubuntu@your-instance-ip

# Install .NET and Ollama (see Linux section)
```

### Google Cloud

```bash
# Create Compute Engine instance
gcloud compute instances create stackradar-instance \
  --image-family ubuntu-2204-lts \
  --image-project ubuntu-os-cloud \
  --machine-type e2-medium

# Connect
gcloud compute ssh stackradar-instance

# Setup (same as Linux instructions)
```

---

## Production Considerations

### Performance Optimization

```csharp
// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "StackRadar": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Scanner": {
    "Concurrency": 20,
    "Timeout": 30,
    "RetryAttempts": 3
  }
}
```

### Security Hardening

```powershell
# Run as dedicated user (Windows)
$user = "stackradar-svc"
$password = ConvertTo-SecureString "StrongPassword123!" -AsPlainText -Force
New-LocalUser -Name $user -Password $password -Description "StackRadar Service Account"

# Restrict file permissions
icacls "C:\Applications\StackRadar" /grant "${user}:(OI)(CI)M"
```

```bash
# Run as dedicated user (Linux)
sudo useradd -m -s /bin/false stackradar
sudo chown -R stackradar:stackradar /opt/stackradar
sudo chmod 750 /opt/stackradar
```

### Backup Strategy

```powershell
# Backup script (Windows)
$date = Get-Date -Format "yyyyMMdd"
$backupPath = "C:\Backups\StackRadar\backup-$date.zip"

Compress-Archive -Path "C:\Applications\StackRadar\data" `
  -DestinationPath $backupPath

# Keep only last 30 days
Get-ChildItem "C:\Backups\StackRadar" -Filter "*.zip" |
  Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
  Remove-Item
```

```bash
# Backup script (Linux)
#!/bin/bash
DATE=$(date +%Y%m%d)
BACKUP_PATH="/backups/stackradar/backup-$DATE.tar.gz"

tar -czf $BACKUP_PATH /opt/stackradar/data

# Keep only last 30 days
find /backups/stackradar -name "*.tar.gz" -mtime +30 -delete
```

---

## Monitoring & Maintenance

### Health Checks

```powershell
# Windows - Check service status
Get-Service "Ollama" | Select-Object Status

# Check disk space
Get-Volume -DriveLetter C | Select-Object SizeRemaining

# Memory usage
Get-Process | Where-Object Name -like "*dotnet*" | Select-Object Name, WorkingSet
```

```bash
# Linux - Check service status
systemctl status ollama
systemctl status stackradar

# Check disk space
df -h /opt/stackradar

# Memory usage
ps aux | grep dotnet
```

### Updating Application

```bash
# Pull latest code
cd /opt/stackradar
git pull origin main

# Rebuild
dotnet build --configuration Release

# Restart service
sudo systemctl restart stackradar
```

### Performance Monitoring

```csharp
// Log metrics
_logger.LogInformation("Scan completed: {DomainsProcessed} domains in {ElapsedSeconds}s", 
  domainsProcessed, elapsed.TotalSeconds);

// Monitor Ollama
GET http://localhost:11434/api/tags
GET http://localhost:11434/api/status
```

---

## Troubleshooting

### Common Issues

**Issue**: "Cannot find Ollama"
```bash
# Restart Ollama service
systemctl restart ollama
# or (Windows)
Restart-Service Ollama
```

**Issue**: "Out of memory"
```bash
# Reduce concurrency
dotnet run --project StackRadar.Cli -- scan --concurrency 3
```

**Issue**: "Connection refused"
```bash
# Check if Ollama is running
ollama list
# or check port
netstat -an | grep 11434
```

---

## Support & Resources

- 📚 [COMPLETE_GUIDE.md](COMPLETE_GUIDE.md) - Complete documentation
- 🤖 [LOCAL_AI_SETUP.md](LOCAL_AI_SETUP.md) - AI setup guide
- 🔧 [README.md](README.md) - Quick reference
- 🐛 GitHub Issues for problem reports

---

**Last Updated**: October 18, 2025
