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
# The X server of the test (see below): Xephyr (a window on the desktop of WSLg) or Xvfb (none), with openbox as window manager.
{ command -v Xephyr >/dev/null 2>&1 || command -v Xvfb >/dev/null 2>&1; } && command -v openbox >/dev/null 2>&1 || missing+=(apt)

if [ "$setup" = "1" ] || [ ${#missing[@]} -gt 0 ]; then
  say "installing: ${missing[*]:-everything (-Setup)}"
  if [ "$setup" = "1" ] || [[ " ${missing[*]} " == *" apt "* ]] || [[ " ${missing[*]} " == *" node "* ]]; then
    # sudo asks for the password of the WSL user once.
    sudo apt-get update -qq
    sudo apt-get install -y -qq xserver-xephyr xvfb openbox x11-utils at-spi2-core dbus-x11 curl ca-certificates libfontconfig1 libice6 libsm6 libx11-6 libxrandr2 \
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

# The session bus: the one of systemd (WSL starts a user session, /run/user/<uid>/bus) when there is one, so that the apps,
# agent-desktop and the AT-SPI bus all meet on the same bus; dbus-launch only when there is none. ("wsl -e" gives no
# DBUS_SESSION_BUS_ADDRESS: a new bus from dbus-launch next to the AT-SPI bus of systemd left the apps out of it.)
uid="$(id -u)"
if [ -z "${DBUS_SESSION_BUS_ADDRESS:-}" ] && [ -S "/run/user/$uid/bus" ]; then
  export DBUS_SESSION_BUS_ADDRESS="unix:path=/run/user/$uid/bus"
fi
if [ -z "${XDG_RUNTIME_DIR:-}" ] && [ -d "/run/user/$uid" ]; then export XDG_RUNTIME_DIR="/run/user/$uid"; fi
if [ -z "${DBUS_SESSION_BUS_ADDRESS:-}" ] || ! dbus-send --session --dest=org.freedesktop.DBus --type=method_call --print-reply \
     /org/freedesktop/DBus org.freedesktop.DBus.ListNames >/dev/null 2>&1; then
  eval "$(dbus-launch --sh-syntax)"
  export DBUS_SESSION_BUS_ADDRESS
fi
# The AT-SPI bus on that session bus: asked for (D-Bus starts it from its service file), else started by hand.
a11y() { dbus-send --session --dest=org.a11y.Bus --type=method_call --print-reply /org/a11y/bus org.a11y.Bus.GetAddress 2>/dev/null; }
if ! a11y >/dev/null; then
  (setsid "$launcher" --launch-immediately >/dev/null 2>&1 &)
  for _ in 1 2 3 4 5 6 7 8 9 10; do a11y >/dev/null && break; sleep 0.5; done
fi
a11y >/dev/null || fail "no AT-SPI bus on the session bus $DBUS_SESSION_BUS_ADDRESS"
# Accessibility on: Avalonia exposes its tree "when a D-Bus session bus is available and an accessibility service is running".
for property in IsEnabled ScreenReaderEnabled; do
  dbus-send --session --dest=org.a11y.Bus --type=method_call /org/a11y/bus org.freedesktop.DBus.Properties.Set \
    string:org.a11y.Status string:$property variant:boolean:true >/dev/null 2>&1 || true
done
say "session bus: $DBUS_SESSION_BUS_ADDRESS; AT-SPI: $(a11y | grep -o 'unix:[^"]*' | head -n1)"
# X11, not Wayland: WSLg is a Wayland desktop (Weston) where the apps run through Xwayland. With WAYLAND_DISPLAY set, agent-desktop
# drives Wayland, where it can neither prove the window of an Xwayland app ("native window type ... could not be proven") nor click
# (no physical input on Wayland). Empty, it counts as unset: agent-desktop and the apps use the X display of WSLg (DISPLAY=:0),
# with XTEST clicks and EWMH window control.
export WAYLAND_DISPLAY= XDG_SESSION_TYPE=x11
[ -n "${DISPLAY:-}" ] || fail "No X display (DISPLAY): WSLg's Xwayland is missing. From Windows: wsl --update, then wsl --shutdown."
# Not the X display of WSLg itself: its window manager (Weston's) keeps no _NET_CLIENT_LIST, so agent-desktop cannot tie an
# accessibility frame to its X window ("the native window type ... could not be proven"). The test gets an X server of its own
# with a full EWMH window manager (openbox): Xephyr, shown as one window on the desktop of WSLg, or Xvfb when ROUTEABLES_X=xvfb
# (nothing shown, as in CI). ROUTEABLES_X=wslg keeps the display of WSLg.
x_mode="${ROUTEABLES_X:-xephyr}"
x_pids=()
cleanup_x() { for p in "${x_pids[@]}"; do kill "$p" 2>/dev/null || true; done; }
trap cleanup_x EXIT
if [ "$x_mode" != "wslg" ]; then
  n=99; while [ -e "/tmp/.X11-unix/X$n" ] || [ -e "/tmp/.X$n-lock" ]; do n=$((n + 1)); done
  x_log="$artifacts/xserver.txt"; mkdir -p "$artifacts"
  # WSLg mounts /tmp/.X11-unix read-only (it holds its own X0): when the socket file cannot be made there, the server listens on the
  # abstract socket only (-nolisten unix), which X clients on Linux try first.
  listen=()
  if ! [ -w /tmp/.X11-unix ] || [ -L /tmp/.X11-unix ] || grep -q ' /tmp/.X11-unix ' /proc/mounts; then listen=(-nolisten unix); fi
  if [ "$x_mode" = "xephyr" ] && command -v Xephyr >/dev/null 2>&1; then
    Xephyr ":$n" -screen 1600x1000 -ac -br -noreset -nolisten tcp "${listen[@]}" -title "Routeables smoke (:$n)" >"$x_log" 2>&1 &
    x_server=Xephyr
  else
    Xvfb ":$n" -screen 0 1600x1000x24 -ac -noreset -nolisten tcp "${listen[@]}" >"$x_log" 2>&1 &
    x_server=Xvfb
  fi
  x_pids+=($!)
  ready=0
  for _ in $(seq 1 60); do
    # Listening: its socket in /proc/net/unix (the file one, or the abstract one written with a leading @).
    if grep -q "/tmp/.X11-unix/X$n\$" /proc/net/unix 2>/dev/null; then ready=1; break; fi
    kill -0 "${x_pids[-1]}" 2>/dev/null || break
    sleep 0.2
  done
  if [ "$ready" != "1" ]; then
    tail -n 15 "$x_log" >&2
    fail "the X server :$n ($x_server) did not start (log: $x_log)"
  fi
  export DISPLAY=":$n"
  openbox >/dev/null 2>&1 &
  x_pids+=($!)
  sleep 1
  say "X display $DISPLAY ($x_server + openbox)"
fi
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
      if grep -q 'NETSDK1147' "$log"; then
        # A library of the repository also targets net10.0-android (Routeables.Native on Linux): restoring it needs the android
        # workload, even to build the desktop app. A distribution's .NET (apt, /usr/lib/dotnet) cannot install workloads: the
        # Microsoft one goes to ~/.dotnet (no sudo), and the workload into it.
        if [ ! -x "$HOME/.dotnet/dotnet" ] || ! "$HOME/.dotnet/dotnet" --list-sdks | grep -q '^10\.'; then
          say "the android workload is needed: installing the .NET 10 SDK of Microsoft into ~/.dotnet"
          curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
          bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet" >/dev/null
        fi
        say "dotnet workload install android ..."
        "$HOME/.dotnet/dotnet" workload install android >"$artifacts/workload.txt" 2>&1 || { tail -n 5 "$artifacts/workload.txt" >&2; fail "the android workload could not be installed (see $artifacts/workload.txt)"; }
        export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
        hash -r
        # shellcheck disable=SC2086
        dotnet build "$repo/${project[$h]}" -c Debug --artifacts-path "$artifacts" ${extra[$h]} -nologo >"$log" 2>&1 && built=1 || built=0
      elif grep -q 'Uno.Sdk.Services.PackageManifest' "$log"; then
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

# -Diagnose: what the accessibility bus sees of each app, without the test: started alone for 15 s, then its connection on the
# AT-SPI bus, the applications the registry lists, and what agent-desktop lists. Written to out/native-smoke-wsl/diagnose.txt.
if [ "${ROUTEABLES_DIAGNOSE:-0}" = "1" ]; then
  # A diagnosis goes on past what it does not find.
  set +e +o pipefail
  out="$repo/out/native-smoke-wsl"; mkdir -p "$out"; report="$out/diagnose.txt"
  a11y_address="$(a11y | grep -o 'unix:[^"]*' | head -n1)"
  {
    echo "== $(date -Is)  $(uname -a)"
    echo "DISPLAY=${DISPLAY:-} WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-} XDG_SESSION_TYPE=${XDG_SESSION_TYPE:-}"
    echo "DBUS_SESSION_BUS_ADDRESS=$DBUS_SESSION_BUS_ADDRESS"
    echo "AT-SPI bus: $a11y_address"
    for property in IsEnabled ScreenReaderEnabled; do
      printf '%s: ' "$property"
      dbus-send --session --dest=org.a11y.Bus --print-reply /org/a11y/bus org.freedesktop.DBus.Properties.Get \
        string:org.a11y.Status string:$property 2>&1 | grep -o 'boolean [a-z]*' || echo '?'
    done
    echo "launcher: $(pgrep -a -f at-spi-bus-launcher | head -n3)"
    echo "registryd: $(pgrep -a -f at-spi2-registryd | head -n3)"
  } >"$report" 2>&1
  for entry in "${exe_list[@]}"; do
    h="${entry%%=*}"; exe="${entry#*=}"
    echo "" >>"$report"; echo "== $h: $exe" >>"$report"
    "$exe" >"$out/diagnose-$h-stdout.txt" 2>&1 &
    app_pid=$!
    sleep 15
    {
      echo "pid $app_pid, alive: $(kill -0 $app_pid 2>/dev/null && echo yes || echo no)"
      echo "-- connections of the AT-SPI bus (unique name -> pid):"
      for name in $(dbus-send --bus="$a11y_address" --dest=org.freedesktop.DBus --print-reply /org/freedesktop/DBus \
                    org.freedesktop.DBus.ListNames 2>/dev/null | grep -o '":[0-9.]*"' | tr -d '"'); do
        pid="$(dbus-send --bus="$a11y_address" --dest=org.freedesktop.DBus --print-reply /org/freedesktop/DBus \
               org.freedesktop.DBus.GetConnectionUnixProcessID string:$name 2>/dev/null | grep -o 'uint32 [0-9]*' | cut -d' ' -f2)"
        echo "  $name -> $pid $( [ "$pid" = "$app_pid" ] && echo '<== the app' ) $(tr '\0' ' ' < /proc/$pid/cmdline 2>/dev/null | head -c 80)"
      done
      echo "-- children of the registry (the applications on the desktop):"
      dbus-send --bus="$a11y_address" --dest=org.a11y.atspi.Registry --print-reply /org/a11y/atspi/accessible/root \
        org.a11y.atspi.Accessible.GetChildren 2>&1 | head -n 40
      echo "-- agent-desktop list-apps:"
      "$ad_bin" list-apps 2>&1 | head -c 3000
      echo ""
    } >>"$report" 2>&1
    kill "$app_pid" 2>/dev/null || true
    sleep 1
    echo "-- the app wrote:" >>"$report"; tail -n 30 "$out/diagnose-$h-stdout.txt" >>"$report" 2>/dev/null || true
  done
  say "diagnosis: $report"
  cat "$report"
  exit 0
fi
exe_arg="$(IFS=','; echo "${exe_list[*]}")"
cd "$repo"
pwsh -NoProfile -File "$repo/agent-desktop/run.ps1" -Hosts "$hosts" -Exe "$exe_arg" -AgentDesktop "$ad_bin" -OutDir "$repo/out/native-smoke-wsl" "$@"
code=$?
exit $code
