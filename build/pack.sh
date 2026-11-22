#!/usr/bin/env bash
# Packs the three published libraries in Release and checks the packages before anyone can publish them.
# Used by the CI `pack` job; run it with `bash build/pack.sh [output-dir]` (default: artifacts/packages under the repository
# root). It never pushes anything to NuGet.
#
# Checks:
#   - exactly three .nupkg files, LokiBulkDataProcessor, .Core and .Oracle, at the <Version> in Directory.Build.props;
#   - the SQL Server and Oracle packages depend on LokiBulkDataProcessor.Core [Version, next major);
#   - no package depends on a test-only package (Testcontainers, NUnit, Moq, FluentAssertions, Microsoft.NET.Test.Sdk, coverlet);
#   - every package contains its assembly and XML documentation for the <TargetFramework>, and README.md.
set -euo pipefail

libraries=(LokiBulkDataProcessor.Core LokiBulkDataProcessor LokiBulkDataProcessor.Oracle)
test_only_packages='testcontainers|nunit|moq|fluentassertions|microsoft\.net\.test\.sdk|coverlet'

fail() {
  echo "::error::$1" >&2
  exit 1
}

# Resolve the output directory against the caller's directory, then work from the repository root.
output_dir=$(realpath -m "${1:-$(dirname "${BASH_SOURCE[0]}")/../artifacts/packages}")
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# Reads one plain property from Directory.Build.props; fails (in the caller's shell) if it is missing or malformed.
read_property() {
  local name="$1" pattern="$2" value
  value=$(grep -oP "<$name>\\K[^<]+" Directory.Build.props | head -n 1 || true)
  [[ -n "$value" && "$value" =~ $pattern ]] || fail "Could not read a valid <$name> from Directory.Build.props (found '$value')"
  printf '%s' "$value"
}

# GeneratePackageOnBuild is on for the libraries; `dotnet pack` with it on fails with NU5026, so it is turned off here.
# Only *.nupkg files are removed from the output directory, never anything else in it.
pack_libraries() {
  mkdir -p "$output_dir"
  find "$output_dir" -maxdepth 1 -type f -name '*.nupkg' -delete
  for library in "${libraries[@]}"; do
    dotnet pack "$library" -c Release -p:GeneratePackageOnBuild=false -p:ContinuousIntegrationBuild=true -o "$output_dir"
  done
}

check_package_set() {
  local version="$1" expected actual
  expected=$(printf '%s\n' "${libraries[@]/%/.$version.nupkg}" | sort)
  actual=$(find "$output_dir" -maxdepth 1 -name '*.nupkg' -printf '%f\n' | sort)
  [[ "$actual" == "$expected" ]] || fail "Expected exactly these packages:"$'\n'"$expected"$'\n'"but found:"$'\n'"$actual"
}

# Prints the package's nuspec; fails if it cannot be read, so a broken package never passes the checks below.
read_nuspec() {
  local nuspec
  nuspec=$(unzip -p "$1" '*.nuspec') || fail "Could not read the nuspec of $(basename "$1")"
  [[ -n "$nuspec" ]] || fail "$(basename "$1") has an empty nuspec"
  printf '%s' "$nuspec"
}

check_core_dependency() {
  local name="$1" nuspec="$2" version="$3" range
  range="[$version, $(( ${version%%.*} + 1 )).0.0)"
  grep -qF "<dependency id=\"LokiBulkDataProcessor.Core\" version=\"$range\"" <<< "$nuspec" \
    || fail "$name must depend on LokiBulkDataProcessor.Core $range"
}

check_no_test_only_dependencies() {
  local name="$1" nuspec="$2"
  if grep -ioE "<dependency id=\"($test_only_packages)[^\"]*\"" <<< "$nuspec"; then
    fail "$name depends on a test-only package (listed above)"
  fi
}

check_contents() {
  local package="$1" id="$2" framework="$3" entries
  entries=$(unzip -Z1 "$package") || fail "Could not list $(basename "$package")"
  for entry in "lib/$framework/$id.dll" "lib/$framework/$id.xml" "README.md"; do
    grep -qxF "$entry" <<< "$entries" || fail "$(basename "$package") is missing $entry"
  done
}

check_package() {
  local id="$1" version="$2" framework="$3" package name nuspec
  package="$output_dir/$id.$version.nupkg"
  name=$(basename "$package")
  nuspec=$(read_nuspec "$package")
  echo "--- $id $version dependencies"
  grep -oE '<dependency [^>]*>' <<< "$nuspec" || true
  check_no_test_only_dependencies "$name" "$nuspec"
  check_contents "$package" "$id" "$framework"
  [[ "$id" == "LokiBulkDataProcessor.Core" ]] || check_core_dependency "$name" "$nuspec" "$version"
}

main() {
  local version framework id
  version=$(read_property Version '^[0-9]+\.[0-9]+\.[0-9]+$')
  framework=$(read_property TargetFramework '^net[0-9]+\.[0-9]+$')
  pack_libraries
  check_package_set "$version"
  for id in "${libraries[@]}"; do
    check_package "$id" "$version" "$framework"
  done
  echo "--- sha256"
  (cd "$output_dir" && sha256sum ./*.nupkg)
  echo "Packed and checked ${#libraries[@]} packages at $version ($framework) in $output_dir"
}

main
