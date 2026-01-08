# Unity Editor Discord Rich Presence

Display your Unity project and scene information in your Discord status.

Designed for VRChat avatar creators.

## Features

- Real-time scene and project name updates
- Automatic Discord detection
- Session time tracking
- Play/Edit mode indicators
- Zero configuration required

## Installation

### Via VRChat Creator Companion (VCC)

1. Add this repository to VCC:
   ```
   vcc://vpm/addRepo?url=https://akryst.github.io/Unity-Discord-RPC/index.json
   ```

2. Navigate to your project in VCC and select **Manage Project**

3. Locate **Unity Editor Discord Rich Presence** and click **Add**

## Usage

Install the package and ensure Discord is running. The integration works automatically.

## Discord Status Display

- **Project Name** - Your Unity project name
- **Current Scene** - Active scene
- **Mode** - Edit or Play mode
- **Timestamp** - Session duration

## Requirements

- Unity 2019.4 or later
- Discord application
- Windows, macOS, or Linux

## Troubleshooting

**Status not appearing:**
- Launch Discord before opening Unity
- Check Unity Console for `[UERP]` messages
- Restart Unity if Discord was opened afterwards

**Information not updating:**
- Updates occur every 2 seconds
- Ensure scene is saved with a proper name

## For VRChat Creators

Automatically updates when you:
- Switch between avatar scenes
- Rename your project
- Test avatars in Play mode

## Credits

Based on [MarshMello0's Editor Rich Presence](https://github.com/MarshMello0/Editor-Rich-Presence)

Uses Discord Game SDK

## License

MIT License
