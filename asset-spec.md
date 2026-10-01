# Padrão de Assets — Unity + Nano Banana

Fonte única de tamanho, escala, frames e prompts. Se um asset não segue isto, ele é refeito.

---

## 1. Números travados

| Item | Valor |
|---|---|
| Resolução interna | **960×540**, Pixel Perfect Camera → ×2 = 1920×1080 (ver nota abaixo) |
| **PPU** | **32** (todos os sprites, sem exceção) |
| Soldado humano (1 unidade visual) | **64 px de altura** de corpo = 2 unidades Unity |
| Célula humanoide | **96×96**, pivot **Bottom Center** (pés) |
| Sombra | **nunca no sprite** — blob separado no Unity |
| Filtro / compressão | Point / None, Mesh Type Full Rect |
| Câmera | 3/4, ~30° de elevação, sprites em pé (não achatados) |
| Chão | tile quadrado **32×32** = 1 unidade (Tilemap); estrada pintada pixel a pixel por cima |
| Luz | de cima-esquerda, sempre |
| Outline | 1 px `#0B0A0D` |
| Formato final | **PNG 32-bit com alpha real** |

**Nota (alpha):** a resolução foi travada em 640×360, mas o jogo roda em **960×540 (2×)**. Motivo: com
mapa inteiro em tela, HUD legível, 7 torres e 30–40 inimigos simultâneos, 640×360 deixava o campo apertado
demais (um soldado de 64 px ocupava 1/6 da altura). **Os tamanhos dos sprites deste documento não mudaram**
(soldado 64 px, comandante ~74, cavaleiro ~96, chefes 134–150); só cabe mais mundo na tela. Voltar a
640×360 é uma linha (`CameraRig.Setup`), se for preferível.

Pipeline real dos assets do Syntx (recorte, limpeza do magenta, redução sem interpolação suave, rigs):
`Docs/animation-pipeline.md`. Pedidos de quadros que faltam: `Docs/missing-animation-assets.md`.

## 2. Escala (altura do corpo, sem arma erguida)

| Classe | Mult. | Altura px | Célula |
|---|---|---|---|
| Soldado, arqueiro, lanceiro, monge | 1,0× | 64 | 96×96 |
| Comandante, elites | 1,15× | ~74 | 96×96 (ataque: 128×96) |
| Cavaleiro Negro, criatura média | 1,5× | ~96 | 128×128 |
| Criatura grande | 2× | ~128 | 192×192 |
| Boss | 3–5× | 192–320 | 384×384 |

A célula é o **envelope da animação** (espada, capa, antecipação), não o tamanho do corpo. Ataques largos podem usar célula mais larga (ex.: `128×96`) — mesma altura, pivot nos pés. Não amputar animação para caber no quadrado.

**Régua de escala:** o soldado de referência aparece **só na design sheet** (§6.1). Aprovada, ela vira a referência congelada. Spritesheets finais **nunca** incluem o soldado (o modelo mistura personagens e invade células).

## 3. Direções e frames

Câmera 3/4 → **2 direções desenhadas** (SE = frente-direita, NE = costas-direita). SW/NW = `flipX`.

| Animação | Frames | FPS | Loop |
|---|---|---|---|
| idle | 4 | 6 | sim |
| walk | 6 | 10 | sim |
| attack | 6 | 12 | não (hit no frame 4) |
| hit | 2 | 12 | não |
| death | 6 | 10 | não (último frame fica) |

**24 frames por direção.** Duas direções desenhadas (SE + NE) = **48 frames-fonte por personagem**. SW e NW **não são desenhadas**: o Unity gera por `flipX`. Uma folha de 24 frames = UMA direção, nunca o personagem inteiro. Vertical slice: tropas e inimigos comuns podem sair **só com SE** (NE depois).

Estruturas: 1 frame + estado **danificado** + **ruína**. Tocha/runas: 3–4 frames de loop.

### Layout da spritesheet
Uma linha por animação, na ordem da tabela acima, colunas = frames, grade exata, sem espaçamento. Unity: Sprite Mode Multiple → Slice **Grid By Cell Size**.

## 4. Pipeline (mata o xadrez falso)

O Nano Banana não gera alpha — ele *desenha* o xadrez. Então:

1. **Pedir fundo chroma sólido `#FF00FF` (magenta)**, sem sombra no chão, sem glow tocando a borda. Magenta porque não existe na paleta (verde conflita com `moss`, e o roxo de corrupção é bem mais escuro).
2. Gerar **1 frame por vez ou tira de 4–6**; folhas grandes perdem consistência.
3. Limpar e reduzir: `tools/build_assets.py` / `tools/build_rigs.py` (chave magenta por flood fill, remoção de
   halo, alpha verdadeiro, separação por componentes, redução por área + paleta da própria arte, nunca
   bilinear/bicúbico; grade nativa quando a fonte é pixel art ampliada).
