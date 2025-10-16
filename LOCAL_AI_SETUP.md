# 🤖 StackRadar Local AI Setup Guide

## Prerequisites

- **Ollama** - Local LLM runtime (free, open-source)
- **.NET 8 Runtime**
- **Docker** (optional, for Ollama)

---

## 🚀 Installation Steps

### **1. Install Ollama**

#### **Windows**
```powershell
# Download from https://ollama.ai/download/windows
# Or use Windows Package Manager
winget install Ollama.Ollama
```

#### **macOS**
```bash
# Download from https://ollama.ai/download/mac
# Or use Homebrew
brew install ollama
```

#### **Linux**
```bash
curl https://ollama.ai/install.sh | sh
```

#### **Docker (All Platforms)**
```bash
docker run -d -p 11434:11434 ollama/ollama
```

---

### **2. Start Ollama Service**

#### **Windows**
```powershell
# Ollama runs as a service after installation
# Verify it's running:
curl http://localhost:11434/api/tags
```

#### **macOS/Linux**
```bash
ollama serve
```

---

### **3. Pull AI Models (Choose One)**

For StackRadar, we recommend lightweight models for fast extraction:

#### **Option A: Mistral (7B) - RECOMMENDED**
```bash
ollama pull mistral:latest
# Fast, accurate, optimized for extraction tasks
```

#### **Option B: Neural Chat (7B)**
```bash
ollama pull neural-chat:latest
# Good for conversation and analysis
```

#### **Option C: Dolphin Mixtral**
```bash
ollama pull dolphin-mixtral:latest
# More powerful but slower
```

#### **Option D: Llama 2 (7B)**
```bash
ollama pull llama2:latest
# General purpose, reliable
```

---

### **4. Verify Setup**

```bash
# Test Ollama is running
curl http://localhost:11434/api/tags

# Test with a simple prompt
curl -X POST http://localhost:11434/api/generate -d '{
  "model": "mistral:latest",
  "prompt": "What is .NET?"
}'
```

---

## 📋 Configuration

### **Update StackRadar for Local AI**

In `Program.cs` (Scout command), the LocalAiAnalyzer is configured with:

```csharp
// Default configuration
LocalAiEndpoint: "http://localhost:11434"
Model: "mistral:latest"  // Change to your preferred model
```

### **Custom Configuration**

Create `appsettings.json`:

```json
{
  "LocalAi": {
    "Endpoint": "http://localhost:11434",
    "Model": "mistral:latest",
    "Temperature": 0.3,
    "MaxTokens": 500
  }
}
```

---

## 🎯 StackRadar Commands

### **Scrape .NET Job Sites**

```bash
# Basic job site scraping
dotnet run --project StackRadar.Cli -- scout --source dotnetjobs --limit 50

# With verbose logging
dotnet run --project StackRadar.Cli -- scout --source dotnetjobs --limit 100 --verbose

# Save to specific file
dotnet run --project StackRadar.Cli -- scout --source dotnetjobs --output netjobs.txt
```

### **What Happens**

1. ✅ Loads `dotnet_job_sites.csv` with 15 job sites
2. ✅ Scrapes each site for .NET job listings
3. ✅ Sends HTML to local Ollama for analysis
4. ✅ Extracts company names using AI
5. ✅ Saves discovered companies to output file

---

## 📊 Expected Output

```
┏━━━━━━━━━━━━━━━━━┯━━━━━━━━┯──────────────────┓
│ Source          │ Targets │ Output           │
├─────────────────┼─────────┼──────────────────┤
│ dotnetjobs      │ 50      │ discovered.txt   │
└─────────────────┴─────────┴──────────────────┘

🤖 AI extracted companies:
- Microsoft
- Google
- Amazon
- Facebook
- Apple
- [... more companies ...]
```

---

## 🔧 Troubleshooting

### **Local AI Not Connecting**

```
Error: Cannot connect to local AI at http://localhost:11434
```

**Solution:**
```bash
# Verify Ollama is running
curl http://localhost:11434/api/tags

# If not running:
# Windows: Service should auto-start. Restart if needed.
# macOS/Linux: Run 'ollama serve' in terminal
```

### **Model Not Found**

```
Error: model "mistral:latest" not found
```

**Solution:**
```bash
# Pull the model
ollama pull mistral:latest

# List available models
ollama list
```

### **Slow Performance**

**Solution:**
- Use smaller model: `neural-chat:latest` or `phi:latest`
- Increase model RAM allocation
- Check system resources (RAM, CPU)

### **High Memory Usage**

**Solution:**
- Use smaller quantized models
- Run on GPU if available: `ollama pull mistral:q4_0`

---

## 🚀 Performance Tips

### **Recommended Models by Performance**

| Model | Size | Speed | Quality | RAM |
|-------|------|-------|---------|-----|
| phi:latest | 2.7B | ⚡⚡⚡ | ⭐⭐⭐ | 4GB |
| neural-chat | 7B | ⚡⚡ | ⭐⭐⭐⭐ | 8GB |
| mistral:latest | 7B | ⚡⚡ | ⭐⭐⭐⭐ | 8GB |
| llama2:latest | 7B | ⚡⚡ | ⭐⭐⭐⭐⭐ | 8GB |

### **Optimization Tips**

1. **Use quantized models** for faster performance:
   ```bash
   ollama pull mistral:q4_0  # 4-bit quantization
   ```

2. **Batch processing** - Process multiple pages before AI analysis

3. **Caching** - Cache extraction results to avoid re-processing

---

## 📈 Next Steps

1. ✅ Install Ollama and pull a model
2. ✅ Run basic job site scraping
3. ✅ Analyze extracted companies
4. ✅ Cross-reference with your ASP.NET domain list
5. ✅ Perform manual LinkedIn searches for high-value leads

---

## 🤝 Support Resources

- **Ollama Docs**: https://ollama.ai
- **Model Library**: https://ollama.ai/library
- **GitHub**: https://github.com/jmorganca/ollama
- **Discord Community**: https://discord.gg/ollama

---

## 💡 Example Workflows

### **Workflow 1: Quick Company Discovery**
```bash
# Step 1: Scrape job sites for .NET companies
dotnet run --project StackRadar.Cli -- scout --source dotnetjobs --limit 100 --output companies.txt

# Step 2: Review results
type companies.txt | more

# Step 3: Manually search top companies on LinkedIn
```

### **Workflow 2: Intelligence Gathering**
```bash
# Step 1: Scrape companies
dotnet run -- scout --source dotnetjobs --limit 50 --verbose

# Step 2: Perform detailed analysis
# - Company size from job postings
# - Tech stack requirements
# - Hiring frequency
# - Growth trajectory
```

---

**Happy scraping! 🚀**
