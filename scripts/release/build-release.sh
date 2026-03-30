#!/usr/bin/env bash

set -euo pipefail

usage() {
  echo "Usage: $0 --tag vX.Y.Z --input <input-dir> --output <output-dir>" >&2
  exit 1
}

fail() {
  echo "$1" >&2
  exit 1
}

tag=""
input_dir=""
output_dir=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag)
      [[ $# -ge 2 ]] || usage
      tag="$2"
      shift 2
      ;;
    --input)
      [[ $# -ge 2 ]] || usage
      input_dir="$2"
      shift 2
      ;;
    --output)
      [[ $# -ge 2 ]] || usage
      output_dir="$2"
      shift 2
      ;;
    *)
      usage
      ;;
  esac
done

[[ -n "$tag" && -n "$input_dir" && -n "$output_dir" ]] || usage
[[ "$tag" =~ ^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] || fail "Tag must match vX.Y.Z without zero-padded segments."
command -v zip >/dev/null 2>&1 || fail "zip is required to create release archives."

version="${tag#v}"

required_files=(
  "FFXIVTelegram.dll"
  "FFXIVTelegram.deps.json"
  "FFXIVTelegram.json"
)

for required_file in "${required_files[@]}"; do
  [[ -f "$input_dir/$required_file" ]] || fail "Missing required file: $input_dir/$required_file"
done

mkdir -p "$output_dir"

stage_dir="$(mktemp -d)"
trap 'rm -rf "$stage_dir"' EXIT

for required_file in "${required_files[@]}"; do
  cp "$input_dir/$required_file" "$stage_dir/$required_file"
done

staged_manifest="$stage_dir/FFXIVTelegram.json"
sed -E "s/\"AssemblyVersion\"[[:space:]]*:[[:space:]]*\"[^\"]*\"/\"AssemblyVersion\": \"$version\"/" \
  "$staged_manifest" > "$stage_dir/FFXIVTelegram.json.tmp"
mv "$stage_dir/FFXIVTelegram.json.tmp" "$staged_manifest"
grep -q "\"AssemblyVersion\": \"$version\"" "$staged_manifest" || fail "Failed to stamp AssemblyVersion in $staged_manifest"

zip_path="$output_dir/FFXIVTelegram-$version.zip"
rm -f "$zip_path"
(
  cd "$stage_dir"
  zip -q "$zip_path" FFXIVTelegram.dll FFXIVTelegram.deps.json FFXIVTelegram.json
)

last_update="$(date -u +%s)"
download_url="https://github.com/datamachine/ffxiv-telegram/releases/download/$tag/FFXIVTelegram-$version.zip"
repo_json_path="$output_dir/repo.json"

{
  printf '[\n'
  sed '$ s/}[[:space:]]*$//' "$staged_manifest"
  printf ',\n'
  printf '  "LastUpdate": %s,\n' "$last_update"
  printf '  "DownloadLinkInstall": "%s",\n' "$download_url"
  printf '  "DownloadLinkUpdate": "%s",\n' "$download_url"
  printf '  "DownloadLinkTesting": "%s"\n' "$download_url"
  printf '}\n'
  printf ']\n'
} > "$repo_json_path"
