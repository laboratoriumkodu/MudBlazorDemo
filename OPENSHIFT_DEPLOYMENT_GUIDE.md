# MudBlazorDemo on OpenShift with OpenShift OAuth Proxy + Azure Entra ID

This guide explains how to run MudBlazorDemo on Red Hat OpenShift with OpenShift OAuth Proxy providing OpenID Connect authentication using Azure Entra ID (Microsoft Entra ID).

## Architecture Overview

```
User Browser (HTTPS)
    ↓
OpenShift Route (TLS termination)
    ↓
OAuth-Proxy Container (Port 4180)
    ├→ OIDC flow with Azure Entra ID
    └→ Injects X-Remote-User, X-Remote-Email headers
    ↓
MudBlazor App Container (Port 8080)
    ├→ ProxyAuthMiddleware reads headers
    └→ Creates authenticated user context
```

**Key Benefits:**
- ✅ OpenShift-native authentication (OAuth proxy sidecar in pod)
- ✅ Azure Entra ID (Microsoft Entra ID) as identity provider
- ✅ Kubernetes-standard OIDC pattern
- ✅ Token validation before reaching app
- ✅ No authentication code in application

---

## 1. Build and Push Docker Image

### Prerequisites
- Docker installed locally or Podman
- Access to OpenShift cluster (`oc` CLI)
- Container registry (e.g., Docker Hub, Quay.io, or OpenShift internal registry)

### Build Locally

```bash
# Navigate to project root
cd /path/to/MudBlazorDemo

# Build the Docker image
docker build -t mudblazor-demo:1.0.0 .

# Tag for your registry
docker tag mudblazor-demo:1.0.0 your-registry/mudblazor-demo:1.0.0

# Push to registry
docker push your-registry/mudblazor-demo:1.0.0
```

### Build in OpenShift (Recommended)

OpenShift's BuildConfig automates image builds from your Git repository:

```bash
# Apply BuildConfig
oc apply -f openshift/buildconfig.yaml

# Trigger a build
oc start-build mudblazor-demo -n mudblazor-demo

# Monitor build progress
oc logs -f bc/mudblazor-demo -n mudblazor-demo
```

---

## 2. Deploy to OpenShift

### Prerequisites
- OpenShift cluster access
- `oc` CLI logged into the cluster

### Deploy Manifests

```bash
# Create namespace, deployment, service, and route
oc apply -f openshift/deployment.yaml

# Verify deployment
oc get pods -n mudblazor-demo
oc get svc -n mudblazor-demo
oc get route -n mudblazor-demo

# Get the route URL
oc get route mudblazor-demo -n mudblazor-demo -o jsonpath='{.spec.host}'
```

### Update Image Reference

If using external registry (not OpenShift internal registry):

```bash
# Edit deployment to point to your image
oc set image deployment/mudblazor-demo mudblazor-demo=your-registry/mudblazor-demo:1.0.0 -n mudblazor-demo

# Trigger rollout
oc rollout restart deployment/mudblazor-demo -n mudblazor-demo
```

---

## 3. Register Azure Entra ID Application

Register an OpenID Connect application in Azure Entra ID for OAuth proxy to authenticate users.

### 3.1 Register Application in Azure Portal

