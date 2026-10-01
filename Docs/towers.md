# Torres

Sete construções, cada uma com 4 tiers. Cada tier guarda os atributos completos (não multiplicadores) e o
seu próprio sprite (`Assets/Art/Towers/<id>_t1..t4.png`, das folhas entregues). Os preços sobem forte, então
**melhorar compete com construir**. Dados: `Assets/Data/Towers/*.asset` + `Upgrades/`.

| Torre | Papel | Contra o quê | Sofre contra |
|---|---|---|---|
| Torre de Arqueiros | DPS barato, alvo único, rápido | Cães, carniçais, encapuzados (fracos a físico) | Cavaleiros Negros (armadura 14), Aberração do Lodo (resiste a físico) |
| Balista | golpe lento e pesado, alcance longo, perfura armadura, caça o mais resistente | Elites, blindados, chefes | hordas rápidas |
| Pira Amaldiçoada | fogo em área (ignora armadura), queimadura, chão em chamas no T4 | Hordas de soldados, lodo (fraco a fogo) | Cães Infernais e Encapuzados (resistem a fogo) |
| Capela de Guerra | aura: +dano, +velocidade, +alcance às torres; cura o comandante; T4 tira armadura | multiplica as vizinhas | não ataca |
| Torre do Corvo | maldição: +% de dano sofrido, lentidão, T3 quebra armadura, T4 a maldição se espalha | Chefes e blindados (prepara o dano das outras) | dano direto baixo |
| Obelisco de Sangue | rajada mágica que salta entre inimigos; T3 sangramento; T4 executa abaixo de 15% | grupos densos, elites (magia ignora armadura) | Bruxos (resistem a magia); caro |
| Armadilha de Estacas | **construída sobre a estrada**; fere, faz sangrar e atrasa quem passa; **se desgasta**: cada inimigo atingido tira 1 de durabilidade (elite 2, chefe 4) e em 0 ela quebra (sem reembolso); melhorar a reconstrói; vender devolve proporcional ao que resta | hordas, rápidos | Lodo (não sangra), chefes (gastam 4 por pisada) |

**Evolução das torres:** clique na torre → painel mostra o tier atual, o próximo, o que muda ("antes → depois") e
o preço; MELHORAR [U] desconta o ouro, troca o sprite pelo do novo tier e aplica os novos atributos na hora.
Testado em `Upgrade_SpendsGold_Sell_ReturnsValue` e visível na screenshot `alpha_map1_upgraded_tower.png`.

Combinações que o jogo recompensa: Corvo + Balista contra chefes, Capela no centro de um grupo de torres,
Pira atrás de Estacas (inimigos lentos em cima do fogo), Obelisco onde a estrada se dobra (mais saltos).

<!-- tabelas geradas por Bastião/Exportar tabelas para Docs: não editar abaixo -->

Venda: 70% do investido (100% se vendida na mesma preparação em que foi construída). Dano físico mínimo através de armadura: 15%.

### Torre de Arqueiros  (tecla 1)
Barata e rápida · alvo único · sofre contra armadura. **Bom contra rápidos e frágeis.** Tipo: Projectile, construção: fora da estrada, alvo: First, área ocupada: raio 0.55.

| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |
|---|---|---|---|---|---|---|
| 1 | Torre de Arqueiros | 70 | 11 físico | 1.67 | 3.6 |  |
| 2 | Arqueiros Veteranos | 100 | 12 físico | 2.38 | 3.8 |  |
| 3 | Flechas Perfurantes | 160 | 15 físico | 2.27 | 4 | Penetração 3; Perfura +1 inimigos |
| 4 | Chuva Rubra | 290 | 16 físico | 2.38 | 4.3 | Penetração 4; Alvos por disparo 2; Perfura +1 inimigos; Crítico 15% ×2,0 |
- Tier 2: Tensão maior e mãos firmes: atiram bem mais rápido.
- Tier 3: Pontas de ferro negro atravessam o primeiro alvo e ferem quem vem atrás.
- Tier 4: Dois alvos por disparo e golpes críticos.

