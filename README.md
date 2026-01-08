# Unity Editor Discord Rich Presence

Show what you're working on in Unity directly in your Discord status! Perfect for VRChat avatar creators.

## Features

- 🎮 **Real-time updates** - Shows current scene and project name
- 🔄 **Auto-detection** - Automatically detects when Discord is running
- ⏱️ **Session tracking** - Displays how long you've been working
- 🎭 **Play/Edit mode** - Shows different status when testing
- 🚀 **Zero configuration** - Works out of the box

## Installation

### Via VRChat Creator Companion (VCC)

1. Add this repository to VCC:
   ```
   https://akryst.github.io/Unity-Discord-RPC/index.json
   ```

2. In VCC, go to your project and click **Manage Project**

3. Find **Unity Editor Discord Rich Presence** and click **Install**

### Via Git URL (Unity Package Manager)

1. Open Unity
2. Go to **Window → Package Manager**
3. Click **+** → **Add package from git URL**
4. Paste: `https://github.com/Akryst/Unity-Discord-RPC.git`

### Manual Installation

1. Download the latest `.unitypackage` release
2. Import into your Unity project

## Usage

Once installed, it works automatically! Just make sure Discord is running.

## What it shows

- **Project name** - Your Unity project name (updates in real-time!)
- **Current scene** - The scene you're working on
- **Mode** - Whether you're in Edit or Play mode
- **Time** - How long you've been in this Unity session

## Requirements

- Unity 2019.4 or later
- Discord running on your computer
- Windows/Mac/Linux

## Troubleshooting

**Rich Presence not showing?**
- Make sure Discord is running before opening Unity
- Check Unity Console for `[UERP]` messages
- Restart Unity if Discord was launched after Unity

**Scene/Project name not updating?**
- Changes are detected every 2 seconds automatically
- Make sure your scene is saved with a proper name

## For VRChat Creators

This package is perfect for showing your avatar editing workflow! It automatically updates when you:
- Switch between avatar scenes
- Rename your project
- Test your avatar in Play mode

## Credits

Based on [MarshMello0's Editor Rich Presence](https://github.com/MarshMello0/Editor-Rich-Presence)

Uses Discord Game SDK

## License

MIT License - Feel free to use and modify!