4. Passe manual opcional no **Aseprite** para os sprites finais (alinhar pés, travar paleta).
5. Tiras de animação entregues viram flipbook (como a corrida do Cão Infernal); poses únicas viram rig cutout.

Efeitos de glow (runas, aura) **não** vão pintados no sprite: viram Light2D / partículas na Unity.

## 5. Bloco base de prompt (colar em todo prompt)

```
Pixel art sprite, HD detailed pixel art (not 8-bit, not retro 16x16),
dark fantasy medieval, brutal and decaying, grim atmosphere.
3/4 top-down view from ~30 degrees, character standing upright.
Light from top-left. 1px near-black outline (#0B0A0D).
Limited palette: near-black, dark stone grays, iron gray, bone white #E8DCC0,
blood red #8B1A1A, rune red #FF2A2A, ember orange #F2A33A, rust brown, moss green,
corruption purple #5B2A7A.
Solid pure #FF00FF background filling the whole image. Hard pixel edges only.
Absolutely no antialiasing, glow, bloom, colored fringe, shadow or blending
between the character and background.
No checkerboard, no transparency pattern, no ground shadow, no text, no watermark, no frame border.
No blur, no gradients.
Original design, not based on any existing franchise character.
```

Para tiras de animação, acrescentar:
```
Horizontal sprite strip of N frames, equal-width cells, same character, same size,
same scale, feet on the same baseline in every frame, evenly spaced, nothing overlapping cells.
```

## 6. Comandante (primeiro asset)

Usar `9e8a1bac…jpg` como **imagem de referência** no Nano Banana (estilo + design), não como asset.

### 6.1 Folha de design (aprovar antes de animar)
```
[BLOCO BASE]
Character design turnaround of the COMMANDER, a lone knight-captain defending the last fortress.
Heavy blackened plate armor, battered and scratched, dark red cloth tabard and torn long cloak.
Closed great helm with a single narrow slit; one glowing red eye visible (left eye lost, scar across helm).
Carries an oversized crude greatsword, almost as tall as himself, notched iron blade.
Heavy, tired, unbreakable posture. Broad shoulders.
Show 2 views side by side: front-right 3/4 (SE) and back-right 3/4 (NE).
Next to him, for scale, a plain human foot soldier in gray plate armor, noticeably shorter
(commander is 1.15x the soldier's height).
```

### 6.2 Idle SE (4 frames)
```
[BLOCO BASE] [BLOCO TIRA, N=4]
The same commander from the reference, facing front-right 3/4 (SE).
Idle breathing loop: greatsword resting on shoulder, cloak moving slightly, subtle chest rise.
```

### 6.3 Walk SE (6)
```
[BLOCO BASE] [BLOCO TIRA, N=6]
The same commander, facing SE. Heavy slow walk cycle, greatsword on shoulder, cloak trailing.
```

### 6.4 Attack SE (6)
```
[BLOCO BASE] [BLOCO TIRA, N=6]
The same commander, facing SE. Wide horizontal greatsword sweep:
1 wind-up, 2 wind-up peak, 3 swing start, 4 impact with motion smear, 5 follow-through, 6 recover.
```

### 6.5 Hit (2) e Death (6)
```
[BLOCO BASE] [BLOCO TIRA, N=2]  ... recoiling from a blow, then bracing.
[BLOCO BASE] [BLOCO TIRA, N=6]  ... falls to one knee, sword planted in ground, collapses forward, lies still.
```

Repetir 6.2–6.5 trocando "facing SE" por "facing back-right 3/4 (NE), seen from behind".

## 7. Ordem de geração do kit mínimo (vertical slice)

1. Comandante (SE+NE) ← começar aqui
2. Soldado de referência (régua de escala; também serve de base para lanceiro/arqueiro)
3. Lanceiro, Arqueiro (SE)
4. Soldado Amaldiçoado, Encapuzado, Cão Carniçal, Cavaleiro Negro (SE)
5. Boss Cavaleiro Amaldiçoado (SE, 384×384)
6. Estruturas: núcleo, torre de arqueiros, ferreiro, capela, muralha (normal/danificado/ruína)
7. Tileset do chão (terra, lama, pedra, grama morta) 64×32
8. Props: tocha, estandarte, ruínas, cadáveres

Checklist de aprovação por asset: fundo magenta limpo · altura bate com a design sheet congelada · zero pixel rosa/roxo na borda · pés na mesma linha · paleta travada · sem texto/logo · nada reconhecível de franquia.
