#!/usr/bin/env bash
# Packs the three published libraries in Release and checks the packages before anyone can publish them.
# Used by the CI `pack` job; run it locally from the repository root with `bash build/pack.sh [output-dir]`.
# It never pushes anything to NuGet.
#
# Checks:
#   - exactly three .nupkg files, LokiBulkDataProcessor, .Core and .Oracle, at the <Version> in Directory.Build.props;
#   - the SQL Server and Oracle packages depend on LokiBulkDataProcessor.Core [Version, next major);
#   - no package depends on a test-only package (Testcontainers, NUnit, Moq, FluentAssertions, Microsoft.NET.Test.Sdk, coverlet);
#   - every package contains its assembly, its XML documentation and README.md.
set -euo pipefail

output_dir="${1:-artifacts/packages}"
libraries=(LokiBulkDataProcessor.Core LokiBulkDataProcessor LokiBulkDataProcessor.Oracle)
test_only_packages='testcontainers|nunit|moq|fluentassertions|microsoft\.net\.test\.sdk|coverlet'

fail() {
  echo "::error::$1"
  exit 1
}

read_version() {
  local version
  version=$(grep -oP '<Version>\K[^<]+' Directory.Build.props)
  [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "Could not read a plain <Version> from Directory.Build.props (found '$version')"
  echo "$version"
}

# GeneratePackageOnBuild is on for the libraries; `dotnet pack` with it on fails with NU5026, so it is turned off here.
pack_libraries() {
  rm -rf "$output_dir"
  for library in "${libraries[@]}"; do
    dotnet pack "$library" -c Release -p:GeneratePackageOnBuild=false -o "$output_dir"
  done
}

check_package_set() {
  local version="$1" expected actual
  expected=$(printf '%s\n' "${libraries[@]/%/.$version.nupkg}" | sort)
  actual=$(find "$output_dir" -maxdepth 1 -name '*.nupkg' -printf '%f\n' | sort)
  [[ "$actual" == "$expected" ]] || fail "Expected exactly these packages:"$'\n'"$expected"$'\n'"but found:"$'\n'"$actual"
}

nuspec_of() {
  unzip -p "$1" '*.nuspec'
}

check_core_dependency() {
  local package="$1" version="$2" range
  range="[$version, $(( ${version%%.*} + 1 )).0.0)"
  nuspec_of "$package" | grep -qF "<dependency id=\"LokiBulkDataProcessor.Core\" version=\"$range\"" \
    || fail "$(basename "$package") must depend on LokiBulkDataProcessor.Core $range"
}

check_no_test_only_dependencies() {
  local package="$1"
  if nuspec_of "$package" | grep -ioE "<dependency id=\"($test_only_packages)[^\"]*\""; then
    fail "$(basename "$package") depends on a test-only package (listed above)"
  fi
}

check_contents() {
  local package="$1" id="$2" entries
  entries=$(unzip -Z1 "$package")
  for entry in "lib/net10.0/$id.dll" "lib/net10.0/$id.xml" "README.md"; do
    grep -qxF "$entry" <<< "$entries" || fail "$(basename "$package") is missing $entry"
  done
}

main() {
  local version package id
  version=$(read_version)
  pack_libraries
  check_package_set "$version"
  for id in "${libraries[@]}"; do
    package="$output_dir/$id.$version.nupkg"
    echo "--- $id $version dependencies"
    nuspec_of "$package" | grep -oE '<dependency [^>]*>' || true
    check_no_test_only_dependencies "$package"
    check_contents "$package" "$id"
    [[ "$id" == "LokiBulkDataProcessor.Core" ]] || check_core_dependency "$package" "$version"
  done
  echo "Packed and checked ${#libraries[@]} packages at $version in $output_dir"
}

main
