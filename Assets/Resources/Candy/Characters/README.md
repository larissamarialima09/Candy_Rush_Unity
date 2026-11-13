# Personagens 3D da corrida

Modelos copiados de `Candy_Rush_Game-git/Assets/Models/RabbitKarts`, após
atualização do repositório até `ffb86db`.

O glTFast 6.14.1 importa os quatro GLBs como GameObjects. `KartView` carrega
esses recursos diretamente, sem exigir a criação manual de prefabs:

| Seleção no menu | Modelo |
| --- | --- |
| blue | Nuvem |
| pink | Amora |
| yellow | Caramelo |
| purple | Violeta |

A carroceria usa escala 1,2 e a compensação de X exigida pela raiz espelhada
do jogo. As rodas usam os raios, posições, esterço, giro e compressão da
física existente. `RabbitKartAnimation`, adaptado do `KartVisuals` recebido,
anima orelhas, cabeça, braços e corpo; o tempo da animação congela na pausa.
Os retratos 2D do menu continuam usando o atlas da interface.

Verificação no Editor: `CandyRush > Validate Rabbit Models` e
`CandyRush > Smoke Test Play Mode`. O primeiro também grava uma prévia
`character-preview.png` na pasta acima do projeto.
