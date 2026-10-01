# Loop de jogo (alpha)

```
MENU ─ NOVO JOGO ─► ESCOLHA SEU DESTINO (Mapa 1 | Mapa 2, rota já sorteada)
                        │
                        ▼
                 ┌─ PREPARAÇÃO: construir / melhorar / vender, posicionar o comandante
                 │      (nenhuma torre existe: tudo é construído pelo jogador)
                 │                 │ [Espaço] Iniciar onda
                 │                 ▼
                 │   ONDA N/10 · GRUPO A/B  (sub-ondas com atraso entre si)
                 │                 │ todos mortos → bônus de ouro + cura parcial do comandante
                 └──────── próxima onda (até a 10ª, com chefe)
                        │
          fortaleza 0 ──┤── onda 10 vencida
               ▼        ▼
            DERROTA   MAPA CONCLUÍDO (resultado + Essência)
               │        │
               │        ▼
               │   ESCOLHA SEU PRÓXIMO DESTINO (repetir ou trocar; dificuldade sobe)
               │        │ ... a run continua até a derrota ou até o jogador encerrar
               ▼        ▼
             MENU  (Essência já foi salva; a run não)
```

- **Comandante**: Ulric, Capitão da Companhia do Corvo (nome e título em `CommanderData`).
  Poder **Fúria Negra**: gira o montante em volta de si (160 de dano físico em área, escala com Força,
  atordoa inimigos comuns 1,4 s) e entra em fúria por 8 s (+40% velocidade de ataque, +25% dano, −30% dano recebido).
- **Derrota**: a fortaleza chega a 0. O comandante **cai e volta** após 14 s na fortaleza
  (`CommanderData.respawnSeconds`; com 0 a queda dele encerra o mapa, como no protótipo).
- **Run**: existe só na memória (`RunManager`). Fechar o jogo, voltar ao menu ou perder encerra a run.
  Não há botão "Continuar".
- **Transição entre mapas**: as construções ficam para trás (a batalha inteira é destruída). Vai junto:
  25% do ouro que sobrou (máx. 150) + 40 de bônus por etapa já vencida.
- **Dificuldade da run** (`RunDifficulty`, por mapa vencido): +22% HP, +15% dano, +12% unidades por grupo,
  intervalos 8% menores, +6% de chance de elite, chefes +30% HP e +1 invocação, +5% ouro por abate.
  Além disso, grupos marcados `minRunTier` só aparecem a partir da etapa 2 ou 3 (composições novas, não só
  números maiores).

## Controles

| Ação | Comando |
|---|---|
| Escolher torre | clique no card da barra inferior ou teclas 1–7 |
| Construir | clique no chão (fantasma verde = válido, vermelho = inválido + motivo) · Shift mantém o modo |
| Cancelar construção | botão direito ou Esc |
| Selecionar torre | clique nela → painel com atributos, próximo tier, MELHORAR [U] e VENDER [Del] |
| Selecionar comandante | clique nele, no retrato, ou C |
| Mover comandante | com ele selecionado, clique no chão · botão direito move sempre |
| Iniciar onda | Espaço ou botão "Iniciar onda" |
| Ondas manuais/automáticas | botão "ONDAS: MANUAIS/AUTOMÁTICAS" (topo direito) ou Opções. No automático, depois da 1ª onda a próxima começa sozinha após 12 s de preparação (Espaço antecipa) |
| Poder do comandante | Q ou o botão redondo no painel do comandante: **Fúria Negra** (recarga 45 s, começa meio carregado) |
| Velocidade | 1× 2× 4× (botões) ou F |
| Pausa | P, II ou Esc (menu de pausa: retomar, som, abandonar a run) |

## Câmera, escala e leitura

960×540 internos (PPU 32) ampliados para 1920×1080 pela Pixel Perfect Camera. Soldado ≈ 62 px, comandante 74,
cavaleiro negro 92, chefes 134–150, torres 37–103 px. Ordenação por Y (pivô nos pés).

## Sistemas (código)

`Assets/Scripts/`: `Core/` (GameManager, SceneFlow, RunManager, Battle, BattleInput, SaveSystem, Automation),
`Maps/` (MapController, RouteController, BuildZoneController, FortressController), `Combat/` (DamageSystem,
Health, StatusEffects), `Enemies/` (EnemyController, EnemyMovement, EnemyCombat, EnemyAbilities, BossController),
`Towers/` (TowerController, TowerCombat, TowerTargeting, TowerPlacementController, Projectile),
`Commander/` (Controller, Movement, Combat, Progression, SkinController), `Waves/` (WaveManager,
SpawnController), `UI/` (HUD, painel da torre, menus), `Audio/` (AudioManager, MusicManager), `Fx/`,
`Data/` (ScriptableObjects), `Testing/SmokeTest.cs`.
