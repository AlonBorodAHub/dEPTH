# dEPTH

A Windows game-library frontend for Samsung Odyssey 3D monitors. Browse floating cover art over a curved stereo grid, with rotating console models, a depth-animated opening, controller navigation, and optional head-tracked perspective.

**v0.1.0-beta.3 — early public beta.** Developed and tested on one Odyssey 3D setup; broader hardware and emulator compatibility needs community testing. dEPTH renders side-by-side images. Samsung's Odyssey 3D Hub performs the display conversion. dEPTH does not turn arbitrary games into stereoscopic 3D.

## Download and start

1. Download `dEPTH-v0.1.0-beta.3-win-x64.zip` from Releases and extract the entire archive into a writable folder, such as `C:\Games\dEPTH`. Do not run inside the ZIP or install into Program Files.
2. Install and start Samsung Odyssey 3D Hub and its required display runtime. Confirm your Odyssey monitor works in 3D. Run the included `Enable-Hub-Switching.cmd` once to enable automatic SBS only during dEPTH sessions, or manage Hub conversion manually. Make the Odyssey your primary display for this beta.
3. Run `Depth.exe`. First-run setup asks for each system's existing emulator executable and game folder. Subfolders are scanned; your games can stay where they are. Steam scanning is optional and does not certify a game's 3D support.
4. Choose **Save and scan**. The library starts black until Hub reports an active 3D session, then plays the opening animation.
5. Run `Setup.cmd` while dEPTH is closed to add or change scan folders. Edited game profiles are preserved on rescans; removing a source does not delete previously imported games.

The frontend is portable and does not require administrator rights. The optional Hub switching helper requires one administrator approval and installs a background task at Windows sign-in. Windows x64, .NET Framework 4.8, an OpenGL-capable graphics driver, and a working Odyssey 3D Hub installation are required. Samsung/Leia runtimes are not bundled. The executable is unsigned.

No games, ROMs, firmware, BIOS, console keys, emulators, personal library, saved states, or game artwork are included. Supply your own lawful game files and existing emulator installations. Keep `SDL2.dll` and the `data/models` folder beside `Depth.exe`.

## What is new in beta 3

- A gyro-controlled blue star pointer for compatible Nintendo Switch Pro Controllers, including reliable controller handoff when entering and leaving Dolphin.
- Smooth pointer hover scrolling, soft 3D selection lift and shadow, and a short depth-aware sparkle trail.
- Improved keyboard navigation between game rows and platform tabs, an in-app Settings panel, and an About screen.
- A curved, softened background grid, refined opening audio timing, and optional looping menu audio.
- `Depth Record.exe`, which launches dEPTH in a visible raw side-by-side presentation and records the complete primary display with system audio to an MP4 on the Desktop.

The public archive does not redistribute the copyrighted music and startup recordings used by the development installation. To use your own sounds, provide 16-bit PCM WAV files at `data/audio/startup.wav` and `data/audio/menu.wav`.

## Recording a side-by-side demonstration

Place a current Windows x64 `ffmpeg.exe` beside `Depth Record.exe`, or make FFmpeg available on `PATH`, then double-click `Depth Record.exe`. Recording begins one second after dEPTH launches and ends when dEPTH closes. The recorder saves a timestamped MP4 to the Desktop, including system audio. It keeps the frontend and launched emulators in raw side-by-side form so the captured wide video can be played later as 3D content.

On NVIDIA systems, the recorder uses Desktop Duplication and NVENC to target constant 60 fps at the primary display's full resolution. Other systems fall back to Windows desktop capture and software H.264; sustained 4K60 performance is not guaranteed with the fallback. The mouse arrow is excluded from the recording. FFmpeg is a separate project and is not bundled.

## Systems and setup

| System | Import and stereo requirements |
| --- | --- |
| Nintendo 3DS | Select Azahar and a folder of `.3ds`, `.cci`, `.cxi` or `.app` files. Configure SBS in Azahar first. Optional separate touch window is placed on a secondary display below the Odyssey. |
| GameCube / Wii | Select Dolphin and an ISO/GCM/RVZ/WIA/WBFS folder. Disc headers distinguish GameCube from Wii. Configure Dolphin's SBS output first. |
| Dreamcast | Select Flycast and a CHD/GDI/CDI folder. A separately installed compatible stereo modification is required; ordinary Flycast does not provide this integration by itself. |
| PlayStation 2 | Select PCSX2 and an ISO/CHD/CSO/BIN folder. Stereo requires a separately configured compatible setup. |
| PlayStation 3 | Select RPCS3 and the parent folder containing disc game folders or `dev_hdd0/game`. Bootable EBOOT files and PARAM.SFO metadata are scanned; update packages are excluded. Stereo depends on game/emulator support. |
| Switch | Import XCI/NSP files with an existing compatible emulator and launch arguments. Stereo support is setup-specific. No emulator downloads or console keys are provided. |
| PC / Steam | Optional installed-library scan, or manually add an executable. Steam VR titles can be imported too; import does not mean stereo compatibility. |

Other systems can be added manually through **Settings → Add game**, with an executable and arguments. `{rom}` is replaced with the quoted game path. Test each emulator's 3D output independently before launching it through dEPTH.

