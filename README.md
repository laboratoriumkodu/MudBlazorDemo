# MudBlazorDemo

A modern **Blazor Web 10.0** application demonstrating Material Design UI with MudBlazor components, hybrid rendering, and OpenShift deployment with OpenID Connect authentication using Azure Entra ID.

## 🎯 Overview

**MudBlazorDemo** is a full-stack Blazor application that showcases:

- **Server-side & Client-side Rendering**: Hybrid rendering with Blazor Web 10.0
- **Material Design UI**: Rich component library via [MudBlazor](https://mudblazor.com/)
- **REST API**: ASP.NET Core controllers with clean separation of concerns
- **Authentication**: OpenShift OAuth Proxy with Azure Entra ID (Microsoft Entra ID)
- **Cloud-ready**: Containerized with Docker for OpenShift deployment
- **Type-safe**: .NET 10.0 with full async/await patterns

---

## 📋 Quick Links

### 🚀 Getting Started

- **[AGENTS.md](AGENTS.md)** — AI coding agent guide for developers and AI assistants working in this codebase
- **[OPENSHIFT_SETUP_SUMMARY.md](OPENSHIFT_SETUP_SUMMARY.md)** — Quick summary of OpenShift OAuth Proxy + Entra ID setup
- **[ENTRA_ID_SETUP_GUIDE.md](ENTRA_ID_SETUP_GUIDE.md)** — Step-by-step guide to register and configure the app in Azure Entra ID
- **[ENVOY_JWT_VALIDATION_GUIDE.md](ENVOY_JWT_VALIDATION_GUIDE.md)** — Alternative: Envoy sidecar validating Keycloak JWTs with group-based access (replaces oauth2-proxy)

### 📖 Deployment & Operations

- **[OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md)** — Complete step-by-step deployment guide for Red Hat OpenShift
- **[openshift/README.md](openshift/README.md)** — Kubernetes manifest reference and configuration
- **[Dockerfile](Dockerfile)** — Multi-stage Docker build optimized for production

### 🛠️ Development

- **[MudBlazor Official Docs](https://mudblazor.com/)** — Component library reference
- **[Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)** — Blazor framework guide
- **[ASP.NET Core Web API](https://learn.microsoft.com/en-us/aspnet/core/web-api/)** — REST API best practices

---

## 🏗️ Project Structure

```
MudBlazorDemo (Solution Root)
├── MudBlazorDemo/                    # Server Project (ASP.NET Core)
│   ├── Components/                   # Razor components (shared)
│   ├── Controllers/                  # REST API endpoints
│   ├── Middleware/                   # Custom middleware (ProxyAuthMiddleware)
│   ├── Properties/                   # Launch settings, logging config
│   ├── Program.cs                    # DI setup, middleware registration
│   ├── appsettings.json             # App configuration
│   ├── appsettings.Production.json  # Production-specific config
│   └── Dockerfile                    # Multi-stage container build
│
├── MudBlazorDemo.Client/             # Client Project (Blazor WASM)
│   ├── Pages/                        # Routable components (@page)
│   ├── Layout/                       # MainLayout, NavMenu
│   ├── Services/                     # API client services
│   ├── Models/                       # Data transfer objects (DTOs)
│   ├── Program.cs                    # WASM bootstrap
│   └── wwwroot/                      # Static assets
│
├── openshift/                        # OpenShift/Kubernetes manifests
│   ├── deployment.yaml              # Deployment, Service, Route, RBAC
│   ├── buildconfig.yaml             # Automated image builds
│   └── README.md                    # Manifest reference
│
├── scripts/                          # Build & deployment automation
│   ├── build-and-push.sh            # Linux/Mac Docker build script
│   └── build-and-push.bat           # Windows Docker build script
│
├── AGENTS.md                         # AI agent coding guide
├── ENVOY_JWT_VALIDATION_GUIDE.md    # Envoy JWT sidecar (Keycloak + groups)
├── OPENSHIFT_DEPLOYMENT_GUIDE.md    # Complete deployment walkthrough
├── OPENSHIFT_SETUP_SUMMARY.md       # Quick setup reference
└── README.md                         # This file
```

---

## ⚡ Quick Start (Development)

### Prerequisites

- **.NET 10.0 SDK** — [Download](https://dotnet.microsoft.com/download)
- **Visual Studio Code** or **Visual Studio 2024+**
- Optional: **Docker** for containerized runs

### Build & Run

```bash
# Clone the repository
git clone <repo-url>
cd MudBlazorDemo

# Build the solution
dotnet build

# Run with hot-reload
dotnet watch --project MudBlazorDemo

# Run in development mode
dotnet run --project MudBlazorDemo
```

**Access the app:**
- HTTP: `http://localhost:5004`
- HTTPS: `https://localhost:7080`

### Test the API

The app includes sample endpoints:

```bash
# Get weather forecast
curl https://localhost:7080/api/sample/weather

# View in browser
https://localhost:7080/weather
```

---

## 🐳 Docker & OpenShift

### Build Container Image

```bash
# Windows
.\scripts\build-and-push.bat docker.io your-username 1.0.0

# Linux/Mac
chmod +x ./scripts/build-and-push.sh
./scripts/build-and-push.sh docker.io your-username 1.0.0

# Manual build
docker build -t your-registry/mudblazor-demo:1.0.0 .
docker push your-registry/mudblazor-demo:1.0.0
```

### Deploy to OpenShift

```bash
# Create OAuth proxy secret with Entra ID credentials
oc create secret generic oauth-proxy-secret \
  --from-literal=client-id=YOUR_CLIENT_ID \
  --from-literal=client-secret=YOUR_CLIENT_SECRET \
  -n mudblazor-demo

# Deploy all resources
oc apply -f openshift/deployment.yaml

# Check status
oc get pods -n mudblazor-demo
```

**For complete deployment instructions**, see [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md).

---

## 🔐 Authentication Architecture

The app uses **OpenShift OAuth Proxy** with **Azure Entra ID** for authentication:

```
User Browser
    ↓
OpenShift Route (HTTPS)
    ↓
OAuth Proxy Container (OIDC flow with Entra ID)
    ↓ Injects X-Remote-User, X-Remote-Email headers
MudBlazor App Container
    ↓ ProxyAuthMiddleware creates ClaimsPrincipal
Authenticated User Context
```

**Key Files:**
- [MudBlazorDemo/Middleware/ProxyAuthMiddleware.cs](MudBlazorDemo/Middleware/ProxyAuthMiddleware.cs) — Reads proxy headers
- [MudBlazorDemo/Program.cs](MudBlazorDemo/Program.cs) — Registers middleware
- [openshift/deployment.yaml](openshift/deployment.yaml) — OAuth proxy sidecar configuration

**View authenticated user info:** Navigate to `/auth-info` page (when running behind OAuth proxy)

---

## 📚 Key Technologies

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Frontend** | Blazor WASM | Interactive client-side UI |
| **Components** | MudBlazor | Material Design component library |
| **Backend** | ASP.NET Core | REST API, server-side rendering |
| **Rendering** | Blazor Hybrid | Mix of server & client rendering |
| **Container** | Docker | Production-ready image builds |
| **Orchestration** | OpenShift/Kubernetes | Cloud deployment & scaling |
| **Auth** | OpenID Connect (Entra ID) | Secure identity federation via Azure Entra ID |

---

## 🎓 Code Conventions

### Namespaces & File Organization
- Namespace matches folder path: `MudBlazorDemo.Client.Services`
- PascalCase for all files and folders
- One class per file

### Services (Interface-First)
```csharp
// MudBlazorDemo.Client/Services/IWeatherForecastService.cs
public interface IWeatherForecastService
{
    Task<IEnumerable<WeatherForecast>> GetForecastAsync();
}

// Register in Program.cs
builder.Services.AddScoped<IWeatherForecastService, WeatherForecastService>();
```

### Razor Components
```razor
@page "/weather"
@inject IWeatherForecastService WeatherService

<MudTable Items="forecasts" Hover="true">
    <!-- template -->
</MudTable>

@code {
    private IEnumerable<WeatherForecast> forecasts = [];

    protected override async Task OnInitializedAsync()
    {
        forecasts = await WeatherService.GetForecastAsync();
    }
}
```

### REST API Controllers
```csharp
[Route("api/[controller]")]
[ApiController]
public class SampleController : ControllerBase
{
    [HttpGet("weather")]
    public ActionResult<IEnumerable<WeatherForecast>> GetWeather()
    {
        // Return data with proper status codes
        return Ok(forecasts);
    }
}
```

---

## 📖 Documentation Guide

### For Developers
- Start with [AGENTS.md](AGENTS.md) to understand project structure and conventions
- Review [MudBlazor Docs](https://mudblazor.com/) for component usage
- Check component examples in [MudBlazorDemo.Client/Pages/](MudBlazorDemo.Client/Pages/)

### For DevOps / Cloud Teams
- Read [OPENSHIFT_SETUP_SUMMARY.md](OPENSHIFT_SETUP_SUMMARY.md) for quick overview
- Follow [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md) step-by-step
- Reference [openshift/README.md](openshift/README.md) for manifest details

### For AI Coding Agents
- Review [AGENTS.md](AGENTS.md) for:
  - Build/run commands
  - Architecture decisions
  - Code conventions
  - Common pitfalls & solutions
  - Exemplary file patterns

---

## 🔧 Common Tasks

### Add a New API Endpoint

1. **Create controller method** in [MudBlazorDemo/Controllers/SampleController.cs](MudBlazorDemo/Controllers/SampleController.cs)
2. **Add route & HTTP verb**: `[HttpGet("endpoint")]`
3. **Return `IActionResult`**: `Ok()`, `BadRequest()`, `NotFound()`
4. **Create service interface** in client: `MudBlazorDemo.Client/Services/IMyService.cs`
5. **Implement service** with HttpClient
6. **Inject & use** in Razor component

### Add a New Page

1. **Create `.razor` file** in [MudBlazorDemo.Client/Pages/](MudBlazorDemo.Client/Pages/)
2. **Add route**: `@page "/my-page"`
3. **Inject services**: `@inject IMyService MyService`
4. **Load data** in `OnInitializedAsync()`
5. **Use MudBlazor components** for UI
6. **Add to NavMenu** in [MudBlazorDemo.Client/Layout/NavMenu.razor](MudBlazorDemo.Client/Layout/NavMenu.razor)

### Deploy to OpenShift

1. **Build image**: `docker build -t registry/mudblazor-demo:1.0.0 .`
2. **Create secret**: `oc create secret generic oauth-proxy-secret ...`
3. **Deploy**: `oc apply -f openshift/deployment.yaml`
4. **Monitor**: `oc logs -f deployment/mudblazor-demo`

---

## 🐛 Troubleshooting

### Build Issues

| Error | Solution |
|-------|----------|
| "types not found" | Run `dotnet build` to compile .Client project |
| NuGet resolution fails | Clear cache: `dotnet nuget locals all --clear` |
| Hot-reload not working | Restart `dotnet watch` |

### Runtime Issues

| Error | Solution |
|-------|----------|
| 404 on `/api/...` endpoints | Check route in controller: `[Route("api/[controller]")]` |
| Component not rendering | Verify `@page` directive and `@rendermode` |
| Auth fails | See [OPENSHIFT_DEPLOYMENT_GUIDE.md#8-troubleshooting](OPENSHIFT_DEPLOYMENT_GUIDE.md#8-troubleshooting) |

**See also:**
- [OPENSHIFT_DEPLOYMENT_GUIDE.md#8-troubleshooting](OPENSHIFT_DEPLOYMENT_GUIDE.md#8-troubleshooting) — OpenShift deployment troubleshooting
- [AGENTS.md — Common Pitfalls & Solutions](AGENTS.md#common-pitfalls--solutions) — Codebase-specific gotchas

---

## 📝 License

[Included in LICENSE.txt](LICENSE.txt)

---

## 🔗 Useful Links

### Official Documentation
- [Blazor Web 10.0](https://learn.microsoft.com/en-us/aspnet/core/blazor/) — Microsoft Blazor docs
- [MudBlazor](https://mudblazor.com/) — Material Design component library
- [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/) — Framework documentation
- [OpenShift Docs](https://docs.openshift.com/) — Red Hat OpenShift documentation

### In This Repository
- [AGENTS.md](AGENTS.md) — AI agent guide
- [ENTRA_ID_SETUP_GUIDE.md](ENTRA_ID_SETUP_GUIDE.md) — Azure Entra ID registration and configuration
- [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md) — Deployment walkthrough
- [OPENSHIFT_SETUP_SUMMARY.md](OPENSHIFT_SETUP_SUMMARY.md) — Quick setup reference
- [openshift/README.md](openshift/README.md) — Kubernetes manifest reference
- [Dockerfile](Dockerfile) — Container image definition

---

**Questions?** See [AGENTS.md](AGENTS.md) for detailed project information, or [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md) for deployment help.