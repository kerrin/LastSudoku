Param()
if (-not (Test-Path "local.instructions.md")) {
  Write-Host "No local.instructions.md found in repo root."
  exit 1
}
Write-Host "To refresh the assistant's repo memory, open the chat and ask:"
Write-Host "Please refresh local instructions from local.instructions.md"
