# O Último Bastião: Eclipse Negro — Regras

Tower defense dark fantasy com elementos leves de hero defense. Você defende uma fortaleza no fim da estrada
com torres construídas livremente no terreno e um Comandante que luta junto. Mundo, criaturas e símbolos
próprios — Berserk só como clima, nada copiado.

Engine: **Unity 6 + C#, URP 2D**. PC/Steam, 16:9, 1920×1080 (960×540 internos). Câmera 3/4.

## 1. Escopo da alpha (o que está no jogo)

- Menu: NOVO JOGO · COMANDANTE · SKINS · OPÇÕES · SAIR (sem "Continuar": runs não são salvas)
- 2 mapas (Estrada dos Condenados, Pântano das Cinzas), 3 layouts de estrada cada, 10 ondas com sub-ondas,
  chefe na onda 10
- 7 torres × 4 tiers, construção livre com área ocupada (footprint)
- 10 inimigos + 2 chefes, elenco diferente por mapa
- Comandante com 6 atributos permanentes e 4 skins
- Run de mapa em mapa com dificuldade crescente; Essência como moeda permanente
- **Fora da alpha:** recursos múltiplos, tropas, moral, feridas, relíquias, ciclo dia/noite, IA adaptativa,
  multiplayer (ver §9, visão futura)

## 2. Loop

Preparação (construir/melhorar/vender, posicionar o comandante) → **Iniciar onda** → sub-ondas chegam pela
estrada → onda vencida paga bônus de ouro e cura 35% do comandante → … → onda 10 com chefe → resultado e
Essência → próximo destino (o mesmo mapa ou o outro), sem as construções, com dificuldade maior.
Detalhes e controles: `Docs/game-loop.md`.

## 3. Vitória e derrota

| Condição | Resultado |
|---|---|
| Vencer as 10 ondas do mapa | Mapa concluído → escolher próximo destino (a run continua) |
| Fortaleza chega a 0 | Derrota: a run termina (a Essência ganha fica) |
| Comandante cai | Volta na fortaleza após 14 s. `respawnSeconds = 0` restaura a regra antiga (queda = derrota) |

Essência: integral na primeira vitória de cada fase (mapa + etapa) e em qualquer derrota (nada se perde);
35% ao repetir uma fase já vencida. Ver `Docs/progression.md`.

Fortaleza: 20 HP. Cada inimigo que entra tira 1–3; chefes tiram 10 (nunca a destroem sozinhos, a menos que o
chefe tenha `fortressExecution` ligado — desligado nos dois).

## 4. Economia

| Fonte | Valor |
|---|---|
| Ouro inicial | 300 (Mapa 1), 340 (Mapa 2) + ouro levado da etapa anterior |
| Abates | 2–22 por inimigo comum/elite, 250–300 por chefe |
| Fim de onda | 20 + 5 × onda (Mapa 2: 22 + 5 × onda) |
| Venda | 70% do investido (100% se ainda na mesma preparação em que construiu) |
| Transição | leva 25% do ouro que sobrou (máx. 150) + 40 × etapas vencidas |

Melhorias custam bem mais que a torre (ex.: Balista 140 → 190 → 280 → 420): construir mais ou melhorar é
sempre uma escolha.

## 5. Construção

Escolha a torre → fantasma segue o mouse → **verde** válido / **vermelho** inválido com o motivo → clique
constrói, botão direito/Esc cancela. Proibido: estrada, portão dos mortos (spawn), muralhas da fortaleza,
sobre outra construção, sobre o comandante ou inimigos, zonas NoBuild (cemitério, ruínas, fosso de corrupção,
água negra) e props que bloqueiam. Toda checagem usa a área ocupada da construção, não só o ponto central.
Exceção: a **Armadilha de Estacas** só pode ser armada SOBRE a estrada.

## 6. Torres, inimigos e chefes

Mira: cada torre tem um padrão (balista: mais forte; arqueiros: primeiro à frente…). O botão **TORRES MIRAM** da HUD
troca todas de uma vez (padrão → primeiro à frente → mais forte, salvo nas opções) e o painel da torre (ou **T**)
troca só a selecionada. "Mais forte" = maior vida máxima (chefes e elites primeiro). A balista gira a arma e
acompanha o alvo enquanto ele anda.

- Torres: `Docs/towers.md` (papéis, tiers, preços, efeitos)
- Inimigos e chefes: `Docs/enemies.md` (HP, armadura, resistências, tags, habilidades)
- Ondas e rotas: `Docs/maps.md`

Dano: físico (armadura fixa por golpe, mínimo 15%), fogo, magia e sangramento (só resistência). Status:
queimadura, sangramento, lentidão, maldição (+dano sofrido, −armadura), atordoamento.

## 7. Comandante — Ulric, Capitão da Companhia do Corvo

Selecionável, clique para mover (botão direito sempre move), ataca sozinho quem entra no alcance: golpe largo
de montante em até 3 inimigos. Segura até 3 inimigos comuns (eles vão até ele e o cercam); chefes sempre param
para lutar. A vida aparece na HUD. Atributos permanentes (Força, Vigor, Fúria, Disciplina, Tática, Fé Sombria)
e skins: `Docs/progression.md`.

**Poder [Q] — cada skin tem o seu** (começa o mapa meio carregado; a força escala com Força):

| Skin | Poder | Recarga | Efeito |
|---|---|---|---|
| Ulric | Fúria Negra | 45 s | golpe giratório: 160 físico em raio 2,4, atordoa comuns 1,4 s; depois 8 s de fúria (+40% vel. de ataque, +25% dano, −30% dano recebido) |
| Espadachim Negro | Armadura do Abismo | 40 s | veste a armadura (mesma fúria de 8 s) e avança como um raio sobre até 6 inimigos seguidos (60% da força em cada, atordoa comuns 0,5 s) |
| Veterano Escarlate | Estandarte Escarlate | 50 s | crava um estandarte por 10 s: torres num raio de 4 com +35% dano e +25% vel. de ataque; cura 35% da vida |
| Cavaleiro do Eclipse | Eclipse | 45 s | explosão mágica (60% da força) em raio 3,6; 6 s de maldição (+30% dano sofrido, −6 armadura) e 45% de lentidão, chefes inclusive |

Ondas: manuais (Espaço/botão) ou automáticas (opção; 12 s de preparação entre ondas a partir da 2ª).

## 8. Dificuldade da run

Por mapa vencido: +22% HP, +15% dano, +12% unidades, spawns 8% mais densos, +6% de chance de elite, chefes
+30% HP e +1 invocação, +5% de ouro. Grupos extras (`minRunTier`) entram a partir da etapa 2/3 com
composições novas. Teto: etapa 7.

## 9. Visão futura (fora da alpha)

Mantida do documento original para orientar o jogo completo: ciclo dia/noite com 50 noites; recursos (ferro,
madeira, almas, fé, homens); tropas (soldado, lanceiro, arqueiro, cavaleiro, monge, mago proibido) com ordens;
moral por unidade; feridas permanentes e veteranos; relíquias amaldiçoadas; IA adaptativa dos comandantes
inimigos; muralhas/ferreiro/calabouço como estruturas.
