# Changelog

## [2.0.0] - 2026-08-29

### 🎉 Major Rewrite - Lightweight & Feature-Rich

#### Breaking Changes
- **Migrated from Discord GameSDK to discord-rpc-csharp** ([Lachee/discord-rpc-csharp](https://github.com/Lachee/discord-rpc-csharp))
  - 98% smaller package size (19MB → 343KB)
  - 100% managed C# code (no native DLLs required)
  - Cross-platform without platform-specific binaries
- Removed unused SDK components and managers
- Settings now stored globally in EditorPrefs (shared across projects)

#### Added
- **Settings Window** - `Window → Discord RPC Settings`
  - Enable/Disable Rich Presence toggle
  - Customizable display options (project name, scene name, selected object)
  - Custom project name override
  - Up to 2 clickable buttons with custom labels and URLs
  - Update interval configuration (1-10 seconds)
  - Live connection status indicator (🟢 Connected / 🔴 Disconnected)
  - Links to Akryst website and Discord server
- **Selected Object Detection**
  - Shows "Editing [GameObject Name]" when selecting objects in hierarchy
  - Material and mesh asset detection
  - Instant updates on selection change
- **Enhanced Status Display**
  - Project name in Details line
  - Scene name or selected object in State line
  - Session time always visible
  - Small icon shows edit/play mode status

#### Fixed
- Memory leaks with Process and GCHandle disposal
- Race conditions in cleanup with thread-safe locking
- CancellationToken for proper async cleanup
- Enable/Disable toggle now properly clears Discord presence
- Settings properly respect show/hide toggles

#### Removed
- 85 unused files (C/C++ SDK, native DLLs, helper classes)
- Discord GameSDK (19MB) replaced with lightweight library
- URI scheme registration (Join/Spectate features not needed)
- File and console loggers (unused)
- Project-specific settings (now global only)

#### Technical Improvements
- Reduced total lines of code by 76%
- No unsafe code in user-facing components
- Cleaner architecture with single RPC manager
- Better error handling and logging
- Improved Unity 2019.4+ compatibility

## [1.0.1] - 2026-04-09

### Added
- VRChat avatar and world upload detection (requires VRCSDK3)
- Unity Player build detection

### Fixed
- Plugin not loading in the Editor on Windows and Linux
- Domain reload leaving a dangling Discord instance
- Unnamed scenes showing empty state in Discord

### Changed
- Minor stability and performance improvements

## [1.0.0] - 2026-01-08

### Added
- Initial release
- Discord Rich Presence integration for Unity Editor
- Real-time scene and project name updates
- Play/Edit mode detection
- Automatic Discord detection
- Session time tracking
- Live updates every 2 seconds
