# Animações que precisam de novos quadros

Todos os personagens já animam por rig cutout (partes separadas), sem "PNG pulando". O que está abaixo é o
que um rig **não consegue** inventar a partir de uma pose única — gere estes quadros no Syntx e coloque em
`Assets/Art/Source/...`; o pipeline troca o clipe do rig pelo flipbook (como já acontece na corrida do Cão
Infernal) sem mudar código de gameplay.

Padrão de pedido (ver `asset-spec.md` §5): fundo #FF00FF chapado, sem sombra, mesmo personagem/escala/linha
de pés em todos os quadros, tira horizontal com células iguais. Ângulos: **SE** = 3/4 frente-direita,
**NE** = 3/4 costas-direita (as outras direções são espelhadas no jogo).

| Asset | Animação faltante | Quadros necessários | Ângulo | Por que o rig não basta |
|---|---|---|---|---|
| Comandante | Walk | 6 | SE e NE | o rig gira as pernas no quadril, mas o joelho não dobra e a capa não ondula de verdade |
| Comandante | Attack (golpe largo) | 6 (1 preparação, 2 pico, 3 início, 4 impacto com rastro, 5 seguimento, 6 recuperação) | SE | o ombro gira inteiro; falta o torso torcer e o braço de trás segurar o cabo |
| Comandante | Hit | 2 | SE | ok no rig; quadros dariam recuo do capacete |
| Comandante | Special — Fúria Negra (giro de montante) | 8 (preparação agachada, ergue, 4 de giro, impacto no chão, recuperação) | SE | o rig gira o braço 360°, mas o torso não acompanha o giro |
| Comandante | Walk / Attack de costas | 6 / 6 | NE | hoje a vista de costas é rig da pose única de costas |
| Comandante | Death | 6 (cai de joelhos, espada cravada, tomba) | SE | hoje cai rodando do quadril |
| Espadachim Negro | Walk / Attack / Death | 6 / 6 / 6 | SE e NE | arte pintada (não pixel): recorte em partes perde detalhe nas juntas |
| Veterano Escarlate, Cavaleiro do Eclipse | — | — | — | são recolorações do comandante; herdam o que ele ganhar |
| Soldado Amaldiçoado | Walk | 6 | SE | escudo e perna da frente se sobrepõem na pose única |
| Soldado Amaldiçoado | Attack | 4 | SE | golpe de espada por cima do escudo |
| Cavaleiro Negro | Walk / Attack | 6 / 6 | SE | arte só de frente/perfil; capa longa cobre as pernas |
| Encapuzado | Walk / Cast | 6 / 4 | SE | túnica sem pernas visíveis: hoje a barra balança |
| Bruxo Corrompido | **Arte própria** + Walk / Cast | 1 pose + 6 / 4 | SE | hoje é o monge da folha antiga recolorido com um cajado desenhado por código |
| Cão Infernal | Attack (bote) / Death | 4 / 4 | SE | a corrida já usa os 6 quadros entregues; o resto é rig do quadro 1 |
| Carniçal | Walk (trote) / Attack (mordida) | 6 / 4 | SE | 4 patas da pose única se sobrepõem |
| Aberração do Lodo | **Arte própria** + Walk / Death (divisão) | 1 pose + 4 / 6 | SE | não veio arte: o corpo foi modelado com a paleta da poça de lodo do tileset |
| Cavaleiro Amaldiçoado (chefe) | Intro / Walk / Attack / Special / Death | 8 / 6 / 6 / 8 / 8 | SE | arte original de baixa resolução (≈68 px): rig funciona, mas o espadão é um bloco |
| Guardião do Pântano (chefe) | Intro / Walk / Attack / Special (raízes) / Death | 8 / 6 / 6 / 8 / 8 | SE | braço de raízes e alabarda grandes demais para girar sem distorcer |
| Fortaleza | estados danificada / em ruínas | 2 | SE | hoje o dano aparece com tinta + fumaça |
| Torres | disparo (recuo da balista, chama da pira, corvo decolando) | 3–4 por torre | SE | hoje: squash do sprite + partículas |

Prioridade sugerida: 1) Comandante Walk/Attack SE, 2) Soldado Walk, 3) chefes Attack/Special,
4) Cão/Carniçal Attack, 5) arte própria do Bruxo e da Aberração.