1. **Sign in** to [Azure Portal](https://portal.azure.com)
2. **Navigate** to: Azure AD → App registrations → New registration
3. **Fill in details:**
   - **Name**: `MudBlazor-Demo-OAuth`
   - **Supported account types**: `Single tenant` (or multi-tenant if needed)
   - **Redirect URI**: `https://mudblazor-demo.yourdomain.com/oauth2/callback`

4. **Click**: Register
5. **Save these values from Overview page:**
   - **Application (client) ID** → `CLIENT_ID`
   - **Directory (tenant) ID** → `TENANT_ID`

### 3.2 Create Client Secret

1. **Go to**: Certificates & secrets → Client secrets
2. **Click**: New client secret
3. **Description**: `MudBlazor OAuth Proxy`
4. **Expires**: 24 months (or as needed)
5. **Click**: Add
6. **Copy the secret value** → `CLIENT_SECRET` (shown only once!)

### 3.3 Add Redirect URI (if not already added)

1. **Go to**: Authentication → Redirect URIs
2. **Add URI**: `https://mudblazor-demo.yourdomain.com/oauth2/callback`

---

## 4. Create OpenShift Secret with Entra ID Credentials

Before deploying, create a Kubernetes Secret with your Entra ID credentials:

```bash
# Create namespace
oc create namespace mudblazor-demo

# Create secret
oc create secret generic oauth-proxy-secret \
  --from-literal=client-id=YOUR_CLIENT_ID \
  --from-literal=client-secret=YOUR_CLIENT_SECRET \
  -n mudblazor-demo

# Verify
oc get secret oauth-proxy-secret -n mudblazor-demo -o yaml
```

---

## 5. Deploy to OpenShift

### Prerequisites
- Image pushed to registry
- OAuth proxy secret created
- Domain configured to resolve to OpenShift ingress

### Deploy Manifests

```bash
# Navigate to project root
cd /path/to/MudBlazorDemo

# Apply deployment (includes namespace, deployment, service, route, RBAC, secret template)
oc apply -f openshift/deployment.yaml

# Verify resources
oc get pods -n mudblazor-demo
oc get svc -n mudblazor-demo
oc get route -n mudblazor-demo
```

### Update OAuth Proxy Configuration

Edit `openshift/deployment.yaml` and update OAuth proxy arguments with your Entra ID values:

```yaml
containers:
- name: oauth-proxy
  args:
  - --oidc-issuer-url=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0
  - --client-id=$(OAUTH_CLIENT_ID)
  - --client-secret=$(OAUTH_CLIENT_SECRET)
  - --redirect-url=https://mudblazor-demo.yourdomain.com/oauth2/callback
  - --upstream=http://localhost:8080/
  # ... more args
```

---

## 6. Verify Deployment

Wait for pods to be ready:

```bash
# Watch pod status
oc get pods -n mudblazor-demo -w

# When both containers show 1/1 and 2/2, continue
```

Get the route URL:

```bash
oc get route mudblazor-demo -n mudblazor-demo -o jsonpath='{.spec.host}'
# Output: mudblazor-demo.yourdomain.com
```

### Test Authentication

1. **Open browser**: `https://mudblazor-demo.yourdomain.com`
2. **Redirected to Entra ID login** (if not already authenticated)
3. **Sign in** with your Entra ID credentials
4. **Redirected back** to the app
5. **Visit** `/auth-info` to see authenticated user information

---

## 7. OAuth Proxy Headers & Middleware

### Headers Injected by OAuth Proxy

After successful Entra ID authentication, OAuth proxy injects these headers:

```
X-Auth-Request-User: user@company.com
X-Remote-User: user@company.com
X-Remote-Email: user@company.com
X-Remote-Name: User Name
X-Auth-Request-Groups: group1,group2
Authorization: Bearer <ENTRA_ID_ACCESS_TOKEN>
```

### ProxyAuthMiddleware Configuration

The middleware reads these headers and creates a `ClaimsPrincipal`:

```csharp
// MudBlazorDemo/Middleware/ProxyAuthMiddleware.cs
var remoteName = context.Request.Headers["X-Remote-User"].ToString();
var remoteEmail = context.Request.Headers["X-Remote-Email"].ToString();
var remoteDisplayName = context.Request.Headers["X-Remote-Name"].ToString();

// Create claims and set context.User
```

**Custom header names:** Update in `appsettings.Production.json`:
```json
{
  "ProxyAuth": {
    "HeaderNames": {
      "UserHeader": "X-Remote-User",
      "EmailHeader": "X-Remote-Email",
      "DisplayNameHeader": "X-Remote-Name"
    }
  }
}
```

### Real Client IP (X-Forwarded-For)

Requests pass through two proxies before reaching the app:

```
Client ──► OpenShift Router (HAProxy) ──► oauth2-proxy (:4180) ──► app (:8080)
           sets XFF = <client>            appends <router-ip>      reads XFF (2 hops)
```

The app receives `X-Forwarded-For: <client-ip>, <router-ip>`. Configuration on each layer:

| Layer | Setting | Where |
|-------|---------|-------|
| OpenShift Route | `haproxy.router.openshift.io/set-forwarded-headers: replace` — router overwrites client-supplied XFF (anti-spoofing). Use `append` only behind an external LB that sets XFF (and enable PROXY protocol on the IngressController). | `openshift/deployment.yaml` (Route) |
| oauth2-proxy | `--reverse-proxy=true`, `--real-client-ip-header=X-Forwarded-For`, `--pass-host-header=true` | `openshift/deployment.yaml` (sidecar args) |
| ASP.NET Core | `ForwardLimit = 2`, trusted proxies: loopback (sidecar) + router network from `ForwardedHeaders:KnownNetworks`; `UseForwardedHeaders()` is the first middleware | `Program.cs`, `appsettings.Production.json` |

Adjust `ForwardedHeaders` in `appsettings.Production.json` to match your cluster:

```json
{
  "ForwardedHeaders": {
    "ForwardLimit": 2,
    "KnownNetworks": [ "10.128.0.0/14" ]
  }
}
```

- `KnownNetworks` — CIDR(s) the OpenShift router connects **from** (see *Finding the router / pod CIDR* below).
- `ForwardLimit` — number of proxy hops; set `3` if an external load balancer sits in front of the router.
- Can be overridden by environment variables, e.g. `ForwardedHeaders__ForwardLimit=3`, `ForwardedHeaders__KnownNetworks__0=10.128.0.0/14`.
- Do **not** set `ASPNETCORE_FORWARDEDHEADERS_ENABLED` — it registers a second ForwardedHeaders middleware with conflicting defaults.

#### Finding the router / pod CIDR

The value for `KnownNetworks` is the network the router's requests come **from**. That depends on how the router is deployed.

**Step 1 — Check whether the router uses the host network**

```bash
oc get pods -n openshift-ingress -o wide
# or explicitly:
oc get deployment -n openshift-ingress -o jsonpath='{.items[*].spec.template.spec.hostNetwork}'
```

- Router pod `IP` is in the same range as your app pods and `hostNetwork` is empty or `false` → use the **pod network CIDR** (Step 2).
- Router pod `IP` equals the node IP and `hostNetwork` is `true` (common on bare metal or vSphere) → use the **node / machine network CIDR** (Step 3).

**Step 2 — Pod (cluster) network CIDR** (requires cluster-reader rights)

```bash
oc get network.config cluster -o jsonpath='{.spec.clusterNetwork[*].cidr}{"\n"}'
# example output: 10.128.0.0/14
```

**Step 3 — Node (machine) network CIDR** (only for `hostNetwork` routers)

```bash
oc get configmap cluster-config-v1 -n kube-system -o jsonpath='{.data.install-config}' | grep -A3 machineNetwork
# or list the node IPs and derive the subnet:
oc get nodes -o wide
```

**No cluster-admin rights?** Read the IPs of the running router pods, or check the IP the app actually sees (the last entry in `X-Forwarded-For`):

```bash
oc get pods -n openshift-ingress -o jsonpath='{range .items[*]}{.status.podIP}{"\n"}{end}'
```

Choose a CIDR that covers all router replicas, then put it in `ForwardedHeaders:KnownNetworks`. If you are unsure, ask your cluster administrator for the cluster network and machine network CIDRs. If the router sends from an address that isn't in the list, ASP.NET Core ignores `X-Forwarded-For`, and `RemoteIpAddress` shows the router IP instead of the client IP.

The real IP is then available as `HttpContext.Connection.RemoteIpAddress` (logs, rate limiting, auditing).

> ⚠️ **Security:** Port 8080 is reachable from other pods without passing through oauth2-proxy, so they could send forged `X-Forwarded-For` / `X-Remote-*` headers. Restrict ingress to port 4180 with a `NetworkPolicy`, or bind the app to `127.0.0.1:8080`.

---

## 8. Troubleshooting

### OAuth Proxy Pod Won't Start

**Check events and logs:**
```bash
oc describe pod <pod-name> -n mudblazor-demo
oc logs pod/<pod-name> -c oauth-proxy -n mudblazor-demo
```

**Common issues:**
- Secret not found: `oc get secret oauth-proxy-secret -n mudblazor-demo`
- Image pull error: Check image registry access
- Port conflict: Check for other containers on 4180

### OAuth Proxy Shows 401 Unauthorized

**Check logs:**
```bash
oc logs -f pod/<pod-name> -c oauth-proxy -n mudblazor-demo
```

**Verify Entra ID configuration:**
1. Redirect URI matches exactly: `https://mudblazor-demo.yourdomain.com/oauth2/callback`
2. Client ID and Secret are correct
3. App is not disabled in Azure AD

**Check OIDC issuer URL:**
```bash
# Should contain your TENANT_ID
--oidc-issuer-url=https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0

# Test endpoint is accessible
curl -s https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0/.well-known/openid-configuration | jq .
```

### User Not Authenticated in App

**Check headers are passed from proxy:**
```bash
# SSH into pod and check headers
oc exec -it pod/<pod-name> -c mudblazor-demo -- /bin/bash
curl -H "X-Remote-User: testuser@company.com" http://localhost:8080/auth-info
```

**Enable debug logging in middleware:**
```csharp
// Program.cs or ProxyAuthMiddleware.cs
var remoteName = context.Request.Headers["X-Remote-User"].ToString();
logger.LogInformation($"Received X-Remote-User: {remoteName}");
```

**Check header names match:**
- Proxy sends: `X-Remote-User`
- Middleware reads: `X-Remote-User`

### Route Not Accessible / 404 Errors

**Verify route exists:**
```bash
oc get route mudblazor-demo -n mudblazor-demo
oc describe route mudblazor-demo -n mudblazor-demo
```

**Check service endpoints:**
```bash
oc get endpoints mudblazor-demo -n mudblazor-demo
# Should show <cluster-ip>:4180 (pointing to oauth-proxy)
```

**Verify DNS:**
```bash
nslookup mudblazor-demo.yourdomain.com
ping mudblazor-demo.yourdomain.com
```

---

## 9. Advanced OAuth Proxy Configuration

### Entra ID Specific Settings

```bash
# Tenant-specific OIDC endpoint
--oidc-issuer-url=https://login.microsoftonline.com/{TENANT_ID}/v2.0

# Request specific scopes
--oidc-extra-audience=api://mudblazor-demo

# Pass access token to backend
--pass-access-token=true
```

### Cookie & Session Configuration

```bash
# Secure cookie settings
--cookie-secure=true          # HTTPS only
--cookie-httponly=true        # Prevent JavaScript access
--cookie-samesite=lax         # CSRF protection
--cookie-name=_oauth2_proxy
--cookie-age=604800           # 7 days (default)
--cookie-refresh=3600         # Refresh every hour

# Session store
--session-store-type=redis    # Optional: use Redis for distributed sessions
```

### Group-Based Access Control

Restrict access to specific groups from Entra ID:

```bash
# Require group membership
--oidc-groups-claim=groups

# Allowed groups
--allowed-groups=developers,team-leads

# Get groups from Entra ID token
# (Requires additional Entra ID app configuration)
```

### Email Domain Filtering

```bash
# Allow all email domains (default)
--email-domain=*

# Allow specific domain(s)
--email-domain=company.com
--email-domain=partner.com

# Restrict to authenticated users only
--email-domain=
```

---

## 10. Scaling & High Availability

Current deployment uses:
- **2 replicas** for redundancy
- **OAuth proxy sidecar** in each pod (runs oauth-proxy container)
- **Stateless design** with secure cookies (no session backend needed)

Scale to more replicas:
```bash
oc scale deployment mudblazor-demo --replicas=5 -n mudblazor-demo
```

Each pod gets its own oauth-proxy sidecar, distributing load across instances.

---

## 11. References

- [OAuth2-Proxy GitHub](https://github.com/oauth2-proxy/oauth2-proxy)
- [OAuth2-Proxy Documentation](https://oauth2-proxy.github.io/oauth2-proxy/)
- [Azure Entra ID OpenID Connect](https://learn.microsoft.com/en-us/entra/identity-platform/v2-protocols-oidc)
- [OpenShift Routes](https://docs.openshift.com/container-platform/latest/networking/routes/route-configuration.html)
- [ASP.NET Core Forwarded Headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer)
- [OIDC Spec](https://openid.net/specs/openid-connect-core-1_0.html)

---

## 12. Quick Command Reference

```bash
# Prerequisites: CLIENT_ID, CLIENT_SECRET, TENANT_ID from Entra ID

# Step 1: Build & Push Image
docker build -t your-registry/mudblazor-demo:1.0.0 .
docker push your-registry/mudblazor-demo:1.0.0

# Step 2: Create Secret
oc create namespace mudblazor-demo
oc create secret generic oauth-proxy-secret \
  --from-literal=client-id=YOUR_CLIENT_ID \
  --from-literal=client-secret=YOUR_CLIENT_SECRET \
  -n mudblazor-demo

# Step 3: Deploy
oc apply -f openshift/deployment.yaml

# Step 4: Monitor
oc get pods -n mudblazor-demo -w
oc logs -f pod/mudblazor-demo-xxx -c oauth-proxy -n mudblazor-demo

# Step 5: Test
oc get route mudblazor-demo -n mudblazor-demo -o jsonpath='{.spec.host}'
# Open in browser and login with Entra ID credentials

# Step 6: View User Info
# Visit https://mudblazor-demo.yourdomain.com/auth-info
```

---

## 13. Deployment Checklist

- [ ] Entra ID application registered
- [ ] Application (Client) ID saved
- [ ] Client Secret created and saved
- [ ] Redirect URI set to `https://mudblazor-demo.yourdomain.com/oauth2/callback`
- [ ] Docker image built and pushed to registry
- [ ] OAuth proxy secret created in OpenShift
- [ ] Image reference updated in `openshift/deployment.yaml`
- [ ] Tenant ID updated in OAuth proxy OIDC issuer URL
- [ ] Deployment applied: `oc apply -f openshift/deployment.yaml`
- [ ] Both OAuth proxy and app containers running (2/2 ready)
- [ ] Route accessible from browser
- [ ] DNS resolves to OpenShift ingress
- [ ] Can login with Entra ID credentials
- [ ] `/auth-info` shows authenticated user
- [ ] Logs show no errors
