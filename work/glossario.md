# Glossário e regras — tradução pt-BR do Fallout Shelter

**Estilo escolhido:** manter os termos **icônicos do Fallout em inglês**; traduzir naturalmente todo o resto para português do Brasil.

## 1) NÃO TRADUZIR — manter exatamente em inglês (respeitando plural e CAIXA do original)
Termos de marca / universo Fallout:
- Vault, Vault-Tec, Vault Door, Vault Boy, Vault Dweller
- Dweller, Dwellers
- Overseer
- Caps
- Stimpak, Stimpaks, RadAway
- Nuka-Cola, Nuka-Cola Quantum, Nuka
- Wasteland
- Pip-Boy, Mr. Handy
- Lunchbox, Lunchboxes
- S.P.E.C.I.A.L., SPECIAL, Pet
- Rad, Rads

Criaturas/inimigos (nomes Fallout) — manter: Deathclaw, Radroach, Mole Rat, Ghoul, Feral Ghoul, Raider, Raiders, Super Mutant.

**Nomes próprios** de personagens, lugares, armas, trajes (outfits), pets e quests: manter como no inglês (não traduzir o nome em si).

## 2) TRADUÇÕES CANÔNICAS — use SEMPRE assim
| Inglês | pt-BR |
|---|---|
| Power | Energia |
| Water | Água |
| Food | Comida |
| Health | Saúde |
| Happiness | Felicidade |
| Damage | Dano |
| Healing | Cura |
| Quest | Missão |
| Objective | Objetivo |
| Reward | Recompensa |
| Weapon | Arma |
| Outfit | Traje |
| Junk | Sucata |
| Theme | Tema |
| Room | Sala |
| Level | Nível |
| Experience / XP | Experiência / XP |
| Training | Treinamento |
| Pregnant / Pregnancy | Grávida / Gravidez |
| Build | Construir |
| Craft / Crafting | Fabricar / Fabricação |
| Rush | Acelerar |
| Incident | Incidente |
| Settings / Options | Configurações / Opções |
| Storage | Depósito |
| Inventory | Inventário |

(Quando uma palavra da seção 1 aparecer numa frase, mantenha-a em inglês e traduza o resto: ex. "Not enough Caps" → "Caps insuficientes"; "Assign a Dweller" → "Atribuir um Dweller".)

## 3) REGRAS DE FORMATO (críticas — não quebrar)
1. **Preserve EXATAMENTE** e não traduza nada dentro de: `{...}` (ex. `{0}`, `{1}`, `{0:N0}`, `{name}`), `[...]` (ex. `[NUMBER]`, `[VALUE]`), e tags `<...>` (ex. `<sprite=..>`, `<color=#..>`, `<b>`, `</b>`, `<size=..>`). O conjunto desses tokens na tradução deve ser **idêntico** ao do inglês.
2. **Quebras de linha** vêm escapadas como `\n` no texto de entrada — **mantenha os `\n`** na mesma posição lógica. Idem `\t`.
3. **Mantenha a CAIXA** do original: TUDO MAIÚSCULO → traduza em maiúsculas; Title Case → Title Case; frase normal → frase normal.
4. Não adicione nem remova espaços/pontuação nas pontas.
5. `%` e números: mantenha como estão.
6. Use as colunas **Spanish** e **German** como referência de sentido/terminologia quando o inglês for ambíguo (mas o estilo é pt-BR, seção 1 e 2 mandam).

## 4) Formato de saída
TSV (separado por TAB), uma linha por termo, **na mesma ordem da entrada**, com cabeçalho:
```
Key<TAB>ptBR
```
Apenas duas colunas: a `Key` original (idêntica) e a tradução. Não inclua English/Spanish/German na saída. Não use blocos de código.
