# 🤝 Contributing to StackRadar

Thank you for your interest in contributing to StackRadar! This document provides guidelines and instructions for contributing to the project.

---

## 📋 Code of Conduct

We are committed to providing a welcoming and inspiring community for all. Please be respectful and constructive in all interactions.

---

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)
- Ollama (optional, for AI features): https://ollama.ai/download
- Git
- Your favorite text editor or IDE (VS Code, Visual Studio, Rider)

### Setup Development Environment

```pwsh
# Clone repository
git clone https://github.com/yourusername/stackradar.git
cd stackTracer

# Install dependencies
dotnet restore

# Build solution
dotnet build

# Run tests (when available)
dotnet test

# Start coding!
```

---

## 🎯 Types of Contributions

We welcome various types of contributions:

### 🐛 Bug Reports
- Check if the issue already exists
- Include reproduction steps
- Provide error messages and logs
- Specify your environment (OS, .NET version)

### ✨ Feature Requests
- Describe the use case
- Explain the expected behavior
- Provide examples if possible
- Consider performance implications

### 📝 Documentation
- Improve existing documentation
- Add examples and tutorials
- Fix typos and formatting
- Clarify complex concepts

### 💻 Code Contributions
- Fix bugs
- Implement new features
- Improve performance
- Add tests
- Refactor code for clarity

### 🧪 Testing
- Write unit tests
- Create integration tests
- Test edge cases
- Document test scenarios

---

## 🔄 Development Workflow

### 1. Fork and Clone
```pwsh
# Fork the repository on GitHub
# Clone your fork
git clone https://github.com/YOUR-USERNAME/stackradar.git
cd stackTracer

# Add upstream remote
git remote add upstream https://github.com/original/stackradar.git
```

### 2. Create a Branch
```pwsh
# Update from upstream
git fetch upstream
git checkout upstream/main

# Create feature branch
git checkout -b feature/your-feature-name
```

**Naming conventions:**
- Features: `feature/description`
- Bugs: `fix/bug-description`
- Docs: `docs/documentation-topic`
- Tests: `test/test-description`

### 3. Make Changes
```pwsh
# Make your changes
# Run local tests
dotnet test

# Build to check for errors
dotnet build

# Format code
dotnet format  # if available
```

### 4. Commit Changes
```pwsh
# Stage changes
git add .

# Commit with meaningful message
git commit -m "feat: add new scraping source for Dice job site"
```

**Commit message format:**
```
<type>: <subject>

<body>

<footer>
```

Types:
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation
- `style:` Code style (formatting, semicolons, etc.)
- `refactor:` Code refactoring
- `perf:` Performance improvements
- `test:` Tests
- `chore:` Build process, dependencies, tooling

### 5. Push and Create Pull Request
```pwsh
# Push to your fork
git push origin feature/your-feature-name

# Create PR on GitHub
# Fill in PR template
# Link related issues
```

---

## ✅ Pull Request Guidelines

### Before Submitting
- [ ] Code builds without errors
- [ ] All tests pass
- [ ] No merge conflicts
- [ ] Code is properly formatted
- [ ] Comments explain complex logic
- [ ] No debug code or console logs (except logging framework)

### PR Description
```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Related Issues
Fixes #123

## Testing
Describe testing performed

## Checklist
- [ ] Code builds
- [ ] Tests pass
- [ ] Documentation updated
- [ ] No breaking changes
```

### What We Look For
- ✅ Code quality and clarity
- ✅ Test coverage
- ✅ Documentation
- ✅ Performance impact
- ✅ Security considerations
- ✅ Backward compatibility

---

## 🧪 Testing Guidelines

### Writing Tests
```csharp
[TestFixture]
public class DetectionEngineTests
{
    [Test]
    public void DetectAspNet_WithValidHeader_ReturnsTrue()
    {
        // Arrange
        var engine = new DetectionEngine();
        var headers = new { ["X-AspNet-Version"] = "4.0.30319" };
        
        // Act
        var result = engine.Detect(headers);
        
        // Assert
        Assert.That(result.IsAspNet, Is.True);
        Assert.That(result.Confidence, Is.GreaterThan(0.8));
    }
}
```

### Test Requirements
- Unit tests for new features
- Integration tests for data sources
- Edge case testing
- Error handling tests
- Aim for >80% code coverage

### Running Tests
```pwsh
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter ClassName

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## 📊 Code Style

### C# Conventions
- Use `PascalCase` for classes, methods, properties
- Use `camelCase` for local variables and parameters
- Use `_camelCase` for private fields
- Use `UPPER_CASE` for constants
- Use meaningful variable names
- Keep methods focused and small (< 20 lines ideal)
- Maximum line length: 120 characters

### Examples
```csharp
// ✅ Good
public class AspNetDetector
{
    private readonly ILogger<AspNetDetector> _logger;
    private const string AspNetVersionHeader = "X-AspNet-Version";
    
