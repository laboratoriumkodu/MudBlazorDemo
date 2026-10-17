# AGENTS.md — AI Coding Agent Guide for MudBlazorDemo

This guide helps AI coding agents understand the architecture, conventions, and best practices in the MudBlazorDemo codebase.

## Quick Start

**Build & Run:**
```bash
dotnet build                                 # Compile solution
dotnet run --project MudBlazorDemo           # Run dev server (https://localhost:7080)
dotnet watch --project MudBlazorDemo         # Run with hot-reload
dotnet publish                               # Build for production
```

## Project Overview

**MudBlazorDemo** is a modern Blazor Web 10.0 application using:
- **Server-side**: ASP.NET Core Web App with REST API controllers
- **Client-side**: Blazor WebAssembly with Material Design components
- **UI Framework**: [MudBlazor](https://mudblazor.com/) (Material Design component library)
- **Rendering**: Hybrid mode—server handles initial page load, client handles interactivity via WebAssembly
- **.NET Target**: `net10.0`

### Architecture

```
MudBlazorDemo (Server)
├── Controllers/          → REST API endpoints (route: /api/[controller])
├── Components/           → Razor components (shared across server & client)
└── Program.cs           → DI setup, MudBlazor registration, render mode config

MudBlazorDemo.Client (WebAssembly)
├── Pages/               → Routable Razor components (@page "/route")
├── Services/            → HttpClient-based API service interfaces
├── Models/              → Data transfer objects (DTOs)
├── Layout/              → MudBlazor layout components (MainLayout, NavMenu)
└── Program.cs           → WASM app bootstrap, service registration
```

## Key Conventions

### Namespaces & File Organization
- **Namespace matches folder path**: `MudBlazorDemo.Client.Services`, `MudBlazorDemo.Client.Models`
- **PascalCase** for class, folder, and namespace names
- **Singular folder names** for types: `Models/` contains model classes, `Services/` contains service interfaces

### Services (Interface-First Pattern)
- Define interface: `IWeatherForecastService` in `Services/`
- Implement service: `WeatherForecastService`
- Register in DI: `builder.Services.AddScoped<IWeatherForecastService, WeatherForecastService>()`
- Example: [MudBlazorDemo.Client/Services/](MudBlazorDemo.Client/Services/)

### Razor Components (Pages)
- Declare route: `@page "/weather"`
- Inject service: `@inject IWeatherForecastService WeatherService`
- Load data async: `OnInitializedAsync()` lifecycle hook
- Bind UI to state: Use `@bind` for two-way binding, `@on*` for event handlers
- Example: [MudBlazorDemo.Client/Pages/Weather.razor](MudBlazorDemo.Client/Pages/Weather.razor)

### REST API Endpoints (Controllers)
- Route template: `[Route("api/[controller]")]`
- Return `IActionResult` with status codes: `Ok()`, `BadRequest()`, `NotFound()`
- Named actions map to routes: `[HttpGet("weather")]` → `GET /api/sample/weather`
- Example: [MudBlazorDemo/Controllers/SampleController.cs](MudBlazorDemo/Controllers/SampleController.cs)

### Layout & MudBlazor Components
- Use `MudLayout` as root container
- Compose with `MudAppBar`, `MudDrawer`, `MudContainer`, etc.
- Theme provider at app root: `<MudThemeProvider />`, `<MudDialogProvider />`, `<MudSnackbarProvider />`
- Example: [MudBlazorDemo.Client/Layout/MainLayout.razor](MudBlazorDemo.Client/Layout/MainLayout.razor)

### HTTP Calls from WASM Client
- Use relative paths: `api/sample/weather` (not absolute URLs)
- BaseAddress is auto-configured; HttpClient is injected via DI
- Always use `async Task<T>` patterns; never blocking calls

### Data Models
- **Must be JSON-serializable**: public properties only, no circular references
- Use `[JsonPropertyName("name")]` if JSON property differs from C# property name
- DTOs should live in `Models/` folder to be referenced by both server and client (if shared)

## Common Pitfalls & Solutions

| Issue | Solution |
|-------|----------|
| WASM interop fails; prerendering clashes | Set `prerender: false` in `InteractiveWebAssemblyRenderMode` |
| Model serialization fails | Ensure public properties only, no circular refs; use `[JsonPropertyName]` if needed |
| HTTP 404 from client to API | Use relative path (e.g., `api/sample/weather`); check routing in controllers |
| Dark mode not persisted across refresh | Use localStorage or persist theme state in a service |
| MudBlazor CSS missing (unstyled components) | Verify `_content/MudBlazor/MudBlazor.min.css` is loaded in App.html |
| Build fails with "types not found" | Ensure `.Client` project is compiled; check `.csproj` for shared component paths |

## Important Files & Patterns

- **[MudBlazorDemo/Program.cs](MudBlazorDemo/Program.cs)** — Dual-render setup, DI configuration, MudBlazor registration
- **[MudBlazorDemo.Client/Program.cs](MudBlazorDemo.Client/Program.cs)** — WASM bootstrap, HttpClient base address
- **[MudBlazorDemo.Client/Pages/Weather.razor](MudBlazorDemo.Client/Pages/Weather.razor)** — Service injection, async data load, MudTable binding
- **[MudBlazorDemo.Client/Layout/MainLayout.razor](MudBlazorDemo.Client/Layout/MainLayout.razor)** — MudBlazor providers, theme, layout structure
- **[MudBlazorDemo.Client/Services/IWeatherForecastService.cs](MudBlazorDemo.Client/Services/IWeatherForecastService.cs)** — Interface-first API pattern

## When Adding Features

1. **New API Endpoint**: Add `[HttpGet/Post]` method in `Controllers/SampleController.cs`; ensure method returns `IActionResult` with proper status codes
2. **New Service**: Create interface in `Services/INewService.cs`, implement `NewService.cs`, register in `Program.cs`
3. **New Page**: Create `.razor` file in `Pages/` with `@page "/route"` and inject services via `@inject`
4. **New MUD Component**: Use `<MudButton>`, `<MudTable>`, etc.; refer to [MudBlazor docs](https://mudblazor.com/)
5. **Data Model**: Add class to `Models/`; ensure it's JSON-serializable

## Testing & Debugging

- **Hot-reload**: Use `dotnet watch` to see changes instantly
- **Network requests**: Browser DevTools → Network tab to inspect API calls
- **Blazor debugger**: Requires additional setup; check .NET documentation
- **Common breakpoints**: Component lifecycle (`OnInitializedAsync`), service methods, API responses

## References

- [Blazor documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [MudBlazor component library](https://mudblazor.com/)
- [Material Design principles](https://m3.material.io/)
- [ASP.NET Core Web API best practices](https://learn.microsoft.com/en-us/aspnet/core/web-api/)
