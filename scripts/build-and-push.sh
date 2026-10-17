#!/bin/bash
# build-and-push.sh - Build Docker image and push to registry
# Usage: ./scripts/build-and-push.sh [registry] [tag]

set -e

# Configuration
REGISTRY="${1:-docker.io}"
USERNAME="${2:-your-username}"
IMAGE_NAME="mudblazor-demo"
TAG="${3:-latest}"
FULL_IMAGE="$REGISTRY/$USERNAME/$IMAGE_NAME:$TAG"

echo "🐳 Building Docker image..."
echo "   Image: $FULL_IMAGE"

# Build
docker build -t "$FULL_IMAGE" .

echo "✅ Build complete"
echo ""
echo "🔐 Logging in to registry (if needed)..."
read -p "   Push to registry? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    docker login $REGISTRY
    echo "📤 Pushing image..."
    docker push "$FULL_IMAGE"
    echo "✅ Push complete: $FULL_IMAGE"
else
    echo "⏭️  Skipped push"
fi

echo ""
echo "📋 Next steps:"
echo "   1. Update image in openshift/deployment.yaml"
echo "   2. Deploy: oc apply -f openshift/deployment.yaml"
echo "   3. Check: oc get pods -n mudblazor-demo"
