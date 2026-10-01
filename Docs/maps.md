# Mapas

Dois mapas jogáveis, cada um com 3 layouts de estrada (`RouteData`). Quando uma etapa começa, uma rota é
sorteada e mostrada na tela de escolha (com miniatura); durante aquela etapa ela não muda.

- O caminho lógico é um spline Catmull-Rom centrípeto pelos pontos da rota; a estrada visível é pintada em
  Tilemap ao longo dele, com bordas irregulares pixel a pixel (sarjeta escura + pedras soltas).
- Zonas `NoBuild`: portão dos mortos (spawn), muralhas da fortaleza, cemitérios, ruínas, fossos de corrupção,
  água negra (Mapa 2). Props grandes (ruínas, rochas, árvores, totens) também bloqueiam.
- Mapa 2 usa água negra como zona e **pontes de tábuas** onde a estrada cruza a água.
- `tools/route_preview.py` desenha todos os layouts e mede o menor espaço entre trechos de estrada.

| Mapa | Layouts |
|---|---|
| Estrada dos Condenados | Curva dos Enforcados (S), Ziguezague do Carrasco (zigue-zague), Descida da Necrópole (entrada lateral) |
| Pântano das Cinzas | Dois Corredores (dois caminhos que se separam em volta de um lago e se juntam), Gargalos do Lodo (gargalos + ponte), Entrada do Charco (entrada lateral + curva longa) |

Fortaleza (`FortressController`): torre negra + torre de vigia, 20 HP. Cada inimigo que entra tira
`damageToFortress` (1–3; chefes 10). Chefes só destroem a fortaleza inteira se `BossData.fortressExecution`
estiver ligado (desligado nos dois chefes).

<!-- tabelas geradas por Bastião/Exportar tabelas para Docs: não editar abaixo -->

## Estrada dos Condenados
Ruínas medievais, cemitérios e uma estrada de pedra que leva direto aos portões. Mortos de escudo, cães do inferno, encapuzados e cavaleiros negros.

Ouro inicial 285 · fortaleza 20 HP · bônus por onda 20 + 5 × onda · música `music_map1` · ambiente `ambient_map1`

Rotas (uma é sorteada a cada etapa e aparece na tela de escolha):

- **Curva dos Enforcados** (S): 1 caminho(s), largura 1.64, 3 zonas sem construção
- **Ziguezague do Carrasco** (zigue-zague): 1 caminho(s), largura 1.64, 3 zonas sem construção
- **Descida da Necrópole** (entrada lateral): 1 caminho(s), largura 1.64, 2 zonas sem construção

| Onda | Título | Grupos (sub-ondas) |
|---|---|---|
| 1 | Os Primeiros Mortos | 8× Soldado Amaldiçoado (a cada 1.4s)<br>+6s: 6× Soldado Amaldiçoado (a cada 1.2s) |
| 2 | Carne Fresca | 10× Soldado Amaldiçoado (a cada 1.1s)<br>+5s: 6× Cão Infernal (a cada 0.6s)<br>+4s: 8× Soldado Amaldiçoado (a cada 1s) |
| 3 | A Matilha | 10× Cão Infernal (a cada 0.5s)<br>+5s: 10× Soldado Amaldiçoado + 4× Cão Infernal (a cada 0.9s)<br>+4s: 12× Cão Infernal (a cada 0.4s) |
| 4 | Os Encapuzados | 12× Soldado Amaldiçoado (a cada 0.9s)<br>+4s: 3× Encapuzado + 6× Soldado Amaldiçoado (a cada 1.2s)<br>+5s: 10× Cão Infernal (a cada 0.5s) |
| 5 | Aço Negro | 12× Soldado Amaldiçoado (a cada 0.85s)<br>+5s: 2× Cavaleiro Negro + 4× Soldado Amaldiçoado (a cada 2s)<br>+5s: 3× Encapuzado (a cada 1.3s)<br>+5s: 12× Cão Infernal (a cada 0.5s)<br>*[etapa 2+]* +3s: 2× Cavaleiro Negro (a cada 2.5s) |
| 6 | Maré de Ossos | 34× Soldado Amaldiçoado (a cada 0.38s)<br>+6s: 14× Cão Infernal + 4× Encapuzado (a cada 0.5s)<br>+4s: 16× Soldado Amaldiçoado + 2× Cavaleiro Negro (a cada 0.7s)<br>*[etapa 3+]* +3s: 6× Encapuzado + 2× Cavaleiro Negro (a cada 0.6s) |
| 7 | Uivos na Névoa | 20× Cão Infernal (a cada 0.35s)<br>+3s: 16× Soldado Amaldiçoado + 4× Encapuzado (a cada 0.6s)<br>+5s: 24× Cão Infernal (a cada 0.3s)<br>*[etapa 2+]* +3s: 3× Cavaleiro Negro (a cada 1.6s) |
| 8 | Muralha de Ferro | 3× Cavaleiro Negro (a cada 3.2s)<br>+4s: 18× Soldado Amaldiçoado + 5× Encapuzado (a cada 0.6s)<br>+5s: 3× Cavaleiro Negro + 10× Cão Infernal (a cada 1s)<br>*[etapa 2+]* +3s: 6× Encapuzado (a cada 0.8s) |
| 9 | A Horda | 46× Soldado Amaldiçoado (a cada 0.28s)<br>+4s: 20× Cão Infernal + 6× Encapuzado (a cada 0.4s)<br>+4s: 6× Cavaleiro Negro (a cada 1.8s)<br>*[etapa 2+]* +3s: 4× Cavaleiro Negro (a cada 1.5s)<br>*[etapa 3+]* +2s: 20× Cão Infernal + 6× Encapuzado (a cada 0.35s) |
| 10 | O Cavaleiro Amaldiçoado | 18× Soldado Amaldiçoado + 4× Encapuzado (a cada 0.5s)<br>+4s: 12× Cão Infernal (a cada 0.4s)<br>+6s: 1× Cavaleiro Amaldiçoado (a cada 1s)<br>+10s: 20× Soldado Amaldiçoado + 2× Cavaleiro Negro (a cada 0.6s)<br>+8s: 16× Cão Infernal + 4× Encapuzado (a cada 0.45s)<br>*[etapa 2+]* +4s: 4× Cavaleiro Negro + 4× Encapuzado (a cada 1s) |