    public void AnalyzeDomain(string domain)
    {
        // Implementation
    }
}

// ❌ Avoid
public class Detector
{
    private ILogger l; // Unclear abbreviation
    
    public void A(string d) // Non-descriptive names
    {
        // Implementation
    }
}
```

### Documentation
```csharp
/// <summary>
/// Detects ASP.NET technology usage on a domain.
/// </summary>
/// <param name="domain">The domain to analyze</param>
/// <returns>Detection result with confidence score</returns>
/// <exception cref="ArgumentNullException">Thrown when domain is null</exception>
public DetectionResult Detect(string domain)
{
    // Implementation
}
```

---

## 🚨 Areas for Contribution

### High Priority
- [ ] Additional job site integrations
- [ ] Email finder integration
- [ ] WHOIS enrichment
- [ ] Performance optimization

### Medium Priority
- [ ] GitHub API integration
- [ ] CRM sync (Salesforce, HubSpot)
- [ ] Advanced filtering options
- [ ] Better error messages

### Low Priority
- [ ] UI/Dashboard
- [ ] Automated outreach
- [ ] Advanced ML models
- [ ] SaaS platform

---

## 🔧 Tools and Setup

### Recommended IDE
- **Visual Studio 2022** - Full IDE with excellent .NET support
- **VS Code** - Lightweight with C# extension
- **Rider** - Best-in-class for .NET development

### VS Code Extensions
```json
{
  "extensions": [
    "ms-dotnettools.csharp",
    "ms-dotnettools.vscode-dotnet-runtime",
    "EditorConfig.EditorConfig"
  ]
}
```

### Development Commands
```pwsh
# Build
dotnet build

# Run tests
dotnet test

# Format code
dotnet format

# Create NuGet package
dotnet pack

# Clean build artifacts
dotnet clean
```

---

## 📚 Resources

### Documentation
- [COMPLETE_GUIDE.md](COMPLETE_GUIDE.md) - Full project documentation
- [LOCAL_AI_SETUP.md](LOCAL_AI_SETUP.md) - AI setup guide
- [stackradar_spec.md](stackradar_spec.md) - Technical specification

### Learning Resources
- [Microsoft .NET Documentation](https://docs.microsoft.com/dotnet)
- [C# Programming Guide](https://docs.microsoft.com/dotnet/csharp)
- [Unit Testing Best Practices](https://docs.microsoft.com/dotnet/core/testing)

---

## 🐛 Debugging

### Enable Verbose Logging
```csharp
// In Program.cs
.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
})
```

### Common Issues

**Build Fails**
```pwsh
dotnet clean
dotnet restore
dotnet build --verbose
```

**NuGet Issues**
```pwsh
dotnet nuget locals all --clear
dotnet restore
```

**Test Failures**
```pwsh
dotnet test --verbosity=detailed
```

---

## 📝 Documentation

### Update Documentation
1. Clone the repository
2. Edit `.md` files
3. Preview changes locally
4. Submit PR with documentation changes

### Documentation Standards
- Clear and concise language
- Code examples for features
- Links to related sections
- Screenshots/diagrams where helpful
- Keep updated with code changes

---

## 🔐 Security

### Security Issues
**DO NOT** open public issues for security vulnerabilities. Instead:
1. Email security concerns to stackradar@example.com
2. Include reproduction steps
3. Provide suggested fixes if possible
4. Allow time for fix before disclosure

### Security Best Practices
- Never commit secrets or API keys
- Use environment variables for sensitive data
- Validate all user input
- Use HTTPS for all connections
- Keep dependencies updated

---

## 🎉 Recognition

### Contributors
All contributors will be:
- Added to CONTRIBUTORS.md
- Mentioned in release notes
- Recognized in project README

### Levels
- 🥉 Bronze (1-3 contributions)
- 🥈 Silver (4-10 contributions)
- 🥇 Gold (10+ contributions)
- 💎 Core Team (Very active contributors)

---

## 📞 Questions?

- 📧 Email: stackradar@example.com
- 💬 GitHub Discussions: [Link]
- 🐛 GitHub Issues: [Link]
- 📚 Documentation: See docs/ folder

---

## ✨ Thank You!

Your contributions make StackRadar better for everyone. Thank you for helping us build the best ASP.NET discovery platform! 🙏

---

**Happy coding! 🚀**
