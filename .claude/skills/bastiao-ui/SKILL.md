---
name: bastiao-ui
description: UI/UX rules for the Bastiao tower defense (HUD, tower cards, tower panel, menus, commander and skin screens in Assets/Scripts/UI). Use for any UI work.
---

UI must be readable at 1920x1080 and scale correctly to common 16:9 resolutions.

HUD priorities:
- Gold
- Fortress HP
- Wave
- Sub-wave
- Enemies alive
- Commander HP
- Game speed

Tower cards must clearly show:
- icon
- cost
- hotkey
- affordability state

Selected tower panel must show:
- name
- tier
- damage
- range
- attack speed
- special effect
- upgrade cost
- sell value

Use dark panels with restrained medieval ornamentation.
Do not cover significant gameplay space.

Animations:
- 100-200ms hover/press transitions
- smooth HP changes
- subtle panel transitions
- no long blocking animations

Menu flow:
Main Menu
→ New Game
→ Map Selection
→ Gameplay

No Continue button.
