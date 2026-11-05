Set-Location C:/Users/shash/Documents/MySimGodot
git worktree add C:/Users/shash/MySimGodot-wt-track -b mistral/track-render 2>&1 | Select-Object -Last 1
git worktree add C:/Users/shash/MySimGodot-wt-car -b mistral/car-assembly 2>&1 | Select-Object -Last 1
git worktree add C:/Users/shash/MySimGodot-wt-hud -b mistral/hud-camera 2>&1 | Select-Object -Last 1
git worktree add C:/Users/shash/MySimGodot-wt-physics -b mistral/physics-gameplay 2>&1 | Select-Object -Last 1
git worktree add C:/Users/shash/MySimGodot-wt-verify -b mistral/verification 2>&1 | Select-Object -Last 1
git worktree list
