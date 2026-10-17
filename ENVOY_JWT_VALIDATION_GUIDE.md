# Envoy JWT Validation Sidecar — Keycloak Tokens with Group-Based Access

This guide replaces the **oauth2-proxy** sidecar with an **Envoy** sidecar that only *validates* a JWT
issued by Keycloak (Red Hat build of Keycloak / RH-SSO). Login, session handling and token refresh
happen **in front of OpenShift** (edge / gateway). The application code stays unchanged: Envoy turns
token claims into the `X-Remote-*` headers that
[ProxyAuthMiddleware](MudBlazorDemo/Middleware/ProxyAuthMiddleware.cs) already reads.

> Replace the example values (`sso.example.com`, `jwks.example.com`, group names) with your own.

---

## Table of Contents

1. [Architecture](#1-architecture)
2. [How Envoy Works](#2-how-envoy-works)
3. [Prerequisites](#3-prerequisites)
4. [Keycloak Configuration](#4-keycloak-configuration)
5. [Envoy Configuration (ConfigMap)](#5-envoy-configuration-configmap)
6. [CA Certificate for the JWKS Endpoint](#6-ca-certificate-for-the-jwks-endpoint)
7. [Deployment Changes](#7-deployment-changes)
8. [Network Policies](#8-network-policies)
9. [Deploy](#9-deploy)
10. [Testing](#10-testing)
11. [Troubleshooting](#11-troubleshooting)
12. [Security Notes](#12-security-notes)
13. [Variations](#13-variations)

---

## 1. Architecture

```
Browser
  │  (login to Keycloak happens at the edge, NOT in the pod)
  ▼
Edge / Gateway  ──  adds  Jwt-Assertion: <access_token>  to EVERY request
  │                 (including the /_blazor WebSocket upgrade and /api/* calls)
  ▼
OpenShift Router (TLS edge termination)
  ▼
Service :80 ──► Pod
                ├── envoy (:4180)          validates JWT + checks groups
                │     • strips client-supplied X-Remote-* headers
                │     • 401 if token missing/invalid
                │     • 403 if user is not in an allowed group
                │     • sets X-Remote-User / -Email / -Name from claims
                └── mudblazor-demo (:8080) unchanged app (ProxyAuthMiddleware)
```

| Value | Example |
|---|---|
| Token issuer (`iss`) | `https://sso.example.com/auth/realms/master` |
| JWKS (public signing keys) | `https://jwks.example.com/api/keys` |
| Signing algorithm | `RS256` |
| Authorization | `groups` claim, e.g. `/mudblazor-users`, `/mudblazor-admins` |

### How the token reaches Envoy

The component in front of OpenShift sends the token as a raw JWT in the `Jwt-Assertion` header
(no `Bearer ` prefix):

```
GET / HTTP/1.1
Host: mudblazor-demo.apps.example.com
Jwt-Assertion: eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6...
```

- The **OpenShift Route** (edge TLS) decrypts HTTPS, leaves `Jwt-Assertion` untouched, adds
  `X-Forwarded-*`, and forwards plain HTTP to the Envoy port `4180`.
- **Envoy** reads the token from `Jwt-Assertion` (`from_headers`), validates it, and removes the header
  before forwarding (`forward: false`), so the app never receives the raw token — only `X-Remote-*`.
- The header must be present on **every** request, including the `/_blazor` WebSocket upgrade and `/api/*`.
- Envoy ignores tokens in any other header (e.g. `Authorization`); only `Jwt-Assertion` is checked.
- Large tokens (many groups) can exceed the router header buffer (`400`/`431` at the router). Ask the
  cluster admin to raise `tuningOptions.headerBufferBytes` on the IngressController, or reduce the
  number of groups in the token.

Envoy **never calls the issuer** (`sso.example.com`). It downloads only the public keys from the JWKS
endpoint, caches them, and validates every token locally, in memory. No client secret is needed in the pod.

### oauth2-proxy vs. Envoy

| | oauth2-proxy (current) | Envoy (this guide) |
|---|---|---|
| Redirects user to login | ✅ | ❌ (done at the edge) |
| Session cookie / token refresh | ✅ | ❌ (done at the edge) |
| Needs client secret in pod | ✅ | ❌ |
| Validates bearer JWT | partially | ✅ |
| Group / role authorization | limited | ✅ (RBAC filter) |
| Strips spoofed identity headers | ❌ | ✅ |

---

## 2. How Envoy Works

Envoy is a high-performance L7 proxy (CNCF project, the data plane of Istio / OpenShift Service Mesh).
It is configured with a YAML file built from four concepts:

| Concept | Meaning | In this setup |
|---|---|---|
| **Listener** | Port Envoy accepts connections on | `0.0.0.0:4180` |
| **HTTP filters** | Ordered pipeline; any filter can stop the request | `health_check` → `header_mutation` → `jwt_authn` → `rbac` → `router` |
| **Route** | Decides where a request goes | `/` → cluster `app` |
| **Cluster** | Upstream destination | `app` = `127.0.0.1:8080`, `jwks` = `jwks.example.com:443` |

Request flow:

1. `health_check` answers `/healthz` itself (Kubernetes probes have no token).
2. `header_mutation` removes any `X-Remote-*` sent by the client (anti-spoofing).
3. `jwt_authn` reads the raw JWT from the `Jwt-Assertion` header, verifies the signature against the cached JWKS,
   checks `iss`, `exp`, `nbf` → **401** on failure. On success it copies claims into headers and
   stores the token payload in request metadata.
4. `rbac` checks the `groups` claim from that metadata → **403** if no allowed group matches.
5. `router` forwards the request (HTTP or WebSocket) to the app on `localhost:8080`.

The JWKS is fetched at startup, cached (`cache_duration`) and refreshed periodically, so key rotation
is handled automatically.

---

## 3. Prerequisites

- OpenShift 4.x project (`mudblazor-demo`) with the app already deployed
  (see [OPENSHIFT_DEPLOYMENT_GUIDE.md](OPENSHIFT_DEPLOYMENT_GUIDE.md)).
- An edge component that authenticates users against Keycloak and forwards the **access token** as
  `Jwt-Assertion: <token>` (raw JWT, no `Bearer ` prefix) on **every** request, and refreshes it before it expires.
- Network egress from the pod to `jwks.example.com:443`.
- The CA certificate that signed the TLS certificate of `jwks.example.com` (if it is an internal CA).

Verify the endpoints from a machine with access:

```bash
# Issuer — must match the "iss" claim of your tokens exactly
curl -s https://sso.example.com/auth/realms/master/.well-known/openid-configuration | jq -r .issuer
# → https://sso.example.com/auth/realms/master

# JWKS — must return {"keys":[...]} containing the kid used by your tokens
curl -s https://jwks.example.com/api/keys | jq -r '.keys[] | "\(.kid) \(.alg)"'
```

---

## 4. Keycloak Configuration

### 4.1 Add the `groups` claim to the access token

Keycloak Admin Console → realm **master** → **Clients** → *(client used by the edge)* →
**Client scopes** → `<client>-dedicated` → **Add mapper** → **By configuration** → **Group Membership**:

| Field | Value |
|---|---|
| Name | `groups` |
| Token Claim Name | `groups` |
| Full group path | **ON** → `"/mudblazor-users"` (recommended, unambiguous) |
| Add to ID token | optional |
| Add to access token | **ON** |
| Add to userinfo | optional |

### 4.2 Create groups and assign users

**Groups** → create `mudblazor-users` (and optionally `mudblazor-admins`) →
**Users** → *(user)* → **Groups** → **Join group**.

### 4.3 Check a token

```bash
TOKEN="<access token>"
echo "$TOKEN" | cut -d. -f2 | tr '_-' '/+' | base64 -d 2>/dev/null | jq '{iss, aud, azp, exp, preferred_username, email, name, groups}'
```

PowerShell:

```powershell
$p = $TOKEN.Split('.')[1].Replace('-','+').Replace('_','/'); $p += '=' * ((4 - $p.Length % 4) % 4)
[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json | Select iss, aud, azp, preferred_username, email, name, groups
```

Expected:

```json
{
  "iss": "https://sso.example.com/auth/realms/master",
  "preferred_username": "jdoe",
  "email": "jdoe@example.com",
  "name": "John Doe",
  "groups": ["/mudblazor-users"]
}
```

Also check the token header (`cut -d. -f1`): its `kid` must be present in `https://jwks.example.com/api/keys`.

---

## 5. Envoy Configuration (ConfigMap)

Save as `openshift/envoy-configmap.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: envoy-jwt
  namespace: mudblazor-demo
  labels:
    app: mudblazor-demo
data:
  envoy.yaml: |
    static_resources:
      listeners:
      - name: ingress
        address:
          socket_address: { address: 0.0.0.0, port_value: 4180 }
        filter_chains:
        - filters:
          - name: envoy.filters.network.http_connection_manager
            typed_config:
              "@type": type.googleapis.com/envoy.extensions.filters.network.http_connection_manager.v3.HttpConnectionManager
              stat_prefix: ingress
              # Keep X-Forwarded-For / X-Forwarded-Proto from the OpenShift router unchanged,
              # so the app (trusting loopback) sees the real client IP and https scheme.
              use_remote_address: false
              upgrade_configs:
              - upgrade_type: websocket          # Blazor Server /_blazor (SignalR)
              stream_idle_timeout: 3600s
              access_log:
              - name: envoy.access_loggers.stdout
                typed_config:
                  "@type": type.googleapis.com/envoy.extensions.access_loggers.stream.v3.StdoutAccessLog
              route_config:
                name: local_route
                virtual_hosts:
                - name: app
                  domains: ["*"]
                  routes:
                  - match: { prefix: "/" }
                    route:
                      cluster: app
                      timeout: 0s                # no limit (long-lived WebSockets)
              http_filters:

              # 1) Probe endpoint answered by Envoy (no token required)
              - name: envoy.filters.http.health_check
                typed_config:
                  "@type": type.googleapis.com/envoy.extensions.filters.http.health_check.v3.HealthCheck
                  pass_through_mode: false
                  headers:
                  - name: ":path"
                    string_match: { exact: "/healthz" }

              # 2) Anti-spoofing: remove identity headers sent by the client
              - name: envoy.filters.http.header_mutation
                typed_config:
                  "@type": type.googleapis.com/envoy.extensions.filters.http.header_mutation.v3.HeaderMutation
                  mutations:
                    request_mutations:
                    - remove: x-remote-user
                    - remove: x-remote-email
                    - remove: x-remote-name

              # 3) JWT validation (signature, issuer, expiry) + claims -> headers
              - name: envoy.filters.http.jwt_authn
                typed_config:
                  "@type": type.googleapis.com/envoy.extensions.filters.http.jwt_authn.v3.JwtAuthentication
                  providers:
                    keycloak:
                      issuer: https://sso.example.com/auth/realms/master
                      # No "audiences": aud is NOT checked; access is decided by groups (RBAC below).
                      remote_jwks:
                        http_uri:
                          uri: https://jwks.example.com/api/keys
                          cluster: jwks
                          timeout: 5s
                        cache_duration: 3600s
                        async_fetch:
                          fast_listener: false   # listener starts only after keys are loaded
                        retry_policy:
                          num_retries: 3
                      from_headers:
                      - name: Jwt-Assertion       # raw JWT, no "Bearer " prefix
                      forward: false             # remove the token header before forwarding to the app
                      clock_skew_seconds: 60
                      payload_in_metadata: jwt_payload
                      claim_to_headers:
                      - { header_name: x-remote-user,  claim_name: preferred_username }
                      - { header_name: x-remote-email, claim_name: email }
                      - { header_name: x-remote-name,  claim_name: name }
                  rules:
                  - match: { prefix: "/" }
                    requires: { provider_name: keycloak }

              # 4) Authorization by group membership (any listed group is enough)
              - name: envoy.filters.http.rbac
                typed_config:
                  "@type": type.googleapis.com/envoy.extensions.filters.http.rbac.v3.RBAC
                  rules:
                    action: ALLOW
                    policies:
                      allowed-groups:
                        permissions:
                        - any: true
                        principals:
                        - or_ids:
                            ids:
                            - metadata:
                                filter: envoy.filters.http.jwt_authn
                                path: [ { key: jwt_payload }, { key: groups } ]
                                value:
                                  list_match:
                                    one_of: { string_match: { exact: "/mudblazor-users" } }
                            - metadata:
                                filter: envoy.filters.http.jwt_authn
                                path: [ { key: jwt_payload }, { key: groups } ]
                                value:
                                  list_match:
                                    one_of: { string_match: { exact: "/mudblazor-admins" } }

              # 5) Forward to the app
              - name: envoy.filters.http.router
                typed_config:
                  "@type": type.googleapis.com/envoy.extensions.filters.http.router.v3.Router

      clusters:
      # Application container in the same pod
      - name: app
        type: STATIC
        connect_timeout: 2s
        load_assignment:
          cluster_name: app
          endpoints:
          - lb_endpoints:
            - endpoint:
                address:
                  socket_address: { address: 127.0.0.1, port_value: 8080 }

      # JWKS endpoint (public signing keys)
      - name: jwks
        type: LOGICAL_DNS
        dns_lookup_family: V4_ONLY
        connect_timeout: 5s
        load_assignment:
          cluster_name: jwks
          endpoints:
          - lb_endpoints:
            - endpoint:
                address:
                  socket_address: { address: jwks.example.com, port_value: 443 }
        transport_socket:
          name: envoy.transport_sockets.tls
          typed_config:
            "@type": type.googleapis.com/envoy.extensions.transport_sockets.tls.v3.UpstreamTlsContext
            sni: jwks.example.com
            common_tls_context:
              validation_context:
                trusted_ca: { filename: /etc/envoy-ca/ca.crt }
                match_typed_subject_alt_names:
                - san_type: DNS
                  matcher: { suffix: ".example.com" }   # wildcard *.example.com certificate
```

### Key settings

| Setting | Purpose |
|---|---|
| `issuer` | Only tokens whose `iss` equals this value are accepted. **Critical** when the JWKS endpoint publishes keys of several systems. |
| no `audiences` | `aud` is not checked; access is decided by the RBAC group check. |
| `payload_in_metadata` | Makes the token claims available to the RBAC filter. |
| `claim_to_headers` | Maps claims to the headers read by `ProxyAuthMiddleware`. |
| `fast_listener: false` | Pod becomes ready only after keys are loaded (no 401 burst at startup). |
| `use_remote_address: false` | Preserves `X-Forwarded-For`/`-Proto` set by the OpenShift router. |
| `list_match.one_of` | Matches if any element of the `groups` array equals the value. |

---

## 6. CA Certificate for the JWKS Endpoint

Envoy must trust the TLS certificate of `jwks.example.com`.

**Option A — internal CA file** (PEM, include intermediate + root):

```bash
oc create configmap jwks-ca -n mudblazor-demo --from-file=ca.crt=corporate-ca-chain.pem
```

**Option B — cluster trusted CA bundle** (if the CA is already in the cluster proxy `trustedCA`):

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: jwks-ca
  namespace: mudblazor-demo
  labels:
    config.openshift.io/inject-trusted-cabundle: "true"
```

With Option B the key is `ca-bundle.crt`: set `trusted_ca.filename` to `/etc/envoy-ca/ca-bundle.crt`.

**Option C — public CA:** use the bundle inside the Envoy image and skip the volume:
`trusted_ca: { filename: /etc/ssl/certs/ca-certificates.crt }`.

---

## 7. Deployment Changes

Edit [openshift/deployment.yaml](openshift/deployment.yaml):

### 7.1 Replace the `oauth-proxy` container

```yaml
      containers:
      - name: envoy
        image: docker.io/envoyproxy/envoy:v1.31-latest
        imagePullPolicy: IfNotPresent
        args: ["-c", "/etc/envoy/envoy.yaml", "--log-level", "info"]
        ports:
        - name: envoy
          containerPort: 4180
          protocol: TCP
        volumeMounts:
        - { name: envoy-config, mountPath: /etc/envoy,    readOnly: true }
        - { name: jwks-ca,      mountPath: /etc/envoy-ca, readOnly: true }
        resources:
          requests: { cpu: 50m,  memory: 64Mi }
          limits:   { cpu: 200m, memory: 256Mi }
        livenessProbe:
          httpGet: { path: /healthz, port: envoy }
          initialDelaySeconds: 5
          periodSeconds: 10
        readinessProbe:
          httpGet: { path: /healthz, port: envoy }
          initialDelaySeconds: 2
          periodSeconds: 5
        securityContext:
          allowPrivilegeEscalation: false
          readOnlyRootFilesystem: true
          capabilities:
            drop: [ALL]

      - name: mudblazor-demo
        # ... unchanged ...

      volumes:
      - name: envoy-config
        configMap: { name: envoy-jwt }
      - name: jwks-ca
        configMap: { name: jwks-ca }
```

> Use an image mirrored to your internal registry if Docker Hub is not reachable.

### 7.2 Service — point to the Envoy port

```yaml
spec:
  ports:
  - name: http
    port: 80
    targetPort: envoy
    protocol: TCP
```

### 7.3 Remove obsolete resources

- Delete the `oauth-proxy-secret` Secret (no client secret is needed anymore).
- The Route stays unchanged (edge TLS, `haproxy.router.openshift.io/set-forwarded-headers: replace`).
- The app container and its forwarded-headers configuration stay unchanged: Envoy connects from
  `127.0.0.1`, which the app already trusts.

---

## 8. Network Policies

Only the OpenShift router may reach the pod; the app port 8080 is only reachable via localhost:

```yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-from-router
  namespace: mudblazor-demo
spec:
  podSelector:
    matchLabels: { app: mudblazor-demo }
  policyTypes: [Ingress]
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          network.openshift.io/policy-group: ingress
    ports:
    - { port: 4180, protocol: TCP }
```

If egress is restricted (NetworkPolicy egress / `EgressFirewall`), allow DNS and
`jwks.example.com:443`.

> Envoy does **not** use `HTTP_PROXY`/`HTTPS_PROXY`. If the JWKS host is reachable only through a proxy,
> use `local_jwks` instead (see [Variations](#13-variations)).

---

## 9. Deploy

```bash
oc apply -f openshift/envoy-configmap.yaml
oc create configmap jwks-ca -n mudblazor-demo --from-file=ca.crt=corporate-ca-chain.pem
oc apply -f openshift/deployment.yaml
oc delete secret oauth-proxy-secret -n mudblazor-demo --ignore-not-found

oc rollout status deployment/mudblazor-demo -n mudblazor-demo
oc logs deployment/mudblazor-demo -c envoy -n mudblazor-demo
```

Validate the Envoy configuration before deploying:

```bash
# Extract envoy.yaml from the ConfigMap, then:
podman run --rm -v "$PWD/envoy.yaml:/etc/envoy/envoy.yaml:Z" \
  docker.io/envoyproxy/envoy:v1.31-latest --mode validate -c /etc/envoy/envoy.yaml
```

After changing the ConfigMap, restart the pods (Envoy does not reload static config):

```bash
oc rollout restart deployment/mudblazor-demo -n mudblazor-demo
```

---

## 10. Testing

```bash
ROUTE=https://mudblazor-demo.apps.example.com

# 1. Health probe (no token)                   → 200
curl -i $ROUTE/healthz

# 2. No token                                  → 401  "Jwt is missing"
curl -i $ROUTE/

# 3. Invalid / expired token                   → 401  "Jwt verification fails" / "Jwt is expired"
curl -i -H "Jwt-Assertion: abc.def.ghi" $ROUTE/

# 4. Valid token, user NOT in allowed group    → 403  "RBAC: access denied"
curl -i -H "Jwt-Assertion: $TOKEN_NO_GROUP" $ROUTE/

# 5. Valid token, user in /mudblazor-users     → 200
curl -i -H "Jwt-Assertion: $TOKEN" $ROUTE/

# 6. Spoofed header is ignored                 → app shows the token user, not "admin"
curl -s -H "Jwt-Assertion: $TOKEN" -H "X-Remote-User: admin" $ROUTE/api/...
```

In the browser (through the edge), open the **Auth Info** page of the app: it should show the user
from `preferred_username`, `email` and `name`.

Obtain a test token (user flow, if direct grant is enabled for a test client):

```bash
TOKEN=$(curl -s https://sso.example.com/auth/realms/master/protocol/openid-connect/token \
  -d grant_type=password -d client_id=<test-client> -d username=<user> -d password=<pwd> \
  | jq -r .access_token)
```

---

## 11. Troubleshooting

Enable debug logging temporarily: change `--log-level info` to `--log-level debug` (or
`--component-log-level jwt:debug,rbac:debug`).

| Symptom / log message | Cause | Fix |
|---|---|---|
| `401 Jwt is missing` | No `Jwt-Assertion` header reached the pod (or header name misspelled) | Check that the edge forwards the token on every request (incl. WebSocket upgrade) |
| `401 Jwt issuer is not configured` | `iss` ≠ configured `issuer` | Copy `iss` exactly from a decoded token (watch `/auth`, trailing `/`, http vs https) |
| `401 Jwks doesn't have key to match kid or alg from Jwt` | Token `kid` not in JWKS | Check `curl https://jwks.example.com/api/keys`; key rotation not yet published |
| `401 Jwks remote fetch is failed` | Envoy cannot reach JWKS | TLS CA, SNI/SAN, DNS, egress policy, proxy requirement |
| `401 Jwt is expired` | Token too old | Edge must refresh tokens; check pod clock / `clock_skew_seconds` |
| `403 RBAC: access denied` | `groups` claim missing or no match | Check Group Membership mapper (access token ON), full path `/` prefix, exact spelling |
| Pod not Ready | `fast_listener: false` waits for JWKS | Fix JWKS connectivity (see Envoy logs) |
| Blazor UI stuck / reconnecting | WebSocket upgrade blocked or token missing on `/_blazor` | `upgrade_configs: websocket`; edge must add token to the upgrade request |
| Redirect loop | App sees `http` scheme | Keep `use_remote_address: false` so `X-Forwarded-Proto: https` from the router is preserved |

Useful commands:

```bash
oc logs deployment/mudblazor-demo -c envoy -n mudblazor-demo --tail=100
oc exec deployment/mudblazor-demo -c envoy -- cat /etc/envoy/envoy.yaml
```

Optional admin interface (stats, config dump) — add to `envoy.yaml`, local only:

```yaml
admin:
  address:
    socket_address: { address: 127.0.0.1, port_value: 9901 }
```

```bash
oc port-forward deployment/mudblazor-demo 9901:9901 -n mudblazor-demo
curl -s localhost:9901/stats | grep -E 'jwt_authn|rbac'
```

---

## 12. Security Notes

- **Issuer is the trust boundary.** The JWKS endpoint may publish keys of several systems; the
  `issuer` check ensures only tokens from `https://sso.example.com/auth/realms/master` are accepted.
- **No audience check.** Any token from this realm for *any* client is accepted if the user is in an
  allowed group. Use groups dedicated to this application, and optionally also check `azp`
  (see [Variations](#13-variations)).
- **Header spoofing** is prevented by stripping `X-Remote-*` before validation. Make sure port 8080
  is never exposed by the Service or Route.
- **Long-lived Blazor circuits** are authorized only when the WebSocket is opened. Keep circuit
  lifetime short or force reconnects at the edge if immediate revocation matters.
- **Token revocation** is not checked (offline validation). Keep access-token lifetime short in Keycloak.
- The `master` realm is Keycloak's administrative realm; prefer a dedicated realm for application users.

---

## 13. Variations

### Require group AND client (`azp`)

```yaml
principals:
- and_ids:
    ids:
    - metadata:
        filter: envoy.filters.http.jwt_authn
        path: [ { key: jwt_payload }, { key: groups } ]
        value: { list_match: { one_of: { string_match: { exact: "/mudblazor-users" } } } }
    - metadata:
        filter: envoy.filters.http.jwt_authn
        path: [ { key: jwt_payload }, { key: azp } ]
        value: { string_match: { exact: "edge-client-id" } }
```

### Group prefix (all subgroups)

```yaml
value: { list_match: { one_of: { string_match: { prefix: "/mudblazor/" } } } }
```

### Keycloak roles instead of groups

```yaml
path: [ { key: jwt_payload }, { key: realm_access }, { key: roles } ]                       # realm roles
path: [ { key: jwt_payload }, { key: resource_access }, { key: <client> }, { key: roles } ] # client roles
```

### Admin-only paths

Add a second policy and restrict paths with `permissions`, e.g. only `/mudblazor-admins` for `/admin`:

```yaml
policies:
  admins-admin-area:
    permissions:
    - url_path: { path: { prefix: "/admin" } }
    principals:
    - metadata:
        filter: envoy.filters.http.jwt_authn
        path: [ { key: jwt_payload }, { key: groups } ]
        value: { list_match: { one_of: { string_match: { exact: "/mudblazor-admins" } } } }
  users-everything-else:
    permissions:
    - not_rule: { url_path: { path: { prefix: "/admin" } } }
    principals:
    - or_ids:
        ids:
        - metadata:
            filter: envoy.filters.http.jwt_authn
            path: [ { key: jwt_payload }, { key: groups } ]
            value: { list_match: { one_of: { string_match: { exact: "/mudblazor-users" } } } }
        - metadata:
            filter: envoy.filters.http.jwt_authn
            path: [ { key: jwt_payload }, { key: groups } ]
            value: { list_match: { one_of: { string_match: { exact: "/mudblazor-admins" } } } }
```

### Token in another header

Accept several locations (Envoy uses the first one present):

```yaml
from_headers:
- name: Jwt-Assertion                  # raw JWT
- name: Authorization                  # "Bearer <JWT>"
  value_prefix: "Bearer "
```

If the app itself needs the raw token, set `forward: true` (keeps the original header) or
`forward_payload_header: x-jwt-payload` (base64url-encoded claims only).

### Static keys (no egress to JWKS)

```yaml
local_jwks:
  filename: /etc/envoy-jwks/jwks.json   # mounted from a ConfigMap; update manually on key rotation
```

### Accept a second issuer

Add another provider (same or different `remote_jwks`) and require either:

```yaml
rules:
- match: { prefix: "/" }
  requires:
    requires_any:
      requirements:
      - provider_name: keycloak
      - provider_name: other-issuer
```

---

## References

- [Envoy JWT Authentication filter](https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_filters/jwt_authn_filter)
- [Envoy RBAC filter](https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_filters/rbac_filter)
- [Keycloak — Group Membership mapper](https://www.keycloak.org/docs/latest/server_admin/#_protocol-mappers)
- [OpenShift — Network policies](https://docs.openshift.com/container-platform/latest/networking/network_policy/about-network-policy.html)
