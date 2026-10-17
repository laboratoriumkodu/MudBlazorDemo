# OpenShift OAuth Proxy + Entra ID Setup - Summary

✅ **Setup Complete!** Your MudBlazorDemo app is now ready for OpenShift deployment with OpenShift OAuth Proxy authenticating against Azure Entra ID (Microsoft Entra ID).

---

## 📁 Files Created/Modified

### Core Containerization
| File | Purpose |
|------|---------|
| [Dockerfile](Dockerfile) | Multi-stage Docker build for production |
| [.dockerignore](.dockerignore) | Exclude unnecessary files from build context |

### Middleware & Authentication
| File | Purpose |
|------|---------|
| [Middleware/ProxyAuthMiddleware.cs](MudBlazorDemo/Middleware/ProxyAuthMiddleware.cs) | Reads OAuth proxy headers (X-Remote-User, etc.) and creates user context |
| [MudBlazorDemo/Program.cs](MudBlazorDemo/Program.cs) | Registers middleware and configures forwarded headers for OAuth proxy |
| [MudBlazorDemo/appsettings.Production.json](MudBlazorDemo/appsettings.Production.json) | Configuration for OAuth proxy authentication |

### OpenShift Deployment
| File | Purpose |
|------|---------|
| [openshift/deployment.yaml](openshift/deployment.yaml) | Namespace, Deployment, Service, Route, RBAC |
| [openshift/buildconfig.yaml](openshift/buildconfig.yaml) | Automated image builds from Git |
| [openshift/README.md](openshift/README.md) | Quick reference for OpenShift manifests |

### Scripts & Documentation
| File | Purpose |
|------|---------|
| [scripts/build-and-push.sh](scripts/build-and-push.sh) | Linux/Mac build & push script |
| [scripts/build-and-push.bat](scripts/build-and-push.bat) | Windows build & push script |
| [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md) | **Complete setup guide** (read this first!) |
| [MudBlazorDemo.Client/Pages/AuthInfo.razor](MudBlazorDemo.Client/Pages/AuthInfo.razor) | Example page showing authenticated user info |

---

## 🚀 Quick Start (5 Steps)

### Step 1: Build Docker Image

**Windows:**
```powershell
.\scripts\build-and-push.bat docker.io your-username latest
```

**Linux/Mac:**
```bash
chmod +x ./scripts/build-and-push.sh
./scripts/build-and-push.sh docker.io your-username latest
```

Or build manually:
```bash
docker build -t your-registry/mudblazor-demo:1.0.0 .
docker push your-registry/mudblazor-demo:1.0.0
```

### Step 2: Update Image Reference

Edit `openshift/deployment.yaml` and update:
```yaml
image: your-registry/mudblazor-demo:1.0.0
```

### Step 3: Create OAuth Proxy Secret

```bash
# Create secret with Entra ID credentials
oc create secret generic oauth-proxy-secret \
  --from-literal=client-id=YOUR_CLIENT_ID \
  --from-literal=client-secret=YOUR_CLIENT_SECRET \
  -n mudblazor-demo
```

### Step 3b: Deploy to OpenShift

```bash
oc apply -f openshift/deployment.yaml
```

### Step 4: Configure OpenShift OAuth Proxy with Entra ID

**In Azure Portal:**
1. Register an app in Entra ID (Azure AD)
2. Add redirect URI: `https://mudblazor-demo.yourdomain.com/oauth2/callback`
3. Create client secret and copy **Client ID** and **Client Secret**

**In OpenShift:**
1. Create OAuth proxy secret with Entra ID credentials
2. Deploy OAuth proxy sidecar or use OAuth proxy operator
3. Configure proxy annotations in deployment.yaml
4. Proxy will authenticate users against Entra ID and inject headers

### Step 5: Verify Authentication

Visit `/auth-info` page after accessing through the proxy to see authenticated user info.

---

## 🔐 How It Works

```
┌─OpenShift Route                         │
│ (TLS termination)                       │
└─────────────────────────────────────────┘
          ↓
┌─────────────────────────────────────────┐
│ OAuth-Proxy Sidecar Container           │
│ (OIDC flow with Entra ID)               │
│ Verifies token, injects headers         │
│ Adds X-Remote-User, X-Remote-Email      │
└─────────────────────────────────────────┘
          ↓
┌─────────────────────────────────────────┐
│ MudBlazor App Pod (localhost:8080)──────┐
│ OpenShift Ingress (TLS termination)     │
└─────────────────────────────────────────┘
          ↓
┌─────────────────────────────────────────┐
│ MudBlazor App Pod                       │
│ ProxyAuthMiddleware reads headers       │
│ Creates ClaimsPrincipal for user        │
└─────────────────────────────────────────┘
```
OpenShift-native OAuth proxy** — Kubernetes-standard authentication
- ✅ **Entra ID OIDC integration** — Uses Microsoft Entra ID as identity provider
- ✅ **No auth code in app** — Proxy handles all OpenID Connect flow
- ✅ **Headers passed to app** — `X-Remote-User`, `X-Remote-Email`, `X-Remote-Name`
- ✅ **User context available** — Access via `User.Identity.IsAuthenticated` and `User.Claims`
- ✅ **TLS secured** — End-to-end encryption with Route termination
- ✅ **Non-root container** — Security best practice
- ✅ **Token validation** — Proxy validates all tokens before allowing accessdentity.IsAuthenticated` and `User.Claims`
- ✅ **TLS secured** — End-to-end encryption
- ✅ **Non-root container** — Security best practice

---

## 📋 Key Files Explained

### `ProxyAuthMiddleware.cs`
Reads authentication headers from the proxy and creates a `ClaimsPrincipal`. If you need custom header names, update here or via `appsettings.json`.

### `Program.cs`
- Enables `ForwardedHeadersMiddleware` as the first middleware (`ForwardLimit=2`, trusts loopback + router network) so the app sees the real client IP — see *Real Client IP* in `OPENSHIFT_DEPLOYMENT_GUIDE.md`
- Registers `ProxyAuthMiddleware`
- Sets ASPNETCORE_URLS to `http://+:8080`
OAuth proxy sidecar container (oauth-proxy)
- OAuth proxy configured with Entra ID OIDC endpoints
- 
### `deployment.yaml`
- 2 replicas for high availability
- Health checks configured (liveness + readiness)
- Non-root user security context
- Resource requests/limits set
- Environment variables for production

