# Meta-progressão

Só isto sobrevive entre runs (`SaveSystem`, JSON versionado em `Application.persistentDataPath/meta.json`,
gravação atômica via arquivo temporário; save ilegível é preservado como `.corrupt` e o jogo começa do zero):

- Essência (moeda de meta-progressão) e total histórico
- Níveis dos 6 atributos permanentes do comandante
- Skins desbloqueadas e skin equipada
- Fases já vencidas (para a regra de Essência reduzida em repetições)
- Opções (volumes Master/Música/Efeitos/Interface/Ambiente, resolução, tela cheia, tremor de tela, números de
  dano, ondas automáticas)
- Estatísticas (runs iniciadas, mapas vencidos, chefes mortos)

Nada da run é salvo. Testes automatizados conferem as duas coisas (`Assets/Tests`).

## Essência

Paga ao fim de cada mapa (`MetaProgressionData`):

```
essência = 0,1 × abates + 1 × abates de elite + 15 × chefes + 2 × ondas vencidas (+25 se venceu o mapa)
         × (1 + 0,25 × etapa da run)
         × 0,35 se a FASE já tinha sido vencida antes
```

- **Fase** = mapa + etapa da run (ex.: "Estrada dos Condenados, etapa 1"). A primeira vitória numa fase paga a
  Essência integral; repetir uma fase já vencida paga 35% (para não farmar a fase fácil).
  Fases novas (o outro mapa, ou a etapa seguinte da run) pagam integral.
- **Derrota não tira nada**: a Essência ganha até ali é creditada inteira e salva na hora, e como a fase não foi
  vencida, tentar de novo continua contando como fase nova (integral).
- As fases vencidas ficam no save (`clearedPhases`). A tela de escolha avisa "Fase nova: Essência integral" ou
  "Fase já vencida: Essência reduzida"; o resultado explica o que aconteceu.

Ordem de grandeza: vitória inédita no Mapa 1 ≈ 160; repetir a mesma ≈ 55; derrota na onda 10 ≈ 110.

## Atributos do comandante (`CommanderData`)

Base: 700 HP, 38 de dano por golpe (cleave em até 3 inimigos), 1,05 golpes/s, armadura 3, 1 HP/s, segura 3
inimigos, alcance 1,3, velocidade 3,1. Cada atributo vai até o nível 10; custo do nível n = 20 + 15 × n.

| Atributo | Efeito por nível |
|---|---|
| Força | +6% dano físico |
| Vigor | +8% vida máxima |
| Fúria | +5% velocidade de ataque |
| Disciplina | +1,5 de armadura (redução fixa por golpe recebido, no máx. 75% do golpe) |
| Tática | +2,5% de dano para torres a até 3,2 de distância dele |
| Fé Sombria | +0,6 HP/s de regeneração |

Os valores base e as escalas ficam no asset, nunca no código do controlador (`CommanderProgression` só aplica).

## Skins (`CommanderSkinData`)

Só aparência (rig de frente, rig de costas, splash, retrato). Nenhum atributo muda.

| Skin | Custo | Origem da arte |
|---|---|---|
| Comandante | grátis | design sheet aprovada |
| Veterano Escarlate | 100 | recoloração da design sheet (armadura laqueada vermelha, manto cinza) |
| Espadachim Negro | 150 | design sheet própria entregue (encapuzado, lâmina larga às costas) — personagem original |
| Cavaleiro do Eclipse | 250 | recoloração (aço negro e violeta) |
