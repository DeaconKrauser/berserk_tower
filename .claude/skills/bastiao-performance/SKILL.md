---
name: bastiao-performance
description: Performance rules for the Bastiao dark fantasy tower defense Unity project. Use when touching gameplay systems, enemies, towers, projectiles, effects or UI, or when the game feels slow.
---

When working on gameplay systems, enemies, towers, projectiles, effects or UI:

- Target stable 60 FPS on mainstream PC hardware.
- Profile before and after meaningful optimizations.
- Never optimize based only on intuition.
- Avoid Instantiate/Destroy churn during active waves.
- Use object pooling for:
  - enemies
  - projectiles
  - hit effects
  - floating text
  - temporary particles
- Avoid LINQ and avoidable allocations inside Update/FixedUpdate.
- Avoid GetComponent repeatedly during runtime loops.
- Cache component references.
- Do not call FindObjectOfType / GameObject.Find during gameplay loops.
- Prefer event-driven updates to polling when appropriate.
- Use SpriteAtlas for gameplay sprite groups.
- Batch compatible materials.
- Keep URP effects restrained.
- Monitor GC allocations during heavy waves.
- Test performance with at least 100 active enemies.
- Track median, p95 and worst frame time.
- Always report performance impact after major changes.
