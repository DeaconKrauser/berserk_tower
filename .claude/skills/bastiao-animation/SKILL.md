---
name: bastiao-animation
description: Animation quality rules for characters, monsters and bosses in Bastiao (cutout rigs in tools/rig_defs.py, tools/rig_motion.py, Assets/Editor/RigBuilder.cs). Use for any commander, skin, enemy or boss animation work.
---

Never simulate a full character animation by bouncing the root sprite.

Required animation states when applicable:
- Idle
- Walk/Run
- Attack
- Hit
- Death

Bosses may also use:
- Spawn
- Special
- Cast

Rules:
- Root movement represents world movement.
- Visual animation happens on the visual child/Animator.
- Feet should feel planted during grounded motion.
- Attacks require anticipation, action, impact and recovery.
- Weapons must visibly move during attacks.
- Quadrupeds must have readable leg movement.
- Capes, tails and cloth should use restrained secondary motion.
- Do not use scale pulsing as the primary idle animation.
- If asset frames are insufficient, use cutout animation or document missing frames.
- Never ship a "PNG hopping" animation.
- Animation speed must match gameplay movement speed.
- Hit reactions must be short and responsive.
