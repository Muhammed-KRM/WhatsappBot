# Gemini API Docker Troubleshooting Guide

## Problem
Gemini API key validation fails when running inside Docker containers, even though the same API key works outside Docker.

## Root Causes Identified

### 1. Hosting Provider IP Blocks
**Most Common Issue**: Google blocks certain hosting providers' IP ranges, particularly:
- **Hetzner** (Germany) - Confirmed blocked
- **DigitalOcean** (some regions)
- **OVH** (some regions)
- Other budget VPS providers

**Symptoms:**
- Error: "User location is not supported for the API use"
- Status: `FAILED_PRECONDITION` (400)
- Works locally but fails on server

### 2. Docker User-Agent Issues
**Issue**: Docker containers may not send proper User-Agent headers, triggering Google's bot detection.

**Symptoms:**
- 403 Forbidden errors
- Requests timing out
- Different behavior inside vs outside container

### 3. DNS Resolution Problems
**Issue**: Docker containers may have DNS resolution issues with Google services.

## Solutions Implemented

### 1. Enhanced HTTP Client Configuration
```csharp
// Added proper User-Agent and headers
client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36");
client.DefaultRequestHeaders.Add("Accept", "application/json");
client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
```

### 2. Improved Error Handling
- Detailed logging for different error types
- Specific detection of hosting provider blocks
- Better timeout and retry handling

### 3. Diagnostic Endpoint
Access `/api/user/diagnose-gemini` to check:
- Docker environment detection
- DNS resolution capabilities
- Network connectivity
- Configuration status

## Workarounds

### Option 1: Change Hosting Provider
Move to a hosting provider not blocked by Google:
- **AWS EC2**
- **Google Cloud Platform**
- **Microsoft Azure**
- **Vultr** (some regions)
- **Linode** (some regions)

### Option 2: Use Proxy/VPN
Route traffic through an unblocked region:
```yaml
# Add to docker-compose.yml
services:
  cafebot-api:
    environment:
      - HTTP_PROXY=http://proxy-server:port
      - HTTPS_PROXY=http://proxy-server:port
```

### Option 3: Alternative DNS Configuration
```yaml
# Add to docker-compose.yml
services:
  cafebot-api:
    dns:
      - 8.8.8.8
      - 8.8.4.4
      - 1.1.1.1
```

### Option 4: Use Vertex AI Instead
Switch to Vertex AI API which has different IP restrictions:
```csharp
// Use Vertex AI endpoint instead
var baseUrl = "https://us-central1-aiplatform.googleapis.com/v1/projects/YOUR_PROJECT/locations/us-central1/publishers/google/models/gemini-1.5-flash:generateContent";
```

## Testing Steps

1. **Check if it's a hosting provider issue:**
   ```bash
   curl -H "User-Agent: Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36" \
        "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=YOUR_KEY" \
        -X POST -H "Content-Type: application/json" \
        -d '{"contents":[{"parts":[{"text":"test"}]}]}'
   ```

2. **Test from different locations:**
   - Run the same curl command from your local machine
   - Compare results

3. **Check diagnostic endpoint:**
   ```bash
   curl http://localhost:5000/api/user/diagnose-gemini
   ```

## Long-term Solutions

1. **Contact Google Support** (if you have a paid plan)
2. **Use Google Cloud Platform** for hosting
3. **Implement fallback to other AI providers** (OpenAI, Anthropic, etc.)
4. **Set up a proxy service** on an unblocked provider

## Status Monitoring

The application now provides detailed logging to help identify the specific cause:
- Check Docker logs: `docker-compose logs cafebot-api`
- Look for `[GeminiClient]` log entries
- Use the diagnostic endpoint for real-time status

## Community Resources

- [Google AI Forum - Hetzner Block Discussion](https://discuss.ai.google.dev/t/error-user-location-is-not-supported-for-the-api-use/74100)
- [Docker User-Agent Issues](https://discuss.ai.google.dev/t/from-inside-docker-user-location-is-not-supported-for-the-api-use-works-from-outside/81260)
- [N8N Community Solutions](https://community.n8n.io/t/facing-couldnt-connect-with-these-settings-error-for-google-gemini-and-groq-nodes-in-self-hosted-n8n-1-84-1-despite-valid-api-keys-and-network-access/92556)