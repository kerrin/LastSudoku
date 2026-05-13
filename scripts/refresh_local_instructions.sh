#!/bin/sh
# Helper: remind how to refresh assistant repo memory for local instructions
if [ ! -f local.instructions.md ]; then
  echo "No local.instructions.md found in repo root."
  exit 1
fi
echo "To refresh the assistant's repo memory, open the chat and ask:"
echo "Please refresh local instructions from local.instructions.md"
