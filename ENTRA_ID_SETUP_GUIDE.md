# Azure Entra ID Configuration Guide

This guide walks through registering and configuring MudBlazorDemo with Azure Entra ID (Microsoft Entra ID) for use with OpenShift OAuth Proxy.

## 📋 Prerequisites

- **Azure Subscription** — Active Azure account with Entra ID
- **Admin Access** — Ability to register applications in your Entra ID tenant
- **Tenant ID** — Your Azure Entra ID tenant ID (get from [Azure Portal](https://portal.azure.com))
- **Application Domain** — Your planned domain for the app (e.g., `mudblazor-demo.yourdomain.com`)

---

## 🔑 Step 1: Get Your Tenant Information

Before registering the app, gather your Entra ID tenant details:

### Find Your Tenant ID

1. **Go to** [Azure Portal](https://portal.azure.com)
2. **Search for** "Azure Active Directory"
3. **Click** "Azure Active Directory"
4. **Look for** "Tenant ID" in the Overview section
5. **Copy and save** your Tenant ID (format: `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`)

### Find Your Tenant Name

1. **In Azure AD**, look for "Custom domain names" or "Initial domain"
2. **Note** your tenant name (format: `yourtenant.onmicrosoft.com` or custom domain)

**Save these values:**
```
TENANT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
TENANT_NAME=yourtenant.onmicrosoft.com
```

---

## 📱 Step 2: Register Application in Entra ID

### Register the App

1. **Go to** [Azure Portal](https://portal.azure.com) → Azure Active Directory
2. **Click** "App registrations" in the left sidebar
3. **Click** "New registration"
4. **Fill in the form:**
   - **Name**: `MudBlazor-Demo-OAuth` (or your app name)
   - **Supported account types**: 
     - `Accounts in this organizational directory only (Tenant only)` ← **Recommended for single tenant**
     - `Accounts in any organizational directory` ← For multi-tenant
     - `Accounts in any organizational directory + personal Microsoft accounts` ← For public access
   - **Redirect URI**: Leave blank for now (we'll add it in the next step)

5. **Click** "Register"

### Copy Application Credentials

After registration, you'll see the app overview page. **Copy and save** these values:

```
CLIENT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
TENANT_ID=yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
APPLICATION_ID_URI=api://mudblazor-demo
OBJECT_ID=zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz
```

**Location of these values:**
- **Application (Client) ID** — Displayed prominently at top
- **Directory (Tenant) ID** — In the "Essentials" section
- **Object ID** — In the "Essentials" section
- **Application ID URI** — In the "Essentials" section (may be blank initially)

---

## 🔗 Step 3: Configure Redirect URIs

Redirect URIs tell Entra ID where to send users after authentication. OAuth proxy needs this configured.

### Add Redirect URI

1. **In app registration**, go to **Authentication** (left sidebar)
2. **Under "Platform configurations"**, click **Add a platform**
3. **Select** "Web"
4. **Under "Redirect URIs", enter:**
   ```
   https://mudblazor-demo.yourdomain.com/oauth2/callback
   ```
   *(Replace `yourdomain.com` with your actual domain)*

5. **Under "Implicit grant and hybrid flows"**, check:
   - ☐ Access tokens (not needed for OAuth proxy)
   - ☐ ID tokens (not needed for OAuth proxy)
   *(OAuth proxy doesn't use implicit flow; leave unchecked)*

6. **Logout URL** (optional):
   ```
   https://mudblazor-demo.yourdomain.com/oauth2/sign_out
   ```

7. **Click** "Configure"

### Verify Redirect URI

1. **Go to** Authentication → Platform configurations → Web
2. **Verify** your Redirect URI is listed
3. **Status** should show "Configured"

---

## 🔐 Step 4: Create Client Secret

OAuth proxy needs a client secret to authenticate with Entra ID.

### Generate Secret

1. **In app registration**, go to **Certificates & secrets** (left sidebar)
2. **Click** "New client secret"
3. **Fill in:**
   - **Description**: `MudBlazor OAuth Proxy` (or descriptive name)
   - **Expires**: `24 months` (recommended for security; adjust as needed)
4. **Click** "Add"

### Copy the Secret

**IMPORTANT:** The secret is displayed **only once**. Copy it immediately!

```
CLIENT_SECRET=8M7~N-Q_9R.8S-T-U_V8W-X.Y
```

**Store this securely** (e.g., environment variables, secrets manager, not in code).

⚠️ **Warning:** If you lose it, you must create a new secret. The old one cannot be recovered.

---

## 👥 Step 5: Configure API Permissions (Optional)

If your app needs to call Microsoft Graph or other APIs, configure permissions here.

### Add Permissions (Optional)

1. **In app registration**, go to **API permissions** (left sidebar)
2. **Click** "Add a permission"
3. **Choose API**:
   - `Microsoft Graph` (for Microsoft services)
   - `Azure Active Directory Graph` (legacy)
   - Your own custom API

4. **Select permissions** (e.g., `User.Read` for profile access)
5. **Click** "Add permissions"
6. **Click** "Grant admin consent" (if required by your organization)

**Note:** OAuth proxy automatically requests `openid`, `profile`, and `email` scopes. You don't need to add them here.

---

## 📝 Step 6: Configure Entra ID Claims & Groups (Advanced)

If you need user groups or custom claims in the token:

### Enable Group Claims

1. **In app registration**, go to **Token configuration** (left sidebar)
2. **Click** "Add groups claim"
3. **Select group types:**
   - `SecurityGroup` — for security groups
   - `DirectoryRole` — for directory roles
   - `DistributionList` — for distribution lists
4. **SAML version of groups claim**: `Not required`
5. **Click** "Add"

### Add Custom Claims

1. **In "Token configuration"**, click **Add optional claim**
2. **Token type**: `ID`
3. **Add claims** like:
   - `email` — User's email
   - `upn` — User principal name
   - `department` — User's department
4. **Click** "Add"

---

## 🧪 Step 7: Verify Configuration

Before deploying, verify all settings are correct:

### Checklist

- [ ] Application name: `MudBlazor-Demo-OAuth` (or chosen name)
- [ ] Application ID: Copied and saved
- [ ] Tenant ID: Copied and saved
- [ ] Tenant Name: `yourtenant.onmicrosoft.com` (or custom domain)
- [ ] Redirect URI: `https://mudblazor-demo.yourdomain.com/oauth2/callback`
- [ ] Client Secret: Generated and saved securely
- [ ] Secret expiration: Noted (set reminder to rotate before expiration)
- [ ] API Permissions: Configured if needed

### Test OIDC Endpoint

Verify the OIDC endpoint is accessible:

```bash
# Use your TENANT_ID
curl -s https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0/.well-known/openid-configuration | jq .

# Should return JSON with:
# - issuer
# - authorization_endpoint
# - token_endpoint
# - userinfo_endpoint
# - jwks_uri
```

---

## 🚀 Step 8: Deploy with OAuth Proxy

Now that Entra ID is configured, deploy the app with OAuth proxy using these credentials:

### Create OpenShift Secret

```bash
oc create secret generic oauth-proxy-secret \
  --from-literal=client-id=YOUR_CLIENT_ID \
  --from-literal=client-secret=YOUR_CLIENT_SECRET \
  -n mudblazor-demo
```

### Configure OAuth Proxy Deployment

In `openshift/deployment.yaml`, update the OAuth proxy container arguments:

```yaml
containers:
- name: oauth-proxy
  image: quay.io/oauth2-proxy/oauth2-proxy:v7.5.1
  args:
  - --http-address=0.0.0.0:4180
  - --upstream=http://localhost:8080/
  - --provider=oidc
  - --oidc-issuer-url=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - --client-id=$(OAUTH_CLIENT_ID)
  - --client-secret=$(OAUTH_CLIENT_SECRET)
  - --redirect-url=https://mudblazor-demo.yourdomain.com/oauth2/callback
  - --cookie-secure=true
  - --cookie-httponly=true
  - --email-domain=*
  - --set-xauthrequest=true
  - --pass-user-headers=true
```

**Replace:**
- `YOUR_TENANT_ID` — Your Entra ID Tenant ID
- `mudblazor-demo.yourdomain.com` — Your domain

---

## 📊 Step 9: Verify Deployment

After deploying to OpenShift:

### Check OAuth Proxy Logs

```bash
# Watch OAuth proxy logs for authentication attempts
oc logs -f pod/mudblazor-demo-xxx -c oauth-proxy -n mudblazor-demo

# Look for successful token exchanges:
# [2026/01/20 10:30:45] [oauthproxy.go:1234] token validation successful
# [2026/01/20 10:30:46] [oauthproxy.go:1235] cookie set; redirecting to: /
```

### Test Authentication

1. **Open browser**: `https://mudblazor-demo.yourdomain.com`
2. **You should see** Entra ID login page (or be logged in if already authenticated)
3. **Sign in** with your Entra ID credentials
4. **Redirected back** to the app after successful authentication
5. **Visit** `/auth-info` page to see:
   - `User.Identity.Name` — Your UPN (e.g., `user@yourtenant.onmicrosoft.com`)
   - `User.Claims` — Your email, display name, and other claims
   - `X-Remote-User` header value

---

## 🔧 Step 10: Advanced Configuration (Optional)

### Restrict Access by Group

If you want only specific Entra ID groups to access the app:

1. **In Entra ID**, configure group claims (see Step 6)
2. **In OAuth proxy deployment**, add:
   ```yaml
   - --oidc-groups-claim=groups
   - --allowed-groups=developers,admin-team
   ```
3. **In Entra ID**, create groups (Azure AD → Groups → New group) and assign users

### Enable Multi-Tenant Access

If you want users from multiple Entra ID tenants:

1. **In app registration**, change **Supported account types** to:
   - `Accounts in any organizational directory`

2. **In OAuth proxy**, update issuer URL to use "common":
   ```yaml
   - --oidc-issuer-url=https://login.microsoftonline.com/common/v2.0
   ```

3. **Note:** This is less secure; use only if multi-tenant is required

### Custom User Attributes

To pass custom user attributes (department, manager, cost center, etc.):

1. **In app registration**, go to **Token configuration**
2. **Add optional claims** for the attributes you need
3. **In OAuth proxy**, enable:
   ```yaml
   - --pass-access-token=true
   - --set-authorization-header=true
   ```

---

## 🐛 Troubleshooting

### "Invalid Client ID" Error

**Problem:** OAuth proxy cannot authenticate with Entra ID

**Solution:**
1. Verify `CLIENT_ID` is correct and copied exactly
2. Verify secret is valid (not expired)
3. Check OpenShift secret: `oc get secret oauth-proxy-secret -n mudblazor-demo -o yaml`

### "Redirect URI Mismatch" Error

**Problem:** OAuth proxy redirects to a URL that doesn't match Entra ID configuration

**Solution:**
1. Check domain exactly matches configured Redirect URI
2. Ensure DNS resolves correctly
3. Verify TLS certificate is valid

### "Invalid Tenant" Error

**Problem:** `--oidc-issuer-url` is incorrect

**Solution:**
```bash
# Verify TENANT_ID
curl -s https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0/.well-known/openid-configuration

# Should return valid JSON, not 404
```

### User Not Authenticated in App

**Problem:** OAuth proxy passes headers, but app doesn't show authenticated user

**Solution:**
1. Check ProxyAuthMiddleware is enabled in `Program.cs`
2. Verify header names match: `X-Remote-User`, `X-Remote-Email`
3. Enable debug logging:
   ```csharp
   var remoteName = context.Request.Headers["X-Remote-User"].ToString();
   logger.LogInformation($"Received X-Remote-User: {remoteName}");
   ```

### "Certificate Validation Failed" Error

**Problem:** OAuth proxy cannot verify Entra ID's TLS certificate

**Solution:**
1. Verify SSL/TLS settings in OAuth proxy:
   ```yaml
   - --insecure-oidc-allow-unverified-email=false  # Keep strict verification
   ```
2. Check system certificate store
3. If behind corporate proxy, configure certificate pinning

---

## 📚 References

### Official Microsoft Documentation
- [Entra ID Application Registration](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app)
- [OAuth 2.0 & OpenID Connect Protocols](https://learn.microsoft.com/en-us/entra/identity-platform/v2-protocols)
- [Access Tokens & ID Tokens](https://learn.microsoft.com/en-us/entra/identity-platform/access-tokens)
- [Configure Token Claims](https://learn.microsoft.com/en-us/entra/identity-platform/how-to-add-app-roles-in-azure-ad-apps)

### OAuth2-Proxy Documentation
- [OAuth2-Proxy OIDC Configuration](https://github.com/oauth2-proxy/oauth2-proxy/blob/main/docs/docs/2_auth.md)
- [Azure/Entra ID OIDC Setup](https://github.com/oauth2-proxy/oauth2-proxy/blob/main/docs/docs/2_auth.md#azure-ad-openid-connect)

### Related MudBlazorDemo Documentation
- [OpenShift Deployment Guide](OPENSHIFT_DEPLOYMENT_GUIDE.md) — Full deployment walkthrough
- [OpenShift Setup Summary](OPENSHIFT_SETUP_SUMMARY.md) — Quick reference
- [AGENTS.md](AGENTS.md) — Project architecture and conventions

---

## 🎯 Quick Reference

### Entra ID Credentials Needed for OAuth Proxy

```
TENANT_ID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
CLIENT_ID=yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
CLIENT_SECRET=zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz
OAUTH_REDIRECT_URI=https://mudblazor-demo.yourdomain.com/oauth2/callback
OIDC_ISSUER_URL=https://login.microsoftonline.com/TENANT_ID/v2.0
```

### OAuth Proxy Container Arguments (Minimal)

```yaml
- --oidc-issuer-url=https://login.microsoftonline.com/{TENANT_ID}/v2.0
- --client-id=$(OAUTH_CLIENT_ID)
- --client-secret=$(OAUTH_CLIENT_SECRET)
- --redirect-url=https://mudblazor-demo.yourdomain.com/oauth2/callback
- --upstream=http://localhost:8080/
- --http-address=0.0.0.0:4180
```

### Entra ID User Flow

```
1. User visits: https://mudblazor-demo.yourdomain.com
        ↓
2. OAuth proxy redirects to Entra ID login:
   https://login.microsoftonline.com/{TENANT_ID}/oauth2/v2.0/authorize?client_id=...
        ↓
3. User authenticates with Entra ID credentials
        ↓
4. Entra ID redirects to: https://mudblazor-demo.yourdomain.com/oauth2/callback?code=...
        ↓
5. OAuth proxy exchanges code for token with Entra ID
        ↓
6. OAuth proxy sets secure cookie with token
        ↓
7. OAuth proxy injects X-Remote-User, X-Remote-Email headers
        ↓
8. Request forwarded to MudBlazor app (localhost:8080)
        ↓
9. ProxyAuthMiddleware reads headers and creates ClaimsPrincipal
        ↓
10. User sees authenticated app
```

---

## 📞 Support

**Issues with Entra ID configuration?**
- Check [Azure Entra ID Troubleshooting](https://learn.microsoft.com/en-us/entra/identity-platform/troubleshoot-sign-in-issues)
- Review OAuth proxy logs: `oc logs -f pod/mudblazor-demo-xxx -c oauth-proxy`
- See [OPENSHIFT_DEPLOYMENT_GUIDE.md#8-troubleshooting](OPENSHIFT_DEPLOYMENT_GUIDE.md#8-troubleshooting)

**Questions about MudBlazorDemo deployment?**
- Read [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md)
- Review [AGENTS.md](AGENTS.md) for project structure
