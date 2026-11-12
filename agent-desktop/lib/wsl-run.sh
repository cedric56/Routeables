#!/usr/bin/env bash
# Runs the native smoke test of the Linux hosts (Avalonia, Uno) inside WSL. Called by run-wsl.ps1 from Windows:
#   wsl-run.sh <repo, as a Linux path> <hosts: Avalonia,Uno> <setup: 0|1> <build: 0|1> [arguments for run.ps1 ...]
# Installs what is missing (the .NET 10 SDK, PowerShell, Node and agent-desktop, the AT-SPI bus, the libraries of Avalonia and
# Uno), builds the samples for Linux into a folder of WSL (not into the bin/obj of Windows), starts the accessibility bus, and
# runs agent-desktop/run.ps1 there. The windows show through WSLg.
set -euo pipefail

repo="$1"; hosts="$2"; setup="$3"; build="$4"; shift 4

export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$HOME/.local/bin:$HOME/.npm-global/bin:$PATH"
# Only the Linux side: WSL appends the PATH of Windows, whose npm folder holds an agent-desktop script for Windows.
linux_command() { local p; p="$(command -v "$1" 2>/dev/null || true)"; [ -n "$p" ] && [[ "$p" != /mnt/* ]] && echo "$p"; }
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
artifacts="$HOME/.cache/routeables-wsl"

say() { printf '\033[36m[wsl] %s\033[0m\n' "$*"; }
fail() { printf '\033[31m[wsl] %s\033[0m\n' "$*" >&2; exit 2; }

# ---------------------------------------------------------------------------------------------------------------------------
# Prerequisites
# ---------------------------------------------------------------------------------------------------------------------------

[ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ] || fail "No display: WSLg is missing. From Windows: wsl --update, then wsl --shutdown."

missing=()
command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.' || missing+=(dotnet)
command -v pwsh >/dev/null 2>&1 || missing+=(pwsh)
command -v node >/dev/null 2>&1 && [ "$(node -p 'process.versions.node.split(".")[0]' 2>/dev/null || echo 0)" -ge 18 ] || missing+=(node)
[ -n "$(linux_command agent-desktop)" ] || missing+=(agent-desktop)
launcher=""
for candidate in /usr/libexec/at-spi-bus-launcher /usr/lib/at-spi2-core/at-spi-bus-launcher; do
  [ -x "$candidate" ] && launcher="$candidate" && break
done
[ -n "$launcher" ] && command -v dbus-launch >/dev/null 2>&1 || missing+=(apt)

if [ "$setup" = "1" ] || [ ${#missing[@]} -gt 0 ]; then
  say "installing: ${missing[*]:-everything (-Setup)}"
  if [ "$setup" = "1" ] || [[ " ${missing[*]} " == *" apt "* ]] || [[ " ${missing[*]} " == *" node "* ]]; then
    # sudo asks for the password of the WSL user once.
    sudo apt-get update -qq
    sudo apt-get install -y -qq at-spi2-core dbus-x11 curl ca-certificates libfontconfig1 libice6 libsm6 libx11-6 libxrandr2 \
      libxi6 libxcursor1 libxext6 libgl1 libegl1 libicu-dev >/dev/null
  fi
  if [[ " ${missing[*]} " == *" node "* ]]; then
    curl -fsSL https://deb.nodesource.com/setup_22.x | sudo bash - >/dev/null 2>&1
    sudo apt-get install -y -qq nodejs >/dev/null
  fi
  if [[ " ${missing[*]} " == *" dotnet "* ]]; then
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet" >/dev/null
  fi
  if ! command -v pwsh >/dev/null 2>&1; then
    # The release archive of PowerShell, in ~/.powershell (the dotnet tool asks for a version that NuGet does not always have).
    case "$(uname -m)" in aarch64|arm64) arch=arm64;; *) arch=x64;; esac
    url="$(curl -fsSL https://api.github.com/repos/PowerShell/PowerShell/releases/latest 2>/dev/null \
      | grep -o "https://[^\"]*powershell-[0-9.]*-linux-$arch\.tar\.gz" | head -n1 || true)"
    [ -n "$url" ] || url="https://github.com/PowerShell/PowerShell/releases/download/v7.5.4/powershell-7.5.4-linux-$arch.tar.gz"
    say "PowerShell: $url"
    rm -rf "$HOME/.powershell" && mkdir -p "$HOME/.powershell"
    curl -fsSL "$url" | tar -xz -C "$HOME/.powershell"
    chmod +x "$HOME/.powershell/pwsh"
    mkdir -p "$HOME/.local/bin" && ln -sf "$HOME/.powershell/pwsh" "$HOME/.local/bin/pwsh"
  fi
  if [ -z "$(linux_command agent-desktop)" ] || [ "$setup" = "1" ]; then
    npm config set prefix "$HOME/.npm-global"
    npm install -g agent-desktop >/dev/null
  fi
  for candidate in /usr/libexec/at-spi-bus-launcher /usr/lib/at-spi2-core/at-spi-bus-launcher; do
    [ -x "$candidate" ] && launcher="$candidate" && break
  done
fi
[ -n "$launcher" ] || fail "at-spi-bus-launcher not found (sudo apt install at-spi2-core)"

# The .NET that runs the apps: the one dotnet comes from (apt puts it in /usr/lib/dotnet, dotnet-install in ~/.dotnet). An
# apphost looks in DOTNET_ROOT: a wrong one gives "You must install .NET to run this application".
dotnet_path="$(readlink -f "$(command -v dotnet)")"
export DOTNET_ROOT="$(dirname "$dotnet_path")"
# The native binary of agent-desktop (the npm launcher would need node at each call).
ad_root="$(npm root -g 2>/dev/null || true)"
case "$(uname -m)" in aarch64|arm64) ad_arch=arm64;; *) ad_arch=x64;; esac
ad_bin="$ad_root/agent-desktop/bin/agent-desktop-linux-$ad_arch"
[ -x "$ad_bin" ] || ad_bin="$(linux_command agent-desktop)"
[ -n "$ad_bin" ] || fail "agent-desktop is not installed on the Linux side (npm install -g agent-desktop)"

# ---------------------------------------------------------------------------------------------------------------------------
# Accessibility bus: a session bus, the AT-SPI bus on it, accessibility switched on (the toolkits only expose their tree then)
# ---------------------------------------------------------------------------------------------------------------------------

if [ -z "${DBUS_SESSION_BUS_ADDRESS:-}" ] || ! dbus-send --session --dest=org.freedesktop.DBus --type=method_call --print-reply \
     /org/freedesktop/DBus org.freedesktop.DBus.ListNames >/dev/null 2>&1; then
  eval "$(dbus-launch --sh-syntax)"
  export DBUS_SESSION_BUS_ADDRESS
fi
pgrep -u "$(id -u)" -f at-spi-bus-launcher >/dev/null || (setsid "$launcher" --launch-immediately >/dev/null 2>&1 &)
sleep 1
dbus-send --session --dest=org.a11y.Bus --type=method_call /org/a11y/bus org.freedesktop.DBus.Properties.Set \
  string:org.a11y.Status string:IsEnabled variant:boolean:true >/dev/null 2>&1 || true
export GTK_MODULES=gail:atk-bridge NO_AT_BRIDGE=0 ACCESSIBILITY_ENABLED=1

# ---------------------------------------------------------------------------------------------------------------------------
# Build for Linux, into a folder of WSL: the bin and obj of the repository stay those of Windows
# ---------------------------------------------------------------------------------------------------------------------------

declare -A project=( [Avalonia]="samples/XAnimals.Avalonia.Desktop/XAnimals.Avalonia.Desktop.csproj" [Uno]="samples/XAnimals.Uno/XAnimals.Uno.csproj" )
declare -A app=( [Avalonia]="XAnimals.Avalonia.Desktop" [Uno]="XAnimals.Uno" )
declare -A extra=( [Avalonia]="" [Uno]="-p:CiBuild=true" )

exe_list=()
IFS=',' read -ra wanted <<< "$hosts"
for h in "${wanted[@]}"; do
  [ -n "${project[$h]:-}" ] || fail "Host '$h' does not run on Linux (Linux hosts: Avalonia, Uno)"
  [ "$h" = "Uno" ] && say "Uno: its Skia renderer does not expose an accessibility tree on Linux yet (AT-SPI is 'planned' by Uno): agent-desktop will not find it"
  exe="$(find "$artifacts/bin/${app[$h]}" -type f -name "${app[$h]}" -perm -u+x 2>/dev/null | head -n1 || true)"
  if [ "$build" = "1" ] || [ -z "$exe" ]; then
    say "build $h for Linux ..."
    log="$artifacts/build-$h.txt"
    mkdir -p "$artifacts"
    # shellcheck disable=SC2086
    if ! dotnet build "$repo/${project[$h]}" -c Debug --artifacts-path "$artifacts" ${extra[$h]} -nologo >"$log" 2>&1; then
      if grep -q 'Uno.Sdk.Services.PackageManifest' "$log"; then
        # A broken copy of the Uno.Sdk package in the NuGet cache of WSL (its packages.json cannot be read): the fix that Uno
        # documents is to delete it, so that the restore takes it again.
        say "the Uno.Sdk package of the NuGet cache is broken: deleted, built again"
        rm -rf "${NUGET_PACKAGES:-$HOME/.nuget/packages}/uno.sdk"
        dotnet build-server shutdown >/dev/null 2>&1 || true
        # shellcheck disable=SC2086
        dotnet build "$repo/${project[$h]}" -c Debug --artifacts-path "$artifacts" ${extra[$h]} -nologo >"$log" 2>&1 && built=1 || built=0
      else
        built=0
      fi
      if [ "$built" != "1" ]; then
        grep -m3 ': error ' "$log" >&2 || tail -n 20 "$log" >&2
        grep -m1 -A12 'Unhandled exception\|InnerException\|---> ' "$log" >&2 || true
        fail "the build of $h failed (see $log)"
      fi
    fi
    exe="$(find "$artifacts/bin/${app[$h]}" -type f -name "${app[$h]}" -perm -u+x 2>/dev/null | head -n1 || true)"
  fi
  [ -n "$exe" ] || fail "no ${app[$h]} executable under $artifacts/bin/${app[$h]}"
  exe_list+=("$h=$exe")
done

# ---------------------------------------------------------------------------------------------------------------------------
# Run
# ---------------------------------------------------------------------------------------------------------------------------

say "agent-desktop: $ad_bin; .NET: $DOTNET_ROOT"
exe_arg="$(IFS=','; echo "${exe_list[*]}")"
cd "$repo"
exec pwsh -NoProfile -File "$repo/agent-desktop/run.ps1" -Hosts "$hosts" -Exe "$exe_arg" -AgentDesktop "$ad_bin" -OutDir "$repo/out/native-smoke-wsl" "$@"
