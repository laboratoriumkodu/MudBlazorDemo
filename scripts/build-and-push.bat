@REM build-and-push.bat - Build Docker image and push to registry (Windows)
@REM Usage: build-and-push.bat [registry] [username] [tag]

@echo off
setlocal enabledelayedexpansion

REM Configuration
set REGISTRY=%1
set USERNAME=%2
set TAG=%3

if "!REGISTRY!"=="" set REGISTRY=docker.io
if "!USERNAME!"=="" set USERNAME=your-username
if "!TAG!"=="" set TAG=latest

set IMAGE_NAME=mudblazor-demo
set FULL_IMAGE=!REGISTRY!/!USERNAME!/!IMAGE_NAME!:!TAG!

echo.
echo 🐳 Building Docker image...
echo    Image: !FULL_IMAGE!
echo.

docker build -t "!FULL_IMAGE!" .

if !errorlevel! neq 0 (
    echo ❌ Build failed
    exit /b 1
)

echo.
echo ✅ Build complete
echo.

set /p PUSH="📤 Push to registry? (y/n): "
if /i "!PUSH!"=="y" (
    docker login !REGISTRY!
    echo.
    echo 📤 Pushing image...
    docker push "!FULL_IMAGE!"
    if !errorlevel! equ 0 (
        echo.
        echo ✅ Push complete: !FULL_IMAGE!
    ) else (
        echo ❌ Push failed
        exit /b 1
    )
) else (
    echo ⏭️  Skipped push
)

echo.
echo 📋 Next steps:
echo    1. Update image in openshift\deployment.yaml
echo    2. Deploy: oc apply -f openshift\deployment.yaml
echo    3. Check: oc get pods -n mudblazor-demo
echo.
