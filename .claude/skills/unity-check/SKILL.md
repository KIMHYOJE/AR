---
name: unity-check
description: Verify that the Unity 6 project compiles and read Unity's logs (C# errors, warnings, runtime exceptions) without the user copy-pasting the console. Use after writing or changing any C# file, when the user reports a Unity error, or before saying code "works".
---

# Unity compile check

```bash
bash Tools/unity_check.sh          # auto mode
bash Tools/unity_check.sh --log    # only read the live Editor.log
bash Tools/unity_check.sh --batch  # force a headless compile
```

- **Editor open on this project** (`Temp/UnityLockfile` exists): reads `%LOCALAPPDATA%/Unity/Editor/Editor.log`.
  Ask the user to focus the Unity window once so it recompiles, then run the script.
- **Editor closed**: runs the project's `m_EditorVersion` Unity in `-batchmode -nographics -quit`, logging to
  `Logs/batch_compile.log`. The first run imports all assets (minutes) — run it in the background.
- Prints `OK: no C# compile errors` or each `Assets/...(line,col): error CSxxxx` once; package-cache warnings are
  hidden. Exit code 0 = clean.

## Fix loop
1. Run the check. 2. Open each erroring file at the reported line, fix the root cause (not with `#pragma` or by
deleting code). 3. Re-run until OK. 4. Only then tell the user the code compiles — and say that play-mode behavior
is still untested unless they ran it.

## Runtime errors
Play-mode exceptions also land in `Editor.log`; search it for `Exception` / `NullReferenceException` with Grep and
read the stack trace's first `Assets/` frame. Player logs (builds) live at
`%USERPROFILE%/AppData/LocalLow/<CompanyName>/<ProductName>/Player.log`.

## Rules
- Never run batchmode while the editor has the project open (it fails on the lock and can corrupt Library/).
- Never delete `Library/` to "fix" things without asking — it forces a full reimport.