### Balista  (tecla 2)
Lenta · alcance longo · perfura armadura · caça o mais resistente. **Bom contra blindados, elites e chefes.** Tipo: Projectile, construção: fora da estrada, alvo: Strongest, área ocupada: raio 0.6.

| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |
|---|---|---|---|---|---|---|
| 1 | Balista | 140 | 75 físico | 0.38 | 5 | Penetração 8 |
| 2 | Molas de Ferro Negro | 190 | 115 físico | 0.42 | 5.3 | Penetração 9 |
| 3 | Virotes Rúnicos | 280 | 140 físico | 0.43 | 5.6 | Penetração 20 |
| 4 | Virote Pesado | 420 | 190 físico | 0.48 | 6 | Penetração 22; Contra elites/chefes ×1,6; Atordoar 25% · 0,6s |
- Tier 2: Mais tensão: muito mais dano e alcance.
- Tier 3: Runas de sangue rasgam placas de aço: ignora quase toda armadura.
- Tier 4: Feito para derrubar colossos: +60% contra elites e chefes, pode atordoar.

### Pira Amaldiçoada  (tecla 3)
Fogo em área · ignora armadura · fraca contra quem resiste ao fogo. **Bom contra hordas.** Tipo: Projectile, construção: fora da estrada, alvo: First, área ocupada: raio 0.6.

| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |
|---|---|---|---|---|---|---|
| 1 | Pira Amaldiçoada | 120 | 16 fogo | 0.56 | 3.3 | Área 1,2 |
| 2 | Óleo de Cadáver | 170 | 18 fogo | 0.59 | 3.4 | Área 1,3; Queimadura 7/s · 3s |
| 3 | Chamas Negras | 260 | 24 fogo | 0.63 | 3.6 | Área 1,8; Queimadura 9/s · 3s |
| 4 | Inferno do Eclipse | 380 | 30 fogo | 0.67 | 3.8 | Área 1,9; Queimadura 12/s · 3s; Chão em chamas 18/s · 4s |
- Tier 2: As chamas grudam: inimigos continuam queimando.
- Tier 3: A explosão cresce: área muito maior.
- Tier 4: Cada impacto deixa o chão em chamas por alguns segundos.

### Capela de Guerra  (tecla 4)
Suporte · fortalece torres próximas · cura o comandante na aura. **Multiplica torres vizinhas.** Tipo: Aura, construção: fora da estrada, alvo: First, área ocupada: raio 0.65.

| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |
|---|---|---|---|---|---|---|
| 1 | Capela de Guerra | 130 | — | — | 3 | Raio da aura 3,0; Dano das torres +15%; Cura do comandante 5 HP/s |
| 2 | Cânticos de Batalha | 180 | — | — | 3.2 | Raio da aura 3,2; Dano das torres +18%; Vel. das torres +12%; Cura do comandante 7 HP/s |
| 3 | Relicário dos Mártires | 260 | — | — | 3.5 | Raio da aura 3,5; Dano das torres +22%; Vel. das torres +15%; Alcance das torres +10%; Cura do comandante 10 HP/s |
| 4 | Marca da Heresia | 380 | — | — | 3.8 | Raio da aura 3,8; Dano das torres +26%; Vel. das torres +18%; Alcance das torres +12%; Armadura inimiga -5; Cura do comandante 14 HP/s |
- Tier 2: As torres na aura atacam mais rápido.
- Tier 3: Ossos santos: alcance maior para as torres.
- Tier 4: Inimigos na aura perdem armadura.

### Torre do Corvo  (tecla 5)
Debuff · amaldiçoa: mais dano sofrido e lentidão. **Bom contra chefes e blindados (prepara as outras torres).** Tipo: Projectile, construção: fora da estrada, alvo: Elite, área ocupada: raio 0.55.

| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |
|---|---|---|---|---|---|---|
| 1 | Torre do Corvo | 110 | 6 mágico | 0.83 | 3.8 | Lentidão 15% · 2s; Maldição +15% dano sofrido |
| 2 | Revoada | 150 | 7 mágico | 0.91 | 3.9 | Alvos por disparo 2; Lentidão 20% · 2s; Maldição +18% dano sofrido |
| 3 | Olho do Abismo | 230 | 8 mágico | 1 | 4.1 | Alvos por disparo 2; Lentidão 25% · 2s; Maldição +22% dano sofrido; Quebra armadura -6 |
| 4 | Praga dos Corvos | 340 | 9 mágico | 1.05 | 4.3 | Alvos por disparo 3; Lentidão 30% · 2,2s; Maldição +30% dano sofrido; Quebra armadura -8; Praga espalha ao morrer |
- Tier 2: Dois corvos por vez, maldição mais funda.
- Tier 3: A maldição corrói a armadura.
- Tier 4: Três alvos; a maldição pula para os vizinhos quando o amaldiçoado morre.

### Obelisco de Sangue  (tecla 6)
Caro · rajada mágica que salta entre inimigos. **Bom contra grupos densos e elites (dano mágico ignora armadura).** Tipo: Chain, construção: fora da estrada, alvo: First, área ocupada: raio 0.6.

| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |
|---|---|---|---|---|---|---|
| 1 | Obelisco de Sangue | 200 | 60 mágico | 0.42 | 3.9 | Corrente 1 saltos |
| 2 | Sangue que Salta | 240 | 70 mágico | 0.45 | 4 | Corrente 3 saltos |
| 3 | Hemorragia Rúnica | 330 | 90 mágico | 0.5 | 4.1 | Sangramento 8/s · 3s; Corrente 4 saltos |
| 4 | Sacrifício | 480 | 115 mágico | 0.53 | 4.4 | Crítico 15% ×2,0; Sangramento 10/s · 3s; Corrente 5 saltos; Execução < 15% HP |
- Tier 2: A rajada salta entre até 3 inimigos.
- Tier 3: Mais saltos, mais dano, e os atingidos sangram.
- Tier 4: Executa inimigos comuns abaixo de 15% de vida.

### Armadilha de Estacas  (tecla 7)
Armada SOBRE a estrada · fere, faz sangrar e atrasa quem passa · se desgasta a cada pisada. **Bom contra hordas e rápidos.** Tipo: Trap, construção: sobre a estrada, alvo: First, área ocupada: raio 0.5.

| Tier | Nome | Custo | Dano | Ataques/s | Alcance | Efeitos |
|---|---|---|---|---|---|---|
| 1 | Armadilha de Estacas | 60 | 22 físico | 1.11 | sobre ela | Penetração 2; Sangramento 4/s · 3s; Lentidão 30% · 1,2s; Durabilidade 45 pisadas |
| 2 | Estacas Farpadas | 90 | 26 físico | 1.11 | sobre ela | Penetração 2; Sangramento 7/s · 3s; Lentidão 30% · 1,2s; Durabilidade 65 pisadas |
| 3 | Fosso de Lanças | 150 | 34 físico | 1.18 | sobre ela | Penetração 4; Sangramento 7/s · 3s; Lentidão 45% · 1,5s; Durabilidade 90 pisadas |
| 4 | Empalador | 240 | 44 físico | 1.25 | sobre ela | Penetração 5; Sangramento 10/s · 3s; Lentidão 50% · 1,5s; Atordoar 20% · 1s; Durabilidade 130 pisadas |
- Tier 2: Ferimentos que não fecham; estacas novas e mais resistentes.
- Tier 3: Mais dano, segura muito mais e aguenta mais pisadas.
- Tier 4: Chance de empalar (atordoa); ferro negro que quase não gasta.