## Pântano das Cinzas
Lama, água negra e madeira podre. Carniçais famintos, bruxos que protegem a horda e aberrações de lodo que se dividem.

Ouro inicial 340 · fortaleza 20 HP · bônus por onda 22 + 5 × onda · música `music_map2` · ambiente `ambient_map2`

Rotas (uma é sorteada a cada etapa e aparece na tela de escolha):

- **Dois Corredores** (dois corredores): 2 caminho(s), largura 1.6, 3 zonas sem construção
- **Gargalos do Lodo** (gargalos): 1 caminho(s), largura 1.56, 4 zonas sem construção
- **Entrada do Charco** (entrada lateral + curva longa): 1 caminho(s), largura 1.6, 3 zonas sem construção

| Onda | Título | Grupos (sub-ondas) |
|---|---|---|
| 1 | Lama e Dentes | 6× Carniçal (a cada 1.6s)<br>+6s: 6× Cão Infernal (a cada 0.7s) |
| 2 | Fome | 9× Carniçal (a cada 1.1s)<br>+5s: 6× Cão Infernal (a cada 0.6s)<br>+5s: 6× Carniçal (a cada 1.1s) |
| 3 | Lodo Vivo | 3× Aberração do Lodo (a cada 3s)<br>+5s: 8× Carniçal + 6× Cão Infernal (a cada 0.7s) |
| 4 | Os Bruxos | 2× Bruxo Corrompido + 8× Carniçal (a cada 1s)<br>+5s: 10× Cão Infernal (a cada 0.5s)<br>+5s: 3× Aberração do Lodo (a cada 2.5s) |
| 5 | Raízes Famintas | 14× Carniçal (a cada 0.7s)<br>+4s: 3× Bruxo Corrompido + 3× Aberração do Lodo (a cada 1.6s)<br>+4s: 16× Cão Infernal (a cada 0.35s)<br>*[etapa 2+]* +3s: 3× Bruxo Corrompido + 2× Aberração do Lodo (a cada 1.2s) |
| 6 | Pântano em Fúria | 5× Aberração do Lodo (a cada 2s)<br>+5s: 14× Carniçal + 2× Bruxo Corrompido (a cada 0.65s)<br>+5s: 14× Cão Infernal (a cada 0.35s)<br>*[etapa 3+]* +3s: 4× Bruxo Corrompido + 10× Carniçal (a cada 0.6s) |
| 7 | Névoa Verde | 5× Bruxo Corrompido + 12× Carniçal (a cada 0.7s)<br>+4s: 6× Aberração do Lodo + 10× Cão Infernal (a cada 0.8s)<br>*[etapa 2+]* +3s: 4× Aberração do Lodo (a cada 1.4s) |
| 8 | A Grande Carniça | 30× Carniçal (a cada 0.4s)<br>+4s: 4× Bruxo Corrompido + 4× Aberração do Lodo (a cada 1.2s)<br>+4s: 20× Cão Infernal (a cada 0.3s)<br>*[etapa 2+]* +3s: 4× Bruxo Corrompido + 10× Carniçal (a cada 0.7s) |
| 9 | O Lodo Sobe | 10× Aberração do Lodo (a cada 1.2s)<br>+4s: 6× Bruxo Corrompido + 16× Carniçal (a cada 0.5s)<br>+4s: 24× Cão Infernal + 10× Carniçal (a cada 0.3s)<br>*[etapa 2+]* +3s: 6× Aberração do Lodo (a cada 1s)<br>*[etapa 3+]* +2s: 6× Bruxo Corrompido + 16× Cão Infernal (a cada 0.5s) |
| 10 | O Guardião do Pântano | 16× Carniçal + 4× Bruxo Corrompido (a cada 0.6s)<br>+5s: 6× Aberração do Lodo (a cada 1.2s)<br>+6s: 1× Guardião do Pântano (a cada 1s)<br>+10s: 20× Carniçal + 12× Cão Infernal (a cada 0.4s)<br>+8s: 4× Bruxo Corrompido + 4× Aberração do Lodo (a cada 1s)<br>*[etapa 2+]* +4s: 4× Aberração do Lodo + 4× Bruxo Corrompido (a cada 0.8s) |

