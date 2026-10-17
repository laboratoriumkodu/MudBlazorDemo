# OpenShift Deployment Files

This directory contains Kubernetes/OpenShift manifests for deploying MudBlazorDemo.

## Files

- **deployment.yaml** — Namespace, Deployment, Service, Route, ServiceAccount, RBAC resources
- **buildconfig.yaml** — OpenShift BuildConfig for automated container image builds from Git

## Quick Start

```bash
# Deploy all resources
oc apply -f deployment.yaml
oc apply -f buildconfig.yaml

# Check status
oc get pods -n mudblazor-demo
oc get route -n mudblazor-demo

# View logs
oc logs -f deployment/mudblazor-demo -n mudblazor-demo
```

## Configuration

### Image Registry

Update the image reference in `deployment.yaml`:

```yaml
image: image-registry.openshift-image-registry.svc:5000/mudblazor-demo/mudblazor-demo:latest
```

Options:
- **Internal OpenShift registry**: `image-registry.openshift-image-registry.svc:5000/mudblazor-demo/mudblazor-demo:latest`
- **Docker Hub**: `docker.io/your-username/mudblazor-demo:latest`
- **Quay.io**: `quay.io/your-org/mudblazor-demo:latest`

### Git Repository (BuildConfig)

Update the Git URL in `buildconfig.yaml`:

```yaml
git:
  uri: https://github.com/your-org/your-repo.git
  ref: main
```

## Security Context

The deployment runs as a non-root user (UID 1001) with minimal capabilities:

```yaml
securityContext:
  runAsNonRoot: true
  runAsUser: 1001
  fsGroup: 1001
```

## Resource Requests/Limits

CPU/Memory resource allocation:

```yaml
resources:
  requests:
    cpu: 100m
    memory: 256Mi
  limits:
    cpu: 500m
    memory: 512Mi
```

Adjust these based on your load testing results.

## Environment Variables

Configure via deployment.yaml `env` section:

- `ASPNETCORE_ENVIRONMENT`: `Production`
- `ASPNETCORE_URLS`: `http://+:8080`

`ASPNETCORE_FORWARDEDHEADERS_ENABLED` is intentionally not set. Real client IP forwarding is configured on the Route (`set-forwarded-headers: replace`), oauth2-proxy (`--reverse-proxy`, `--real-client-ip-header`) and in `Program.cs` (`ForwardedHeaders` section in `appsettings.Production.json`). See *Real Client IP* in [OPENSHIFT_DEPLOYMENT_GUIDE.md](../OPENSHIFT_DEPLOYMENT_GUIDE.md).

## See Also

- [OPENSHIFT_DEPLOYMENT_GUIDE.md](../OPENSHIFT_DEPLOYMENT_GUIDE.md) — Full setup instructions
- [Dockerfile](../Dockerfile) — Container image definition
