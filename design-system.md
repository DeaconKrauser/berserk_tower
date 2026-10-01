# O Último Bastião — Design System

Referências: Berserk (clima), Darkest Dungeon (arte/UI), Kingdom Rush (legibilidade), Castlevania, Diablo (itens).
Regra de ouro: **escuro por padrão, cor só significa algo.** Vermelho = perigo/sangue/runa. Dourado = fogo/valor. Roxo = corrupção.

---

## 1. Paleta

Extraída dos sprites da pasta.

| Token | Hex | Uso | Presente em |
|---|---|---|---|
| `void` | `#0B0A0D` | fundo, outline 1px | todos |
| `stone-900` | `#1C1B22` | painéis | Torre Negra |
| `stone-700` | `#2E2C36` | bordas de painel | Torre Negra, ruínas |
| `stone-500` | `#4A4852` | texto desabilitado | armaduras |
| `iron` | `#8A8C8E` | texto secundário, metal | cavaleiros cinza |
| `bone` | `#E8DCC0` | texto principal | caveira do estandarte, velas |
| `blood` | `#8B1A1A` | HP, dano, perigo | tabardo do Comandante |
| `rune` | `#FF2A2A` | olhos, runas, crítico (glow) | Comandante, boss, runas |
| `ember` | `#F2A33A` | fogo, ouro, seleção | tochas, velas |
| `gold-old` | `#A8894A` | raridade, bordas nobres | — |
| `rust` | `#6B3F26` | madeira, couro | orc, porta, torre de madeira |
| `moss` | `#3F5A2E` | vegetação, veneno | torre arruinada, pele do orc |
| `corruption` | `#5B2A7A` | magia proibida, aura boss | boss, topo torre arruinada |
| `night-teal` | `#1E2A2A` | chão/fundo noturno | folha de sprites |

Nunca usar branco puro nem preto puro em áreas grandes (preto só no outline).

## 2. Pixel art

Pixel art HD detalhada. Tamanhos, PPU, frames e escala: **ver [asset-spec.md](asset-spec.md)** (fonte única).

| Regra | Valor |
|---|---|
| Filtro | Point (no filter), compressão None |
| Outline | 1px `void`, sempre |
| Luz | vem de cima-esquerda |

## 3. Tipografia

| Papel | Fonte (do sistema, primeira encontrada) | Tamanho (px a 1920×1080) |
|---|---|---|
| Títulos, valores da HUD, botões | **Castellar** → Felix Titling → Palatino Linotype | 22–50 |
| Texto | **Palatino Linotype** → Book Antiqua → Georgia | 15–26 |

Títulos em `gold-old`/`ember` com contorno 2 px em sangue escuro; texto em `bone`, secundário em `iron`.
Números flutuantes: ouro em `ember`, crítico em amarelo, execução em `rune`. As fontes vêm do Windows
(`UISkin.titleFonts/bodyFonts`); uma fonte pixel gótica própria (Alagard/m5x7) pode substituí-las depois.

## 4. Componentes de UI (Assets/Art/UI, gerados por tools/build_ui.py)

Pixel art desenhada a 1:1 e exibida a 2× (`Image.pixelsPerUnitMultiplier = 0.5`, canvas `referencePixelsPerUnit = 32`).

- **Painel** (`panel_b6`, `panel_dark_b6`, `panel_solid_b6`): fundo `stone-900`, contorno `void`, filete
  bronze claro/escuro, rebites dourados nos cantos. Menus usam a versão opaca (os fundos pintados têm texto
  em inglês que não pode vazar).
- **Placa de título** (`plate_b8`): faixa com pontas.
- **Botão** (`button_*_b4`): normal / hover (filete dourado) / pressionado / desabilitado (aço). Texto do
  botão desabilitado fica `stone-500`. Clique e hover têm som.
- **Card** (`card_*_b4`): barra de torres e skins; selecionado = moldura `ember`; sem ouro = cinza e preço em vermelho.
- **Botão redondo** (`round_*`): velocidade 1×/2×/4× (ativo = `ember`) e pausa.
- **Barra de vida** (`bar_frame_b3`): preenchimento + "perda recente" clara que escorre em 0,5 s (HUD e mundo).
- **Ícones** (`Icons/*_32|64`): ouro, coração partido (fortaleza), onda, caveira (vivos), elmo coroado
  (chefe), espada+escudo, arco, machado, armadura, fogo, sigilo, mão verde, bolsa (vender), bigorna (melhorar).
- **HUD**: topo (ouro, fortaleza, Onda X/10 · Grupo Y/Z + próxima onda, vivos, 1×/2×/4×, pausa, Iniciar onda),
  comandante embaixo à esquerda (retrato, nome, vida), barra de torres embaixo no centro, barra do chefe que
  surge suave no topo, painel da torre no lado oposto ao da torre selecionada, banner central, dica no cursor.
- **Barras no mundo**: inimigos comuns só depois de levar dano (somem após 3 s), elites sempre, chefe só na HUD.

## 5. Efeitos

| Efeito | Visual |
|---|---|
| Partículas | quadradas 1 px, presas à grade de pixels (sangue, poeira, faíscas, brasas, almas) |
| Impacto | flash curto avermelhado no alvo + anel/flash no ponto; balista com poeira e tremor mínimo |
| Pira | bola de fogo violeta em arco, anel da área, brasas; T4 deixa chão em chamas com luz |
| Obelisco | relâmpago de sangue entre os alvos, "EXECUTADO" em vermelho |
| Corvo | corvo voando até o alvo, partículas violeta sobre amaldiçoados |
| Golpe do comandante | rastro em meia-lua (`Fx.Slash`) no instante do impacto |
| Chefe | entrada com pausa, ênfase de câmera e rugido; telegráfico vermelho antes do golpe especial; tint vermelho ao enfurecer |
| Tremor de tela | só em golpes de chefe, fortaleza atingida e quedas; desligável nas opções |
| Luz | global por mapa (azulado no Mapa 1, esverdeado no Mapa 2), tochas com flicker, portais violeta |

## 6. Sprites

Inventário completo, origem de cada arquivo e o que foi recortado: `tools/sources.py` e
`Docs/animation-pipeline.md`. Animações que ainda precisam de quadros: `Docs/missing-animation-assets.md`.

## 7. Estrutura de pastas (Unity)

```
Assets/
  Art/Source/      originais (nunca editados)
  Art/Generated/   recortes limpos + _debug
  Art/Towers/  Art/Tiles/  Art/Sprites/{Props,Buildings,Projectiles}/
  Art/Animations/<Personagem>/   rig, clipes, prefab, splash, retrato, previews
  Art/UI/{Frames,Icons,Backgrounds}/
  Audio/{SFX,Music,Ambient,Source}/  Bastiao.mixer
  Data/            ScriptableObjects (torres, inimigos, chefes, ondas, rotas, mapas, comandante, skins, meta, áudio, UI)
  Resources/GameConfig.asset
  Scripts/{Core,Maps,Combat,Enemies,Towers,Commander,Waves,UI,Audio,Fx,Data,Testing}/
  Editor/          importador, RigBuilder, ContentBuilder, AudioMixerBuilder, DocsExporter, Build
  Tests/{EditMode,PlayMode}/
```

Números de balanceamento ficam em ScriptableObjects, nunca no código de gameplay.
