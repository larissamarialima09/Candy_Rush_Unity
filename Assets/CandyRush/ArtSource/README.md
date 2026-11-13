# Balão de cupcake

`BalaoDoce.cs` foi adaptado do arquivo fornecido para gerar a arte no editor.
O jogo usa `Resources/Candy/Balloon.png`: atlas transparente com 48 sprites
de 256 × 256, organizados em 8 colunas e 6 linhas. A sequência começa no
canto inferior esquerdo (coordenadas do Unity), a 8 quadros por segundo,
com ciclo de 6 segundos.

O atlas já está gerado e recortado no importador Sprite (Multiple).
`CandyBalloonFrames` compartilha os quadros entre a interface e os
`CandyBalloonSprite` do cenário, que se orientam para cada câmera.
Os menus continuam animando durante a pausa.

Para regenerar após alterar o modelo: **CandyRush > Bake Balloon Sprite Frames**.
O gerador usa uma cena de prévia e preserva a cena aberta.
Abra `Assets/Scenes/Main.unity` e pressione Play para jogar.
