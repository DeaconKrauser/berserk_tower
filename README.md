# O Último Bastião — Eclipse Negro (alpha 0.2)

Tower defense dark fantasy em Unity 6 (6000.6.3f1, URP 2D, PPU 32, Point). Defenda a fortaleza no fim da
estrada com 7 torres de 4 tiers e um comandante que luta junto. Runs não são salvas; Essência, atributos do
comandante, skins e opções são.

## Rodar

- Editor: abra `G:\berserk_tower` no Unity Hub, cena `Assets/Scenes/SampleScene`, Play. Tudo é montado por
  código a partir de `Assets/Resources/GameConfig.asset` (sem prefabs de cena).
- Build: `Unity.exe -batchmode -quit -projectPath . -executeMethod Build.Windows` → `Build/Bastiao.exe`.

## Controles

1–7 ou cards: escolher torre · clique: construir (verde válido / vermelho inválido) · botão direito/Esc: cancelar ·
clique na torre: painel (U melhora, Del vende, T troca o alvo) · clique no comandante (ou C) e depois no chão: mover — botão
direito move sempre · Espaço: iniciar onda · F ou 1×/2×/4×: velocidade · P/Esc: pausa.

## Onde está cada coisa

| | |
|---|---|
| Regras e números | `rules.md`, `Docs/*.md` (tabelas geradas dos dados) |
| Dados (ScriptableObjects) | `Assets/Data/` + `Assets/Resources/GameConfig.asset` |
| Código | `Assets/Scripts/{Core,Maps,Combat,Enemies,Towers,Commander,Waves,UI,Audio,Fx,Data,Testing}` |
| Arte original (intocada) | `Assets/Art/Source/` (cópia de `novos/` e dos arquivos da raiz) |
| Arte processada | `Assets/Art/{Towers,Tiles,Sprites,Animations,UI}` |
| Áudio | `Assets/Audio/{SFX,Music,Ambient}` (provisório, gerado), `Assets/Audio/Source` (rosnados entregues) |
| Ferramentas | `tools/*.py` (Python 3 + numpy + Pillow; ffmpeg para áudio) e menus `Bastião/…` no editor |

## Regenerar conteúdo

```
python tools/build_assets.py      # torres, tiles, props, ícones, fundos de menu
python tools/build_ui.py          # molduras/botões da HUD
python tools/build_rigs.py        # rigs cutout, clipes, splash art
python tools/build_audio.py       # SFX, música e ambiente provisórios
Unity: Bastião › Recriar conteúdo  (ou -executeMethod ContentBuilder.BuildAllAndPlayer)
Unity: Bastião › Exportar tabelas para Docs
```

`ContentBuilder` **sobrescreve** `Assets/Data`: é a ferramenta de autoria dos números. Se preferir ajustar no
Inspector, não rode de novo (ou edite os números em `Assets/Editor/ContentBuilder.cs`).

## Testes

- Unity Test Framework: `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode` (9 testes) e
  `-testPlatform PlayMode` (8 testes): jogo novo sem torres, nada é construído sozinho, ouro inicial, não constrói
  na estrada nem sobreposto, upgrade desconta, venda devolve o valor, sub-ondas, fim de mapa abre a escolha,
  troca de mapa limpa construções, meta sobrevive ao fechamento, run não sobrevive, skin persiste, chefe entra e
  morre, áudio sem clip não quebra, smoke test só com a flag.
- Smoke test (só com a flag): `Build/Bastiao.exe -smoketest [-maps map1,map2] [-route N] [-strategy balanced|archers|spam|upgrades] [-speed 4] [-noshots] [-savepath arquivo] [-scenario crowd|showcase] [-skin id]`.
  Passa pelos menus, joga a run sozinho e grava screenshots em `Build/Bastiao_Data/`. Usa um save próprio
  (`smoke_meta.json`), nunca o do jogador. Sem `-smoketest`, nenhuma ação automática existe: os pontos de
  entrada automáticos passam por `Automation.Require` e recusam com erro.