### `OPENSHIFT_DEPLOYMENT_GUIDE.md`
Complete reference with:
- Entra ID OIDC app registration
- OAuth proxy configuration steps
- OpenShift deployment process
- Azure AD App Proxy configuration
- Troubleshooting guide
- Security best practices

---

## 📝 Configuration Reference

### Environment Variables (in `deployment.yaml`)

| Variable | Value | Purpose |
|----------|-------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Environment mode |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen on port 8080 |

> `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is intentionally **not** set — forwarded headers are configured in `Program.cs` via the `ForwardedHeaders` section of `appsettings.Production.json` (`ForwardLimit`, `KnownNetworks`).

### Real Client IP

| Component | Setting |
|-----------|---------|
| Route | `haproxy.router.openshift.io/set-forwarded-headers: replace` |
| oauth2-proxy | `--reverse-proxy=true`, `--real-client-ip-header=X-Forwarded-For`, `--pass-host-header=true` |
| App | `ForwardedHeaders:ForwardLimit=2`, `ForwardedHeaders:KnownNetworks=[<pod CIDR>]` |

Get the pod CIDR with `oc get network.config cluster -o jsonpath='{.spec.clusterNetwork[*].cidr}'`. If the router uses `hostNetwork`, you need the node CIDR instead. See *Finding the router / pod CIDR* in `OPENSHIFT_DEPLOYMENT_GUIDE.md`.

### Proxy Header Names (in `appsettings.Production.json`)

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
Entra ID OIDC app registered
- [ ] OAuth proxy secret created with client credentials
- [ ] Route hostname

## ✅ Verification Checklist

- [ ] Docker image builds locally without errors
- [ ] Image pushed to container registry
- [ ] OpenShift namespace created
- [ ] Deployment, Service, Route deployed
- [ ] Pods are running (`oc get pods -n mudblazor-demo`)
- [ ] Route is accessible (`oc get route -n mudblazor-demo`)
- [ ] Azure AD App Proxy configured
- [ ] External URL resolves to OpenShift ingress
- [ ] User can access app through proxy
- [ ] `/auth-info` page shows authenticated user
- [ ] Logs show no errors (`oc logs -f deployment/mudblazor-demo`)

---

## 🆘 Troubleshooting

### Pods not running?
```bash
oc descriOAuth proxy pod logs: `oc logs -f pod/mudblazor-demo-xxx -c oauth-proxy`
2. Verify Entra ID app is properly configured
3. Check redirect URI in Entra ID matches route URL
4. Verify header names in `appsettings.Production.json` match proxy output
5``

### 404 errors?
```bash
oc get route -n mudblazor-demo
# Check hostname matches DNS
```

### User not authenticated?
1. Check Azure AD App Proxy udBlazorDemo/Middleware/ProxyAuthMiddleware.cs) — Authentication middleware
- [AuthInfo.razor](MudBlazorDemo.Client/Pages/AuthInfo.razor) — Example authenticated page
- [oauth-proxy Documentation](https://github.com/oauth2-proxy/oauth2-proxy) — OAuth proxy config referenc
3. Enable debug logging in `ProxyAuthMiddleware`

For full troubleshooting guide, see [OPENSHIFT_DEPLOYMENT_GUIDE.md#7-troubleshooting](OPENSHIFT_DEPLOYMENT_GUIDE.md#7-troubleshooting).

---

## 📚 References

- **[OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md)** — Full setup instructions (read this!)
- **[openshift/README.md](openshift/README.md)** — Manifest file reference
- [Dockerfile](Dockerfile) — Container image definition
- [ProxyAuthMiddleware.cs](Middleware/ProxyAuthMiddleware.cs) — Authentication middleware
- [AuthInfo.razor](MudBlazorDemo.Client/Pages/AuthInfo.razor) — Example authenticated page

---
Register** Entra ID OIDC app (get Client ID and Secret)
3. **Build** Docker image using scripts
4. **Push** to your container registry
5. **Create** OAuth proxy secret with credentials
6. **Deploy** to OpenShift: `oc apply -f openshift/`
7. **Build** Docker image using scripts
3. **Push** to your container registry
4. **Deploy** to OpenShift: `oc apply -f openshift/`
5. **Configure** Azure AD Application Proxy
6. **Test** accessing app and viewing authenticated user info

---

## 💡 Tips

- Use `dotnet watch --project MudBlazorDemo` for local hot-reload development
- Use `oc port-forward svc/mudblazor-demo 8080:80 -n mudblazor-demo` to test locally
- Use `oc logs -f deployment/mudblazor-demo` to watch pod logs in real-time
- Use `/auth-info` page to verify authentication is working

---

**Ready to deploy?** Start with [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md)! 🚀
