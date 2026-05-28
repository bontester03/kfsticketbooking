#!/usr/bin/env bash
# Simulate a simultaneous login burst against the API and report response-time stats.
#
# Why: 300 parents hit /auth/login within ~30 seconds at the event. The CPU-bound
# part is BCrypt verify; this script measures whether the chosen EC2 size handles it.
#
# Usage:
#   ./login-burst.sh <BASE_URL> [N=300] [EMAIL=admin@kfs.sch.sa] [PASSWORD=Admin@123]
#
# Tips:
#   - Run this from CloudShell or your laptop, NOT from the EC2 itself
#     (otherwise the EC2 is fighting itself for CPU).
#   - Use a known-good account; failed logins still cost BCrypt time but pollute the metrics.

set -eo pipefail

BASE="${1:-http://localhost:5080}"
N="${2:-300}"
EMAIL="${3:-admin@kfs.sch.sa}"
PASSWORD="${4:-Admin@123}"

ENDPOINT="${BASE%/}/api/v1/auth/admin/login"
[[ "$EMAIL" == *"@stu.kfs.sch.sa" ]] && ENDPOINT="${BASE%/}/api/v1/auth/login"

echo "Target:       $ENDPOINT"
echo "Email:        $EMAIL"
echo "Concurrency:  $N parallel logins"
echo "----------------------------------------"

# One login function called by each worker. Single-quoted curl format string
# preserves literal %{http_code} and %{time_total} through the xargs handoff.
curl_one() {
  curl -s -o /dev/null \
    -w '%{http_code} %{time_total}\n' \
    -X POST "$ENDPOINT" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}"
}
export -f curl_one
export ENDPOINT EMAIL PASSWORD

# Warm one request so DNS / TLS handshake doesn't skew the burst metrics.
echo -n "warm-up: "
curl_one

OUT=$(mktemp)
START=$(date +%s.%N)

# Fire N parallel curl POSTs. (Note: no -I flag because xargs would replace _ inside curl_one.)
seq 1 "$N" | xargs -n 1 -P "$N" bash -c 'curl_one' _ > "$OUT"

END=$(date +%s.%N)
WALL=$(awk "BEGIN { printf \"%.2f\", $END - $START }")

echo
echo "=== Result ==="
echo "wall-clock:   ${WALL}s for $N requests"
echo "throughput:   $(awk "BEGIN { printf \"%.1f\", $N / $WALL }") logins / sec"
echo
echo "by HTTP status:"
awk '{print $1}' "$OUT" | sort | uniq -c | sort -rn | sed 's/^/  /'

echo
echo "response times (seconds):"
awk '{print $2}' "$OUT" | sort -n | awk -v n="$N" '
  BEGIN { c=0; sum=0 }
  { times[c++]=$1; sum+=$1 }
  END {
    if (c == 0) { print "  (no samples)"; exit }
    printf "  count:  %d\n", c
    printf "  mean:   %.3fs\n", sum/c
    printf "  p50:    %.3fs\n", times[int(c*0.50)]
    printf "  p90:    %.3fs\n", times[int(c*0.90)]
    printf "  p95:    %.3fs\n", times[int(c*0.95)]
    printf "  p99:    %.3fs\n", times[int(c*0.99)]
    printf "  max:    %.3fs\n", times[c-1]
  }'

rm -f "$OUT"

echo
echo "Healthy on t3.large for 300 logins: p95 < 3s, max < 6s, all 200."
echo "If p95 > 5s, scale up:"
echo "  aws ec2 stop-instances --instance-ids <id>"
echo "  aws ec2 modify-instance-attribute --instance-id <id> --instance-type '{\"Value\":\"t3.xlarge\"}'"
echo "  aws ec2 start-instances --instance-ids <id>"
