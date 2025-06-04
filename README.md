# EVA Construction Mode Ground Fix

A Kerbal Space Program mod that prevents parts from being placed underground during EVA Construction Mode.

## Problem Description

There is a bug in KSP's EVA Construction Mode that can cause serious issues with ground-anchored structures:

1. An engineer deploys a ground anchor (like a Clamp-O-Tron)
2. They enter EVA Construction Mode and attach various parts to it
3. While in EVA Construction Mode, they can accidentally place parts underground
4. When the game is reloaded, the game will attempt to fix this by raising the entire structure above the ground
5. This results in the ground anchor floating above the surface, which is both unrealistic and can cause stability issues

## Solution

This mod prevents parts from being placed underground during EVA Construction Mode by:

- Monitoring part positions during EVA Construction Mode
- Detecting when a part would intersect with the ground
- Preventing the part from being placed in an invalid position

## Technical Details

The mod works by:
- Using Unity's physics system to detect ground collisions
- Supporting various collider types (Box, Capsule, Sphere, and Mesh colliders)
- Maintaining a history of valid part positions
- Restoring parts to their last valid position when an invalid placement is detected

## Installation

1. Download the latest release
2. Extract the contents to your KSP GameData folder
3. The mod will automatically activate when you enter EVA Construction Mode

## Requirements

- Kerbal Space Program 1.12 or later
- Breaking Ground DLC (for EVA Construction Mode)

## License

This mod is released under the MIT License. See the LICENSE file for details.
