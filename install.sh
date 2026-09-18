#!/usr/bin/env bash
set -euo pipefail

# Default Vintage Story data directory (where Mods/ lives)
DEFAULT_MODS_DIR="${HOME}/.config/VintagestoryData/Mods"

# Allow override via environment variable or first argument
MODS_DIR="${VINTAGE_STORY_DATA:-${1:-$DEFAULT_MODS_DIR}}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_NAME="Unconscious"
PROJECT_DIR="${SCRIPT_DIR}/${PROJECT_NAME}"
BUILD_CONFIG="Release"
OUTPUT_DIR="${PROJECT_DIR}/bin/${BUILD_CONFIG}/Mods/mod/publish"
TARGET_DIR="${MODS_DIR}/${PROJECT_NAME}"

# Ensure VINTAGE_STORY is set for the build
if [[ -z "${VINTAGE_STORY:-}" ]]; then
    echo "VINTAGE_STORY is not set. Trying /opt/vintagestory..."
    export VINTAGE_STORY="/opt/vintagestory"
fi

if [[ ! -f "${VINTAGE_STORY}/VintagestoryAPI.dll" ]]; then
    echo "Error: VintagestoryAPI.dll not found in \${VINTAGE_STORY} (${VINTAGE_STORY})" >&2
    echo "Set the VINTAGE_STORY environment variable to your Vintage Story installation directory." >&2
    exit 1
fi

echo "Building ${PROJECT_NAME}..."
dotnet publish "${PROJECT_DIR}/${PROJECT_NAME}.csproj" -c "${BUILD_CONFIG}" --no-restore 2>&1 | tail -n 20

if [[ ! -d "${OUTPUT_DIR}" ]]; then
    echo "Error: Build output directory not found: ${OUTPUT_DIR}" >&2
    exit 1
fi

echo "Installing to ${TARGET_DIR}..."
mkdir -p "${TARGET_DIR}"
rm -rf "${TARGET_DIR:?}/"*
cp -r "${OUTPUT_DIR}/"* "${TARGET_DIR}/"

echo "Done. Installed ${PROJECT_NAME} to ${TARGET_DIR}"
