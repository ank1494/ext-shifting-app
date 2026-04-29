#!/bin/bash
INPUT=$(cat)
TOOL=$(echo "$INPUT" | sed -n 's/.*"tool_name":"\([^"]*\)".*/\1/p')
[ -z "$TOOL" ] && exit 0

APP_ROOT="C:/dev/ext-shifting-app"
SUB_ROOT="C:/dev/ext-shifting-app/m2/ext-shifting"

case "$TOOL" in
  Bash|PowerShell)
    if echo "$INPUT" | grep -qE 'git\b.*\bcommit\b'; then
      APP_BRANCH=$(git -C "$APP_ROOT" branch --show-current 2>/dev/null)
      SUB_BRANCH=$(git -C "$SUB_ROOT" branch --show-current 2>/dev/null)
      if [[ "$APP_BRANCH" == "main" || "$SUB_BRANCH" == "main" ]]; then
        echo "Blocked: cannot commit to main. Switch to a feature branch first, then commit."
        exit 2
      fi
    fi
    ;;
esac

exit 0
