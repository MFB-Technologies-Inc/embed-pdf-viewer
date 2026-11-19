#!/bin/sh
# Run one command with this machine's GitHub Actions runner stopped, so no
# job starts next to a timed run.
#
#   sh scripts/remote-guard.sh <exit-file> '<command>'
#
# `scripts/remote.mjs` starts every remote run through this. In order:
#   1. If the runner is installed as a service (`<runner dir>/.service`
#      names its LaunchAgent plist) and loaded, wait for the job it is
#      running to end, then stop the service. A job is never cut short.
#   2. Refuse to run if a runner process is still alive after that.
#   3. Run the command.
#   4. Start the service again if step 1 stopped it, whatever the command's
#      exit status, and also when this script is told to stop.
#   5. Write the exit status to <exit-file>.
# A machine with no runner goes straight to step 3.
#
#   PERF_RUNNER_DIR   the runner's directory (default ~/actions-runner)
#   PERF_RUNNER_WAIT  seconds to wait for a running job (default 3600)
#   PERF_RUNNER_POLL  seconds between looks at it (default 15)
#
# Exit status: the command's, or 75 when a job outlasted the wait, or 70
# when a runner could not be stopped.
#
# The service is stopped and started with `launchctl bootout` and
# `bootstrap` on the `gui/<uid>` domain, not with the runner's `svc.sh`:
# that one uses `launchctl load`, which from an SSH session targets the
# session's own domain and not the one the agent lives in.
exit_file=$1
command=$2
dir=${PERF_RUNNER_DIR:-$HOME/actions-runner}
wait_limit=${PERF_RUNNER_WAIT:-3600}
poll=${PERF_RUNNER_POLL:-15}
domain="gui/$(id -u)"
plist=
label=
stopped=0

busy() { pgrep -f 'Runner\.Worker' >/dev/null 2>&1; }
alive() { pgrep -f 'Runner\.(Listener|Worker)' >/dev/null 2>&1; }

restart() {
  [ "$stopped" = 1 ] || return 0
  stopped=0
  echo "runner: starting $label"
  launchctl bootstrap "$domain" "$plist" ||
    echo "runner: could not start $label; run \`launchctl bootstrap $domain $plist\` on this machine" >&2
}
# The service is back before the status is written: whoever waits on the
# exit file can then check the runner.
finish() {
  restart
  echo "$1" > "$exit_file"
  exit "$1"
}
trap restart EXIT
trap 'finish 143' INT TERM HUP

if [ -f "$dir/.service" ]; then
  plist=$(cat "$dir/.service")
  label=$(basename "$plist" .plist)
  # A service someone already stopped stays stopped afterwards.
  if launchctl print "$domain/$label" >/dev/null 2>&1; then
    waited=0
    while busy; do
      if [ "$waited" -ge "$wait_limit" ]; then
        echo "runner: a job is still running after ${waited}s; the timed run was not started" >&2
        finish 75
      fi
      [ "$waited" = 0 ] && echo "runner: waiting for the running job to end (up to ${wait_limit}s)"
      sleep "$poll"
      waited=$((waited + poll))
    done
    echo "runner: stopping $label"
    if ! launchctl bootout "$domain/$label"; then
      echo "runner: could not stop $label; the timed run was not started" >&2
      finish 70
    fi
    stopped=1
    tries=0
    while alive && [ "$tries" -lt 30 ]; do
      sleep 1
      tries=$((tries + 1))
    done
  fi
fi
if alive; then
  echo "runner: a GitHub Actions runner is still running on this machine; the timed run was not started" >&2
  finish 70
fi

sh -c "$command"
finish $?
