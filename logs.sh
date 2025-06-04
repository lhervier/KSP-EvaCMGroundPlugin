#!/bin/bash

# Get logging level as first argument, defaulting to all
LOG_LEVEL=${1:-all}

# Filter logs based on level
if [ "$LOG_LEVEL" != "all" ]; then
    tail -f "$KSPDIR/KSP.log" | grep -E "\[TestPlugin\]" | grep -E "\[$LOG_LEVEL\]"
else
    tail -f "$KSPDIR/KSP.log" | grep -E "\[TestPlugin\]"
fi
