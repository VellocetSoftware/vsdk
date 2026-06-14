#!/usr/bin/env bash
set -euo pipefail

fail() {
  echo "$1" >&2
  exit 1
}

TEMP_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$TEMP_DIR"
}
trap cleanup EXIT

if [[ -d .teamcity ]]; then
  cd .teamcity
fi

patch_files="$(find patches -type f -print 2>/dev/null || true)"
if [[ -n "$patch_files" ]]; then
  echo "Found TeamCity generated settings patches. Merge them into settings.kts before applying versioned settings:" >&2
  echo "$patch_files" >&2
  exit 1
fi

mvn clean verify

script_files=()
while IFS= read -r -d '' script_file; do
  script_files+=("$script_file")
done < <(find scripts -type f -name "*.sh" -print0)

repo_root="$(cd .. && pwd)"
launcher_script="$repo_root/VSDK/scripts/build-steam-tool.sh"
[[ -f "$launcher_script" ]] || fail "Missing VSDK launcher build script: $launcher_script"
script_files+=("$launcher_script")

for script_file in "${script_files[@]}"; do
  bash -n "$script_file"
done

if command -v shellcheck >/dev/null 2>&1; then
  shellcheck "${script_files[@]}"
else
  echo "shellcheck is not installed on this agent; bash -n syntax validation completed."
fi

launcher_config="$(find target/generated-configs -path "*buildTypes/*_BuildLauncher.xml" -print -quit)"
[[ -n "$launcher_config" ]] || fail "Could not find generated Build Launcher config."
[[ "$(basename "$launcher_config")" == *_BuildLauncher.xml ]] || fail "Build Launcher relative ID must remain BuildLauncher; Grimwar depends on VSDK_BuildLauncher."
grep -q '<name>Build Launcher</name>' "$launcher_config" || fail "Build Launcher config has the wrong display name."
grep -q 'name="checkoutMode" value="AUTO"' "$launcher_config" || fail "Build Launcher must prefer agent-side checkout via TeamCity AUTO checkout mode."
grep -Fq 'name="artifactRules" value="VSDK/Build/Launcher/** =&gt; vsdk-launcher.zip"' "$launcher_config" || fail "Build Launcher artifact rule must keep publishing vsdk-launcher.zip."
grep -q 'id="Build_Steam_Tool_Launcher"' "$launcher_config" || fail "Build Launcher must keep the Steam tool launcher build step."
grep -q 'bash VSDK/scripts/build-steam-tool.sh' "$launcher_config" || fail "Build Launcher must call VSDK/scripts/build-steam-tool.sh."
grep -q 'name="teamcity.agent.jvm.os.family" value="Linux|Mac OS"' "$launcher_config" || fail "Build Launcher must run only on Linux/macOS agents."
grep -q 'source.revision' "$launcher_config" || fail "Build Launcher must publish source provenance output parameters."

dsl_config="$(find target/generated-configs -path "*buildTypes/*_Workflows_DslValidate.xml" -print -quit)"
[[ -n "$dsl_config" ]] || fail "Could not find generated TeamCity DSL Validate config."
grep -q '<name>TeamCity DSL Validate</name>' "$dsl_config" || fail "DSL Validate config has the wrong display name."
grep -q 'id="Generate_TeamCity_Configs"' "$dsl_config" || fail "DSL Validate must generate TeamCity configs."
grep -q 'name="checkoutMode" value="AUTO"' "$dsl_config" || fail "DSL Validate must prefer agent-side checkout via TeamCity AUTO checkout mode."
grep -q 'name="teamcity.agent.jvm.os.family" value="Linux"' "$dsl_config" || fail "DSL Validate must run on Linux automation agents."

if grep -R --exclude="*Workflows_DslValidate.xml" 'name="cleanBuild" value="true"' target/generated-configs >"$TEMP_DIR/clean-build.txt"; then
  cat "$TEMP_DIR/clean-build.txt" >&2
  fail "Do not force clean checkout on every build; TeamCity already cleans when needed."
fi

if grep -R --exclude="*Workflows_DslValidate.xml" -E 'name="checkoutMode" value="ON_(SERVER|AGENT)"' target/generated-configs >"$TEMP_DIR/forced-checkout-mode.txt"; then
  cat "$TEMP_DIR/forced-checkout-mode.txt" >&2
  fail "Generated VSDK settings must use TeamCity AUTO checkout mode instead of forcing server-side or agent-side checkout."
fi
