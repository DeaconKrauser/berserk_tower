# Arte do comandante (Ulric) e skins

Os sprites animados ficam junto dos rigs, em `Assets/Art/Animations/`:

| Skin | Frente (SE) | Costas (NE) | Splash (menus, 3x) | Retrato (HUD) |
|---|---|---|---|---|
| Ulric (base) | `Animations/Commander/` | `Animations/CommanderBack/` | `Animations/Commander/splash.png` | `Animations/Commander/portrait.png` |
| Veterano Escarlate | `Animations/CommanderScarlet/` | `Animations/CommanderScarletBack/` | idem | idem |
| Cavaleiro do Eclipse | `Animations/CommanderEclipse/` | `Animations/CommanderEclipseBack/` | idem | idem |
| Espadachim Negro | `Animations/BlackSwordsman/` | `Animations/BlackSwordsmanBack/` | idem | idem |

Cada pasta tem: `parts/*.png` (partes do rig cutout), `rig.json`, `clips.json`, `<Nome>.prefab`,
`<Nome>_<Estado>.anim`, `<Nome>.controller` e `preview/` (quadros usados nos menus).
Originais: `Assets/Art/Source/Commander/`. Como regenerar: `Docs/animation-pipeline.md`.
