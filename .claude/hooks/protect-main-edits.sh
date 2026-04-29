#!/bin/bash
INPUT=$(cat)
TOOL=$(echo "$INPUT" | sed -n 's/.*"tool_name":"\([^"]*\)".*/\1/p')
[ -z "$TOOL" ] && exit 0

APP_ROOT="C:/dev/ext-shifting-app"
SUB_ROOT="C:/dev/ext-shifting-app/m2/ext-shifting"

branch_for_path() {
  local path="$1"
  if [[ "$path" == *m2/ext-shifting* || "$path" == *m2\\ext-shifting* ]]; then
    git -C "$SUB_ROOT" branch --show-current 2>/dev/null
  else
    git -C "$APP_ROOT" branch --show-current 2>/dev/null
  fi
}

case "$TOOL" in
  Edit|Write)
    FILEPATH=$(echo "$INPUT" | sed -n 's/.*"file_path":"\([^"]*\)".*/\1/p')
    if [[ -n "$FILEPATH" && "$FILEPATH" != *.md ]]; then
      BRANCH=$(branch_for_path "$FILEPATH")
      if [[ "$BRANCH" == "main" ]]; then
        echo "Blocked: cannot edit non-.md files on main. Create or switch to a feature branch first, then retry the edit."
        exit 2
      fi
    fi
    ;;
  Bash|PowerShell)
    if echo "$INPUT" | grep -qE 'sed\s+-i|patch\s|>\s*\S+\.(cs|m2|csproj|sh|ps1|yaml|yml|toml|xml|fs|fsproj)|tee\s+\S+\.(cs|m2|csproj|sh|ps1|yaml|yml|toml|xml|fs|fsproj)'; then
      APP_BRANCH=$(git -C "$APP_ROOT" branch --show-current 2>/dev/null)
      SUB_BRANCH=$(git -C "$SUB_ROOT" branch --show-current 2>/dev/null)
      if [[ "$APP_BRANCH" == "main" || "$SUB_BRANCH" == "main" ]]; then
        echo "Blocked: detected file-write command targeting non-.md files on main. Create or switch to a feature branch first, then retry."
        exit 2
      fi
    fi
    ;;
esac

exit 0
