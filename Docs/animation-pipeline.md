# Pipeline de arte e animação

Tudo é reproduzível: os originais ficam intactos em `Assets/Art/Source` (cópias de `novos/` e da raiz,
ver `tools/sources.py`) e cada etapa é um script.

```
python tools/build_assets.py --debug     # torres, tiles, props, ícones, fundos, projéteis
python tools/build_ui.py                 # molduras/botões/cards da HUD (pixel art 1:2)
python tools/build_rigs.py --preview     # rigs cutout + clipes + splash + previews
python tools/build_audio.py              # SFX/músicas/ambiente provisórios + manifest
python tools/route_preview.py            # confere as rotas (espaçamento entre trechos)
Unity: Bastião › Recriar conteúdo (sobrescreve Assets/Data)   # rigs → prefabs, mixer, ScriptableObjects
```

Pastas:

| Pasta | Conteúdo |
|---|---|
| `Assets/Art/Source/` | originais do Syntx/Nano Banana, sem alteração |
| `Assets/Art/Generated/` | recortes limpos em alta resolução (rastreabilidade) e `_debug/` (contact sheets) |
| `Assets/Art/Towers/` | 7 torres × 4 tiers |
| `Assets/Art/Tiles/Map1`, `Map2` | tiles 32×32 (1 unidade) |
| `Assets/Art/Sprites/Props`, `Buildings`, `Projectiles` | cenário, fortaleza, projéteis |
| `Assets/Art/Animations/<Nome>/` | partes do rig, `rig.json`, `clips.json`, prefab, clipes, controller, `preview/`, `splash.png`, `portrait.png` |
| `Assets/Art/UI/` | `Frames/` (9-slice), `Icons/` (32 e 64 px), `Backgrounds/` |

## 1. Limpeza das imagens (tools/pixelkit.py)

```
SOURCE → detectar a cor-chave da borda (#FF00FF) → flood fill a partir da borda (bolsões internos só se
forem quase exatamente magenta) → brilho/halo magenta pintado também vira fundo (glow_like) → descascar
2-4 px de franja rosa → alpha verdadeiro → separar figuras por componentes conexos (sem grade fixa)
→ recortar → reduzir → contorno 1 px #0B0A0D → PNG
```

- **Redução sem interpolação suave:** média por área (pré-multiplicada pelo alpha) seguida de *snap* de
  cada pixel para uma paleta extraída da própria arte (k-means). Nenhuma cor nova, alpha duro, nada de
  bilinear/bicúbico na imagem final. `nearest` puro existe (`method="nearest"`) mas transforma reduções de
  3×+ em ruído; por isso não é o padrão.
- **Grade nativa:** quando a fonte é pixel art ampliada (comandante: 4 px por pixel; boss: 23 px), o
  `native_grid` amostra o centro de cada célula — recupera o desenho original pixel a pixel. Usado na splash
  art do comandante e no Cavaleiro Amaldiçoado (seguido de Scale2x/EPX, que dobra sem inventar cores).
- **Folhas com vários desenhos:** `figures()`/`split_components()` separam por conectividade (o lobo da
  folha de corrida encosta no vizinho: é separado por pixel, não por caixa).
- **JPG com xadrez falso** (arte antiga): `key_checker` (baixa saturação + brilho da borda).
- O importador (`Assets/Editor/PixelArtImporter.cs`) aplica PPU 32, Point, sem compressão, FullRect e pivot nos
  pés (centro para tiles/UI/projéteis). Arquivos `*_b<N>.png` viram 9-slice com borda N. Tiles ficam legíveis
  pela CPU (o mapa pinta a estrada a partir deles).

## 2. Rigs cutout (tools/rigkit.py, rig_defs.py, rig_motion.py)

Nenhum personagem é animado movendo o PNG inteiro. Cada um é cortado em partes com pivô na articulação:

```
Root (Animator + SortingGroup, nunca animado — a gameplay move o pai)
 └ Flip (espelhado para olhar à esquerda; nunca animado)
    └ Body (curvas de corpo inteiro: Spawn)
       └ hips → legF, legB, skirt, torso → head, armF → weapon, armB, cape …
```

