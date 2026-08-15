#!/bin/bash
# ──────────────────────────────────────────────────────────────
# Thermalview CUPS Backend
# ──────────────────────────────────────────────────────────────
# This script is registered as a CUPS backend. When CUPS sends
# a print job to a Thermalview virtual printer, this script
# receives the raw print data (ESC/POS) and forwards it to
# the Thermalview server via HTTP POST.
#
# CUPS backend protocol:
#   When called with no arguments → list available devices
#   When called with 5-6 arguments → process a print job
#     $1 = job-id
#     $2 = user
#     $3 = title
#     $4 = copies
#     $5 = options
#     $6 = filename (optional, if not provided, read from stdin)
#
# Exit codes:
#   0 = success
#   1 = failure
# ──────────────────────────────────────────────────────────────

THERMALVIEW_PORT="${THERMALVIEW_PORT:-5000}"
THERMALVIEW_HOST="${THERMALVIEW_HOST:-localhost}"
THERMALVIEW_URL="http://${THERMALVIEW_HOST}:${THERMALVIEW_PORT}/api/print"

# ── Device discovery mode ──
# When called with no arguments, CUPS expects the backend to list
# the devices it supports. Format:
#   type url "make and model" "device info"
if [ $# -eq 0 ]; then
    echo "direct thermalview:/ \"Thermalview\" \"Virtual Thermal Printer\""
    exit 0
fi

# ── Print job mode ──
JOB_ID="$1"
USER="$2"
TITLE="$3"
COPIES="$4"
OPTIONS="$5"
FILENAME="$6"

# Log to CUPS error log
echo "INFO: Thermalview backend received job $JOB_ID from $USER: $TITLE" >&2

# Read print data from file or stdin
if [ -n "$FILENAME" ]; then
    PRINT_DATA=$(cat "$FILENAME")
else
    PRINT_DATA=$(cat)
fi

# Forward raw data to Thermalview server
if [ -n "$FILENAME" ]; then
    HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
        --max-time 10 \
        --data-binary "@$FILENAME" \
        -H "Content-Type: application/octet-stream" \
        -H "X-Thermalview-Printer: $PRINTER" \
        -H "X-Thermalview-Job: $JOB_ID" \
        -H "X-Thermalview-User: $USER" \
        -H "X-Thermalview-Title: $TITLE" \
        "$THERMALVIEW_URL" 2>/dev/null)
else
    HTTP_STATUS=$(echo "$PRINT_DATA" | curl -s -o /dev/null -w "%{http_code}" \
        --max-time 10 \
        --data-binary @- \
        -H "Content-Type: application/octet-stream" \
        -H "X-Thermalview-Printer: $PRINTER" \
        -H "X-Thermalview-Job: $JOB_ID" \
        -H "X-Thermalview-User: $USER" \
        -H "X-Thermalview-Title: $TITLE" \
        "$THERMALVIEW_URL" 2>/dev/null)
fi

# Check result
if [ "$HTTP_STATUS" = "200" ]; then
    echo "INFO: Thermalview backend successfully forwarded job $JOB_ID" >&2
    exit 0
else
    echo "ERROR: Thermalview backend failed to forward job $JOB_ID (HTTP $HTTP_STATUS)" >&2
    echo "ERROR: Is Thermalview server running? (thermalview start <name>)" >&2
    exit 1
fi