This beta does not automatically configure emulators, install stereo modifications, or apply widescreen hacks. The optional helper changes Hub conversion settings during dEPTH sessions. Emulators retain responsibility for game saves and stereo output. Launching Dolphin supplies session-only fullscreen and stop-confirmation options.

## Automatic Hub switching

Run `Enable-Hub-Switching.cmd` once from the extracted application folder and accept the Windows administrator prompt. The helper enables automatic SBS and suppresses Hub's conversion popup while dEPTH runs. After dEPTH exits, including a crash, it disables automatic conversion and restores the popup for normal PC video. It preserves automatic SBS across internal process replacements when returning from games.

This optional integration was tested with **Odyssey 3D Hub 1.5.1** and uses its internal settings connection; future Hub versions may require updates. It installs `dEPTH Hub Session` in Task Scheduler and the helper in `Program Files\dEPTH`. The frontend and games remain unelevated. Use the same administrator account for setup and playing; elevation using a different user's credentials is not supported.

Run enable again if you move or upgrade dEPTH into another folder. To remove the integration, close dEPTH and run `Disable-Hub-Switching.cmd`. This removes its scheduled task and retains the helper files. The latest status is in `Program Files\dEPTH\HubSessionStatus.txt`. If the helper cannot connect, check Hub is running; manual conversion remains available. Build the helper and run its unit checks with `build-hub-session.ps1` when building from source.

## Controls

| Input | Action |
| --- | --- |
| D-pad / left stick / arrow keys | Select game |
| A / Enter / click | Launch or confirm |
| B / Escape | Back or clear search |
| L / R shoulder | Change system tab |
| X | Toggle favorite |
| Y / F3 | Search |
| Start / + | Main menu |
| Both stick clicks (L3 + R3) | Exit launched game; quit dEPTH from library |
| F2 / right-click game | Edit profile and choose local cover art |

Save in-game before exiting. dEPTH first requests a normal game close, then can terminate an unresponsive launched process. Head-tracked perspective can be turned off in Settings. Favorites opens by default when favorites exist; otherwise Library opens.

The code includes optional Home/Capture and trigger-chord integrations, but these are **not plug-and-play across emulators**. Native mappings must be configured separately. Trigger + D-pad sends F17–F20 to compatible Flycast setups and F19/F20 for Azahar depth; Dolphin uses its own native mappings. Per-game stereo persistence/reset is disabled in fresh public libraries because it requires additional native configuration. Do not assume these shortcuts save or restore settings in this beta.

## Artwork and second screen

Choose your own images in each game's profile, or close dEPTH and run the optional `Fetch-Covers.ps1` using PowerShell. It downloads exact-title matches from Libretro thumbnails and Steam; availability varies. This sends title-based requests to those services. Artwork belongs to its respective owners and is not bundled.

An optional extended desktop display below the Odyssey shows companion details. SuperDisplay can supply that display; it is a separate product. No companion screenshot collection is bundled. Without downloaded companion media, the second screen uses the frontend background and game details. A second screen is not required.

## Troubleshooting and data

- **Black startup / Hub retry dialog:** ensure Hub is running with automatic SBS conversion, the Odyssey is primary, and the display camera can track you. dEPTH deliberately waits for fresh Hub log evidence before revealing SBS frames. This relies on Hub's current log format and may need updates for other versions.
- **Game stays black:** verify the game independently produces SBS and Hub detects it. Unsupported or ordinary 2D games may fail the conversion gate. Controller L3+R3 returns to the library.
- **Missing game:** use Setup again or add a manual profile. Only supported file extensions are scanned; no archive extraction is performed.
- **No controller:** confirm the controller is connected before launching and retain the included x64 SDL2 DLL. Button labels follow SDL mappings and may vary by controller.
- **Settings:** stored beside the executable in `data/library.json`. Back up the `data` folder before upgrading; never overwrite your library with someone else's. Installing a new release into a separate folder is safest. Logs can contain local paths; review them before posting an issue.

## Build

On Windows with .NET Framework 4.8, run `powershell -NoProfile -File .\build.ps1`. Prepared meshes and textures are included in the source package, so Python is not required to build or run. Copy SDL2 2.28.2 x64 from its official release into `bin` for controller support. `Launch.cmd` runs the build. `PublicReleaseChecks.cs` validates portable scan/persistence behavior using temporary fixtures; it does not modify installed emulators.

## License

Original application code: [MIT](LICENSE). Third-party models retain their own licenses; see [credits](assets/models/CREDITS.md) in source, or `data/models/CREDITS.md` in the portable ZIP, and [third-party notices](THIRD-PARTY-NOTICES.md).

The bundled Dreamcast model is CC BY-NC 4.0; PS2 and PS3 models are CC BY-NC-SA 4.0, including their modified versions. Those assets carry noncommercial restrictions even though the application code is MIT. Other included console models are CC BY 4.0. Keep all attribution and license files when redistributing assets. dEPTH is an independent project, not affiliated with Samsung, Leia, Nintendo, Sega, Sony, or emulator developers.
