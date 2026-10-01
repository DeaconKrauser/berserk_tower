# Inimigos

Cada mapa tem o seu elenco (`MapData.roster`); só o Cão Infernal aparece nos dois. Dados em
`Assets/Data/Enemies/*.asset` e `Assets/Data/Bosses/*.asset`.

Regras de dano (`Combat/DamageSystem.cs`):

- **Físico**: armadura fixa por golpe (menos penetração), nunca abaixo de 15% do golpe; depois resistência física.
- **Fogo / Magia / Sangramento**: ignoram armadura; só a resistência do tipo vale.
- **Maldição** (Torre do Corvo): +% de dano sofrido de qualquer fonte e, a partir do T3, armadura reduzida.
- Resistência negativa = fraqueza (ex.: Soldado −25% fogo → recebe 125%).

Tags e o que significam na prática:

| Tag | Exemplos | Resposta esperada |
|---|---|---|
| Rápido | Cão Infernal | Arqueiros, Estacas (lentidão), comandante segurando |
| Blindado | Cavaleiro Negro, chefes | Balista (penetração), Pira/Obelisco (ignoram armadura), Corvo T3 |
| Horda | Soldado, Carniçal, Lodo | Pira, Obelisco, Estacas |
| Elite | Encapuzado, Cavaleiro Negro, Bruxo | foco (Balista prioriza o mais resistente, Corvo prioriza elites) |
| Conjurador | Encapuzado, Bruxo | matar primeiro: escudam/curam o grupo |
| Chefe | Cavaleiro Amaldiçoado, Guardião do Pântano | combinação: Corvo + Balista + Capela, comandante desviando do golpe especial |

Elites "promovidos": a partir da 2ª etapa da run, inimigos comuns podem nascer como elite (+70% HP, +3 de
armadura, +35% de dano, 2,2× ouro, tom avermelhado e barra de vida sempre visível).

Movimento: cada inimigo segue o caminho da sua rota com uma faixa lateral própria e mantém espaço pessoal
(corpos nunca se sobrepõem; quadrúpedes têm corpo alongado). Quem está atrás desvia de quem para e, se ficar
travado, contorna o obstáculo. Quando o comandante está perto e ainda pode segurar inimigos, eles saem da
estrada, vão até ele e o cercam; ao fim da luta voltam para o caminho.

<!-- tabelas geradas por Bastião/Exportar tabelas para Docs: não editar abaixo -->

| Inimigo | Tags | HP | Armadura | Res. física | Res. fogo | Res. magia | Res. sangue | Vel. | Dano fortaleza | Corpo a corpo | Ouro |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Soldado Amaldiçoado | Horda · Morto-vivo | 175 | 3 | 0% | -25% | 0% | 20% | 0.95 | 1 | 10 / 1.3s | 5 |
| Cão Infernal | Rápido · Fera | 70 | 0 | 0% | 50% | 0% | 0% | 2.2 | 1 | 6 / 0.7s | 3 |
| Encapuzado | Elite · Morto-vivo · Conjurador | 220 | 1 | -20% | 60% | 30% | 0% | 1.1 | 2 | 14 / 1.2s | 9 |
| Cavaleiro Negro | Blindado · Elite | 860 | 14 | 0% | 0% | 0% | 30% | 0.7 | 3 | 32 / 1.5s | 22 |
| Cavaleiro Amaldiçoado | Blindado · Chefe · Morto-vivo | 4900 | 12 | 30% | -15% | 0% | 50% | 0.5 | 10 | 110 / 2s | 250 |
| Carniçal | Horda · Morto-vivo · Fera | 210 | 2 | 0% | -30% | 0% | -30% | 1.25 | 1 | 11 / 0.9s | 6 |
| Aberração do Lodo | Horda | 520 | 0 | 45% | -35% | 0% | 80% | 0.65 | 2 | 18 / 1.6s | 14 |
| Bruxo Corrompido | Elite · Conjurador | 220 | 0 | -25% | 0% | 60% | 0% | 0.95 | 2 | 10 / 1.4s | 12 |
| Guardião do Pântano | Chefe · Fera | 6400 | 8 | 15% | 25% | -20% | 30% | 0.48 | 10 | 130 / 2.2s | 300 |
| Lodo Menor | Horda | 120 | 0 | 30% | -35% | 0% | 80% | 1 | 1 | 8 / 1.2s | 2 |

- **Soldado Amaldiçoado**: Morto-vivo de escudo. Vem em massa; queima fácil.
- **Cão Infernal**: Rápido e frágil; o fogo do inferno não o queima.
- **Encapuzado**: Elite conjurador: protege os aliados com escudos sombrios. Resiste ao fogo. — a cada 8s: escudo 35 em aliados num raio 2
- **Cavaleiro Negro**: Armadura pesada: flechas mal arranham.
- **Cavaleiro Amaldiçoado**: Chefe. Ergue os mortos, atordoa torres com golpes sísmicos e enfurece ferido. — chefe: entrada de 2.2s; invoca 2× Soldado Amaldiçoado a cada 14s; **Golpe Sísmico** a cada 11s (raio 2.4, 120 no comandante, torres atordoadas 2.5s); enfurece abaixo de 40% (velocidade ×1.45, dano ×1.3); execução da fortaleza: não
- **Carniçal**: Devora quem morre por perto e se regenera. Sangra muito. — cura 18 quando um inimigo morre a até 1.5; regenera 1 HP/s (não enquanto queima)
- **Aberração do Lodo**: Absorve golpes físicos e se divide ao morrer. Queima bem. — ao morrer vira 2× Lodo Menor
- **Bruxo Corrompido**: Conjurador: escuda e cura os aliados. Resiste a magia. — a cada 7.5s: escudo 45 e cura 20 em aliados num raio 2
- **Guardião do Pântano**: Chefe. Raízes que atordoam torres, carniçais famintos e fúria. — chefe: entrada de 2.4s; invoca 2× Carniçal a cada 16s; **Raízes do Pântano** a cada 12s (raio 2.8, 90 no comandante, torres atordoadas 3s); enfurece abaixo de 35% (velocidade ×1.4, dano ×1.25); execução da fortaleza: não
- **Lodo Menor**: Pedaço da aberração.
