# O Último Bastião — instruções para o Claude

Antes de continuar o desenvolvimento, carregue as skills Unity disponíveis (plugins `unity` e `unity-dev`) e leia
todas as skills locais em `.claude/skills/`.

- Performance: use profiling real (`unity-profiling`) e siga `bastiao-performance`.
- Animação: siga `bastiao-animation`.
- UI: siga `bastiao-ui` (e `unity-game-ui` / `unity-ui-ugui`).
- Visual polish, mapas, estradas, tiles: siga `bastiao-visual-quality` (e `unity-tilemap`, `unity-2d-pixel-perfect`).
- Combate, poderes, torres: siga `bastiao-gamefeel`.

Não considere uma feature concluída apenas porque compila: execute no Unity e valide visualmente/performance
quando possível (smoke test com screenshots em `Build/Bastiao_Data/`, ver README).

Projeto: Unity 6000.6.3f1, URP 2D, PPU 32. Editor em `C:\Program Files\Unity\Hub\Editor\6000.6.3f1`.
Regras do jogo em `rules.md`; pipeline de arte em `Docs/animation-pipeline.md`.