1. `rig_defs.py` descreve cada parte em coordenadas normalizadas da figura (polígonos, pivô, pai, ordem).
2. `rigkit.Rig.cut()` atribui cada pixel a uma parte (da frente para trás); o que sobra vai para a parte mais
   próxima. Partes com `fill` têm a área escondida pelas da frente **reconstruída** (inpainting com as cores
   da própria parte), então mover um braço nunca abre buraco.
3. Todas as partes são reduzidas na mesma grade e paleta, com 1 px de sobreposição nas juntas.
4. `rig_motion.py` gera os clipes por tipo de corpo (`humanoid`, `commander`, `quadruped`, `robed`, `slime`):
   pernas alternando no quadril, braço oposto acompanhando, torso com contra-rotação, cabeça e capa com atraso
   (*secondary motion*), arma com inércia. O ataque tem antecipação → preparação → golpe → impacto → recuperação.
5. `Assets/Editor/RigBuilder.cs` lê `rig.json` + `clips.json` e cria prefab, `AnimationClip`s (curvas de
   rotação/posição/escala por parte) e um `AnimatorController` (Idle, Walk, Attack, Hit, Death, e quando há:
   Cast, Special, Spawn, Intro). Walk usa `MoveSpeed`, ataques usam `ActionSpeed`.
6. Gameplay não depende de quadros: dano, golpes e efeitos são temporizadores no código
   (ex.: o golpe do comandante acerta em `ImpactAt = 0.42 s` do clipe de 0.8 s, escalado pela velocidade).

`--preview` gera `Assets/Art/Generated/_debug/rigs/<Nome>_parts.png` (polígonos sobre a arte) e
`<Nome>_clips.png` (6 quadros por estado), renderizados com a mesma matemática da Unity.

## 3. O que cada personagem tem

| Rig | Tamanho | Partes | Estados | Observação |
|---|---|---|---|---|
| Commander = Ulric (+ Scarlet, Eclipse) | 58×74 | capa, 2 pernas, saiote, torso, cabeça, 2 braços, montante | Idle Walk Attack Hit Death Spawn + **Special** (Fúria Negra: giro completo do montante) | movimento feito à mão (`commander`): ataque por cima da cabeça com antecipação, golpe, impacto e recuperação; capa e cabeça com atraso |
| CommanderBack (+ variantes) | 57×74 | 7 | idem | vista de costas: troca automática ao andar para cima da tela; ataques/golpes sempre de frente |
| BlackSwordsman / Back | 59×76 / 61×76 | 9 / 8 | idem | arte própria da skin |
| CursedSoldier | 49×62 | 8 (escudo separado) | Idle Walk Attack Hit Death Spawn | |
| BlackKnight | 56×92 | 9 | idem | |
| Hooded | 34×60 | 5 (barra da túnica anda) | + Cast | túnica: pernas não aparecem |
| CorruptWarlock | 33×64 | 6 | + Cast | variação corrompida do monge da folha antiga + cajado desenhado |
| HellHound | 65×34 | 7 + flipbook | Run = **6 quadros reais** da arte; Idle/Attack/Hit/Death = rig | |
| Ghoul (Carniçal) | 70×46 | 8 (4 patas, cauda, cabeça, mandíbula) | trote em diagonal | |
| SlimeAberration | 51×44 | 3 (corpo, olhos, respingo) | squash & stretch | corpo modelado com a paleta da poça de lodo |
| CursedKnight (chefe) | 136×134 | 8 | + Intro, Special | arte original em grade nativa + Scale2x |
| SwampGuardian (chefe) | 152×150 | 9 (alabarda em 2 partes) | + Intro, Special, Cast | |

Splash art (menus): `splash.png` do comandante e das skins. O comandante e as recolorações usam a **grade
nativa** da design sheet aprovada (1 pixel da arte = 1 pixel do sprite, 147×190), exibida a 3× — a versão anterior
era a figura de 74 px ampliada, por isso parecia borrada/serrilhada. O Espadachim Negro é arte pintada (sem
grade): reduzido por área + paleta para 196 px de altura. O retrato da HUD é um recorte do rosto dessa mesma arte.

## 4. Efeitos

Partículas quadradas no grid de pixels (`Fx.Burst`), anéis de área, flash de impacto, relâmpago do Obelisco,
chão em chamas da Pira T4, flecha cravada, rastro do golpe do comandante (`Fx.Slash`), tremor de câmera leve
(desligável nas opções). Luzes 2D: tochas, pira, capela, obelisco, portais, fortaleza, chefes.
