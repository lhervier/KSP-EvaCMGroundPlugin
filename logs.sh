#!/bin/bash

tail -f "$KSPDIR/KSP.log" | grep -E "\[TestPlugin\]"