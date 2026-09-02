#!/bin/bash
set -e

echo ""
echo "-------------------------------------------"
echo "Updating the KSP-Shared submodule"
echo "-------------------------------------------"

# Fetch the latest commits from the submodule's tracked branch (main),
# instead of staying on the SHA pinned by the parent repository.
git submodule update --remote --merge KSP-Shared

echo ""
echo "KSP-Shared submodule updated successfully"
echo ""
echo "If the library changed, remember to commit the new pointer:"
echo "  git add KSP-Shared && git commit -m \"Bump KSP-Shared\""
