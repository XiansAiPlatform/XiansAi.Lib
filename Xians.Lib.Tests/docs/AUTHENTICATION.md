# Authentication Guide for Xians.Lib Tests

## 🔐 Understanding API_KEY

**IMPORTANT**: `API_KEY` is **NOT** a simple string - it's a **Base64-encoded X.509 certificate**.

### What the Server Expects

```
Authorization: Bearer <base64-encoded-certificate>
```

The server will:
1. Decode the Base64 string
2. Parse it as an X.509 certificate
3. Extract tenant ID from the Organization (O=) field
4. Extract user ID from the Common Name (CN=) field
5. Validate the certificate

### Certificate Requirements

Your certificate **must** have:
- ✅ **Organization (O=)**: Your tenant ID
- ✅ **Common Name (CN=)**: Your user ID
- ✅ Valid date range (NotBefore < now < NotAfter)
- ✅ Proper Base64 encoding

Example certificate subject:
```
CN=user-abc123, O=tenant-xyz789, C=US
```

## 📥 Getting Your Certificate

### Option 1: Download from Platform (Recommended)

1. Log into your Xians platform
2. Go to Settings → API Access
3. Download your certificate file (`.pfx` or `.cer`)
4. Convert to Base64:

```bash
# On macOS/Linux
base64 -w 0 your-certificate.pfx > certificate-base64.txt

# On Windows (PowerShell)
[Convert]::ToBase64String([IO.File]::ReadAllBytes("your-certificate.pfx")) > certificate-base64.txt
```

5. Copy the Base64 string to your `.env` file:

```bash
# .env
SERVER_URL=https://your-server.com
API_KEY=MIIDXTCCAkWgAwIBAgIJAKL...  # ← Base64 certificate (very long string)
```

### Option 2: Use Existing Certificate File

If you have a `.pfx` or `.cer` file:

```bash
# Extract and encode certificate
openssl pkcs12 -in your-cert.pfx -clcerts -nokeys | base64 -w 0 > cert-base64.txt

# Or for .cer files
base64 -w 0 your-cert.cer > cert-base64.txt
```

## ⚠️ Common Errors

### Error 1: "The input is not a valid Base-64 string"

**Problem**: You provided a simple string instead of a Base64-encoded certificate

```bash
# ❌ WRONG - This is NOT a certificate
API_KEY=my-api-key-string-123

# ✅ CORRECT - Base64-encoded certificate (very long)
API_KEY=MIIDXTCCAkWgAwIBAgIJAKL5g3aN3dqKMA0GCSqGSIb3DQEBCwUA...
```

**Solution**: Get a proper Base64-encoded certificate (see above)

### Error 2: "Failed to extract tenant ID from certificate"

**Problem**: Certificate doesn't have Organization (O=) field

**Solution**: Ensure your certificate was issued by the Xians platform and contains:
```
O=your-tenant-id
CN=your-user-id
```

### Error 3: "Certificate has expired"

**Problem**: Certificate's validity period has passed

**Solution**: 
1. Check certificate expiration: `openssl x509 -in cert.cer -noout -dates`
2. Request a new certificate from the platform

### Error 4: "Certificate is not yet valid"

**Problem**: Certificate's NotBefore date is in the future

**Solution**: Check your system clock or wait until the certificate becomes valid

## 🧪 Testing Your Certificate

### Quick Validation Script

Create a file `test-cert.sh`:

```bash
#!/bin/bash

# Load .env
source .env

# Decode and inspect certificate
echo "$API_KEY" | base64 -d | openssl x509 -inform DER -text -noout

# Expected output should show:
# Subject: CN=user-123, O=tenant-456
# Validity: Not Before: ..., Not After: ...
```

### Manual Testing

```bash
# 1. Decode Base64
echo "YOUR_BASE64_CERT" | base64 -d > decoded-cert.der

# 2. View certificate details
openssl x509 -inform DER -in decoded-cert.der -text -noout

# 3. Check for required fields
# Should see:
#   Subject: CN=..., O=...
#   Validity: Not Before/After dates
```

## 🔄 How It Works

### Client Side (Xians.Lib)

```csharp
// In HttpClientService.CreateClient():

try {
    // 1. Decode Base64
    var certBytes = Convert.FromBase64String(apiKey);
    
    // 2. Load as X.509 certificate
    var cert = new X509Certificate2(certBytes);
    
    // 3. Validate expiration
    if (cert.NotAfter < DateTime.UtcNow) throw ...
    
    // 4. Re-export and send
    var exportedBytes = cert.Export(X509ContentType.Cert);
    var exportedBase64 = Convert.ToBase64String(exportedBytes);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {exportedBase64}");
}
catch (FormatException) {
    // Fallback: use as simple Bearer token (will likely fail on server)
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
}
```

### Server Side

```csharp
// In CertificateAuthenticationHandler:

// 1. Extract Bearer token
var authHeader = Request.Headers["Authorization"];
var token = authHeader.Replace("Bearer ", "");

// 2. Decode Base64
var certBytes = Convert.FromBase64String(token);  // ← Fails if not valid Base64

// 3. Parse certificate
var cert = new X509Certificate2(certBytes);

// 4. Extract tenant and user IDs
var tenantId = cert.Subject.Parse("O=...");  // Organization
var userId = cert.Subject.Parse("CN=...");    // Common Name

// 5. Authenticate request
```

## 📋 .env Example

```bash
# =============================================================================
# PRODUCTION CONFIGURATION
# =============================================================================

SERVER_URL=https://api.xians.ai

# Base64-encoded X.509 certificate (this is a very long string!)
# Get from: https://api.xians.ai/settings/api-credentials
API_KEY=MIIDXTCCAkWgAwIBAgIJAKL5g3aN3dqKMA0GCSqGSIb3DQEBCwUAMEUxCzAJBgNVBAYTAkFVMRMwEQYDVQQIDApTb21lLVN0YXRlMSEwHwYDVQQKDBhJbnRlcm5ldCBXaWRnaXRzIFB0eSBMdGQwHhcNMjQwMTAxMDAwMDAwWhcNMjUwMTAxMDAwMDAwWjBFMQswCQYDVQQGEwJBVTETMBEGA1UECAwKU29tZS1TdGF0ZTEhMB8GA1UECgwYSW50ZXJuZXQgV2lkZ2l0cyBQdHkgTHRkMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...

# Optional: Override Temporal server URL
# TEMPORAL_SERVER_URL=localhost:7233
```

## 💡 Quick Troubleshooting

| Error | Cause | Solution |
|-------|-------|----------|
| "Not valid Base-64" | Using string instead of cert | Get Base64 certificate |
| "Failed to extract tenant ID" | Missing O= field | Use platform-issued cert |
| "Certificate expired" | Past NotAfter date | Get new certificate |
| "Unauthorized" | Wrong tenant/user | Check certificate details |

## 🆘 Getting Help

If you don't have a certificate:

1. **Contact your platform administrator** to issue a certificate
2. **Check the platform documentation** for certificate generation
3. **Look for Settings → API Access** in the web UI

The certificate is tied to your tenant and user account and cannot be generated client-side.



