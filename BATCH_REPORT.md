# BATCH_REPORT — 2026-11-03 session 2 (Mistral Vibe, sole worker)

## 1. Auth diagnosis
- 403 root cause: GH_TOKEN was unset before the gh workflow run in the same command line (shell ordering), so gh fell back to the active default keyring account (Jdiejei), which lacks repo admin. Bot admin:true was never the problem; earlier owner dispatches succeeded with the same shape when GH_TOKEN stayed exported.
- Verified under exported owner token: gh api user = instagramshashank8-bot; repo collaborator permission = admin (admin:true, maintain/push/triage/pull). No permission change requested.
- Inherited GH_TOKEN/GH_HOST: none in the shell (GH_TOKEN len 0 before export).

## 2. Naming correction
- AlphaToCoverageAndToOne is alpha-to-coverage with alpha forced to one over 4x MSAA (project.godot msaa_3d=3) — NOT alpha hashing. API verified in GodotSharp 4.7.2 XML. Renamed everywhere: depth fence_a2c_review, candidate fence_a2c_candidate, locals a2cFence; no hash claims remain outside benign hashlib/sha256 uses.

## 3. Review-class correction
- MESH000{72,73}_QUANT_OVERVIEW.json relabelled review_class=machine_screened_only with explicit note; no manual image inspection; excluded from manual review counts. Prior wording corrected without deleting history.

## 5. Settings migration
- Unloadable file only resets; versionless legacy file PRESERVED in place and migrated (version stamped on next save); foreign versions kept. Never erases valid legacy settings/progression merely because the marker is missing.

## Dispatch
- fence_a2c_review run 41645765696 queued at head bb4c9f8 (naming + migration included).
