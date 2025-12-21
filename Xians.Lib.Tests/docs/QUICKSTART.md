# Xians.Lib.Tests - Quick Start Guide

## ⚡ TL;DR

```bash
# 1. Get your Base64-encoded certificate from the platform
# 2. Setup
cp env.template .env
# Edit .env - set SERVER_URL and API_KEY (Base64 certificate!)

# 3. Run tests
dotnet test --filter "Category!=Integration&Category!=RealServer"  # Fast unit tests
dotnet test --filter "Category=RealServer"  # Test against real server
```

## 🔑 **IMPORTANT: API_KEY is a Certificate!**

The `API_KEY` is **NOT** a simple string - it's a **Base64-encoded X.509 certificate**.

```bash
# ❌ WRONG
API_KEY=my-simple-api-key

# ✅ CORRECT
API_KEY=MIIDXTCCAkWgAwIBAgIJAKL5g3aN3dqKMA0GCSqGSIb...  # Very long Base64 string
```

**See [`docs/AUTHENTICATION.md`](docs/AUTHENTICATION.md) for details on getting your certificate.**

## 🚀 Setup

### 1. Get Your Certificate

**From Platform** (Recommended):
- Log into your Xians platform
- Go to Settings → API Access
- Download certificate and convert to Base64

**Or convert existing certificate**:
```bash
base64 -w 0 your-certificate.pfx > cert-base64.txt
```

### 2. Configure .env

```bash
cp env.template .env
nano .env  # or your preferred editor
```

Update with your values:
```bash
SERVER_URL=https://your-server.com
API_KEY=<paste your base64 certificate here>  # Very long string!
```

## 🧪 Running Tests

### Quick Unit Tests (No Server Required)
```bash
dotnet test --filter "Category!=Integration&Category!=RealServer"
```
⚡ Very fast (<1s), no credentials needed

### Mock Integration Tests (WireMock)
```bash
dotnet test --filter "Category=Integration"
```
🏃 Fast (~10s), uses mock servers, doesn't connect to your server

### **Real Server Tests** (Actually Connects!)
```bash
dotnet test --filter "Category=RealServer"
```
🌐 Connects to your actual server, requires valid certificate

### All Tests
```bash
dotnet test
```

## 📊 Test Categories

| Category | What It Tests | Needs .env? | Needs Server? |
|----------|---------------|-------------|---------------|
| Unit | Code logic | ❌ No | ❌ No |
| Integration | Component integration (mocks) | ❌ No | ❌ No |
| RealServer | Actual server connection | ✅ Yes | ✅ Yes |

## 🔍 Verify Your Certificate

Test if your certificate is valid:

```bash
# Decode and inspect
echo "$API_KEY" | base64 -d | openssl x509 -inform DER -text -noout

# Should show:
# Subject: CN=user-id, O=tenant-id
# Validity: Not Before/After dates
```

## ⚠️ Troubleshooting

### "The input is not a valid Base-64 string"
- ❌ You used a simple string
- ✅ Use a Base64-encoded certificate

### "Failed to extract tenant ID from certificate"
- ❌ Certificate missing O= field
- ✅ Get a platform-issued certificate

### Tests pass with fake URL
- ℹ️ You're running Integration tests (they use mocks)
- ✅ Run `Category=RealServer` to test actual server

## 📚 More Info

- **[AUTHENTICATION.md](docs/AUTHENTICATION.md)** - How to get and use certificates
- **[TEST_TYPES.md](../TEST_TYPES.md)** - Understanding test categories
- **[README.md](../README.md)** - Full documentation

## 💡 Common Commands

```bash
# Fast development cycle
dotnet test --filter "Category!=Integration&Category!=RealServer"

# Verify server connection
dotnet test --filter "Category=RealServer"

# Everything
dotnet test
```
