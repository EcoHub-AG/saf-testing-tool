#!/bin/bash
set -e

VERSION="2.0.1"
APP_NAME="SAFTestingTool"
BUNDLE_NAME="${APP_NAME}.app"
PROJECT_DIR="StandardApiFrameworkTool"
PUBLISH_DIR="publish/osx-arm64"
RELEASE_DIR="release/mac"
ZIP_NAME="${APP_NAME}-macos-arm64-v${VERSION}.zip"

echo "===> Building ${APP_NAME} v${VERSION} for osx-arm64..."
dotnet publish ${PROJECT_DIR} \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -o ${PUBLISH_DIR}

echo "===> Creating .app bundle..."
rm -rf "${RELEASE_DIR}/${BUNDLE_NAME}"
mkdir -p "${RELEASE_DIR}/${BUNDLE_NAME}/Contents/MacOS"
mkdir -p "${RELEASE_DIR}/${BUNDLE_NAME}/Contents/Resources"

# Copy all published files into the bundle
cp -r ${PUBLISH_DIR}/* "${RELEASE_DIR}/${BUNDLE_NAME}/Contents/MacOS/"

# Copy Info.plist
cp "assets/mac/Info.plist" "${RELEASE_DIR}/${BUNDLE_NAME}/Contents/"

# Copy app icon
cp "assets/mac/ecohub.icns" "${RELEASE_DIR}/${BUNDLE_NAME}/Contents/Resources/"

# Make the main executable runnable
chmod +x "${RELEASE_DIR}/${BUNDLE_NAME}/Contents/MacOS/StandardApiFrameworkTool"

echo "===> Ad-hoc signing .app bundle..."
codesign --sign - --force --deep --timestamp=none "${RELEASE_DIR}/${BUNDLE_NAME}"

echo "===> Creating zip archive..."
rm -f "${RELEASE_DIR}/${ZIP_NAME}"
ditto -c -k --keepParent "${RELEASE_DIR}/${BUNDLE_NAME}" "${RELEASE_DIR}/${ZIP_NAME}"

echo ""
echo "Done! Release ready at: ${RELEASE_DIR}/${ZIP_NAME}"
