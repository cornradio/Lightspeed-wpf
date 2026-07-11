## Lightspeed v1.3.0 Release Notes

### Search

- Added global search (Ctrl+P or top-right button) across all folders 0-9
- Real-time filtering as you type, with system file icons and folder tags
- Arrow keys navigate results, Enter opens, Escape closes
- Opens search overlay centered with dimmed background
- Recent files shown by default when search box is empty
- Recently used files ranked higher in search results

### Gamepad Toggle

- Added gamepad enable/disable toggle button (bottom-left, default ON)
- When disabled, all gamepad input is ignored except the summon/hide hotkey
- Prevents accidental gamepad control when playing games

### Drag and Drop

- Folders can now be dragged into the app (fixed previous file-only limitation)
- Drag items from the file list onto folder buttons 0-9 to move between folders
- External files/folders can be dropped onto specific folder buttons

### Icon Size Slider

- Deferred rendering: slider drag only updates the number display, no lag
- Changes apply when closing the settings panel (with "refreshing" toast)
- Manual number input via text box with auto-clamp to valid range
- Global refresh ensures all folders use the new icon size after change
- Icons now correctly fill the full requested size (no more small icons in large containers)

### Other Fixes

- Icon size settings now persist correctly across app restarts
- Keyboard input no longer triggers shortcuts when search overlay is open
- Search result items use real system icons instead of emoji text
