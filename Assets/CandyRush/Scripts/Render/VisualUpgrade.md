# Acabamento dos personagens e karts

Pipeline detectado: **Built-in**, Unity **6000.6.2f1**. O projeto tem build
Android; os padrões consideram **celular Android**, com uma opção mais cara
para PC. Não foi feita migração para URP/HDRP.

## Uso

A integração acontece em `KartView.ApplyCharacter`: Nuvem, Amora, Caramelo
e Violeta recebem `KartVisuals` e `FurController`, inclusive ao trocar de
personagem. A cena continua sendo construída em código pelo `GameManager`.
Os componentes e parâmetros aparecem no Inspector durante o Play.

O pacote oficial `com.unity.postprocessing` **3.5.4** foi acrescentado ao
manifest. Aguarde o Package Manager concluir a instalação. O Editor cria os
assets ausentes automaticamente; também é possível usar
**CandyRush > Visuals > Create missing assets**. Os perfis persistentes,
materiais de referência, texturas e cubemap ficam em
`Assets/CandyRush/Resources/Candy/Visuals`. A geração preserva assets existentes.
Os materiais do kart são instâncias privadas; ajuste os parâmetros do
`KartVisuals` para mudar o acabamento aplicado na corrida. Os três `.mat`
servem como referências para atribuição manual a modelos futuros e garantem
a inclusão do Standard no build.

## Pelo

As cores do pelo seguem os retratos da seleção, separadas da pintura do kart:

| Personagem | Pelo | Cor sRGB |
| --- | --- | --- |
| Nuvem | Branco frio | `#F1F2F4` |
| Amora | Bege quente | `#C69D72` |
| Caramelo | Marrom | `#765239` |
| Violeta | Cinza suave | `#B8B4BF` |

`KartPalette` define as cores do corpo e das marcações claras (focinho/barriga).
`KartView` as aplica antes de inicializar `FurController`, nos dois modos.
No Inspector, `coatColor` e `markingsColor` permitem ajustar esses tons;
`baseColor` continua como multiplicador opcional. O interior das orelhas,
olhos, nariz, capacete e kart mantêm seus materiais próprios.

A luz de contorno do Shell Fur usa intensidade padrão 0,08, limitada a 0,25
e tingida pela cor do pelo. O UV da máscara é passado como um campo próprio
para evitar a declaração duplicada de `_FurMask_ST` pelo gerador Surface Shader.
No Editor, uma falha no Shell Fur mantém o pelo leve colorido como alternativa.

| Configuração | Resultado |
| --- | --- |
| Automatic, Android/iOS | Pelo leve, independentemente da qualidade |
| Automatic, PC Very Low / Low / Medium | Pelo leve |
| Automatic, PC High / Very High / Ultra | Shell Fur, 12 camadas por padrão |
| ShellFur / Lightweight | Força o modo para comparação |

`allowShellOnMobile` permite Shell automático no celular em qualidade alta.
Comece com 8 camadas no teste; 12 é o padrão de PC e 16 serve para close-ups.
O controlador observa `QualitySettings.SetQualityLevel` durante a execução.
Ao alterar campos por código, chame `Refresh()`; mudanças pelo Inspector
também atualizam o efeito.

`ShellFur.shader` contém o HLSL do deslocamento e iluminação, com geração
de passes Surface Shader do Built-in. `FurController` constrói uma malha
com 8–16 cópias **apenas dos submeshes selecionados**, gravando o índice
da camada no UV3. O shader infla pela normal e curva a ponta com
`furDirection` em espaço mundial. `furLength` usa unidades locais do modelo;
com escala 1,2, o padrão 0,018 corresponde a aproximadamente 2,2 cm no mundo.
O número de camadas do material é sincronizado pelo controlador; alterar
somente o slider do material não cria geometria.

Ruído R define os fios e afina as pontas; G reduz a cobertura das camadas
externas. A opacidade é aproximada por recorte/dithering de cobertura com
escrita de profundidade. Isso evita a ordenação frágil de várias superfícies
transparentes. A base permanece opaca e projeta sombras; as camadas recebem
sombras, mas não projetam sombras extras. Rim light suave usa emissão.
O efeito adiciona geometria e overdraw: avalie a corrida com todos os rivais
e a tela dividida, além do personagem isolado.

Por padrão, somente slots `*_Fur` e `Belly` recebem pelo. Olhos, nariz,
interior das orelhas e outras peças ficam fora das camadas. Para um modelo
com atlas único, preencha `regions` com renderer/slot e uma `furMask`:
**R branco = pelo; preto = pele exposta, olhos, nariz e capacete**. A máscara
usa UV0, deve ter sRGB desligado e transições suaves. Não pinte uma máscara
genérica para os GLBs atuais: eles já separam essas regiões por submesh.

`FurLite` usa textura de fibras, normal map e Smoothness 0,12. A máscara
`_SmoothMask` eleva o Smoothness a 0,55 somente nas regiões brancas.
Nos GLBs atuais, interior das orelhas e nariz são materiais separados e
recebem uma máscara branca integral; os slots de pelo recebem máscara preta.
Para um atlas futuro, forneça uma máscara pintada que reúna essas duas áreas.
Importe normal maps como **Normal map**, máscaras/ruído com **sRGB desligado**.
As quatro texturas técnicas de 128×128 são geradas por
`Tools/generate_visual_textures.py` e repetem sem costuras visíveis.

As malhas precisam de **Read/Write** habilitado, UV0 e normais suaves.
Os GLBs atuais atendem esses requisitos. MeshRenderers rígidos acompanham
as animações das peças; SkinnedMeshRenderer usa os mesmos ossos e pesos
clássicos de até quatro influências. Blend shapes não são copiados para
as camadas; para um personagem que os use, estenda a cópia dos shapes e
a sincronização dos pesos antes de ativar Shell Fur nele.

## Kart

`KartVisuals` mantém a física existente como fonte do giro (radianos),
esterço (radianos) e compressão (metros). O giro continua proporcional ao
deslocamento calculado pela física; o esterço visual é suavizado.
Tilt e mola de aterrissagem afetam somente a raiz visual do modelo,
sem mover colisores ou rodas junto com a carroceria.
O pouso é detectado pela transição ar → solo e pela velocidade vertical
anterior; mudanças de compressão respondem às lombadas. A mola integra
em passos pequenos e limita o deslocamento. Pausa congela a animação.

Para outros controladores, a sobrecarga
`Animate(speed, steering, compression, grounded, verticalSpeed, dt)` aceita
velocidade em m/s com sinal e volante de -1 a 1. Ela calcula o giro pelo
raio da roda; `maximumSteer` é usado por esse adaptador. Configure os pivôs,
chame `CaptureRestPose()` e invoque `Animate` uma vez por frame.
Os eixos esperados são X para giro e Y para esterço; ajuste os pivôs no Blender.

| Peça | Metallic | Smoothness padrão |
| --- | --- | --- |
| Carroceria `*_Main` | 0 | 0,88 |
| Pneu `Tire` | 0 | 0,13, com normal map |
| Aro e metal | 0,92 | 0,55 |
| Banco | 0 | 0,18 |

As cores da carroceria vêm dos materiais de cada GLB e ficam configuráveis
por `KartVisuals.bodyColor`. É uma aproximação de verniz pelo Standard PBR;
esse shader não simula uma segunda camada física de clear coat.
Um cubemap HDR pequeno fornece reflexos estáticos de céu/luz suave sem
capturar a pista seis vezes por frame. Os mipmaps fornecem reflexos difusos
aproximados. Para reflexos fiéis ao cenário no PC, substitua-o por um cubemap
convolvido/baked de Reflection Probe. Não é um reflexo dinâmico dos outros karts.

## Pós-processamento

`GameManager` cria um **PostProcessVolume global** e configura
`PostProcessLayer` em cada câmera de jogador. Máscara do Volume: Default
(layer 0). HDR ativo; o HUD em overlay fica fora dos efeitos.
Não há um componente executando o antigo `ToneMap.shader`, portanto o
ACES do perfil é aplicado uma vez.

| Efeito | PC | Celular / qualidade abaixo de High |
| --- | --- | --- |
| Bloom | intensidade 0,30; threshold 1,15; diffusion 5 | intensidade 0,18; diffusion 3; fast mode |
| Cor | ACES; exposição +0,15; saturação +4; contraste +5 | mesmo ajuste |
| Vignette | 0,12; suavidade 0,40 | mesmo ajuste |
| Antialiasing | SMAA Medium | FXAA fast |

Very Low e Low desabilitam bloom. O perfil é clonado em runtime para não
alterar os assets. Edite `DesktopPostProcessing.asset` e
`MobilePostProcessing.asset` fora do Play para persistir alterações.
Depth of Field e Motion Blur não foram ativados: em corrida, mantenha pista,
obstáculos e oponentes legíveis. SSAO pode ser adicionado ao perfil de PC
depois de medir o custo. Use o Profiler e o Frame Debugger no aparelho alvo;
não há garantia de FPS baseada apenas nesses parâmetros.

Recursos de shader/texturas do Post Processing são referenciados por uma
cópia de `PostProcessResources.asset` dentro de Resources, garantindo sua
presença no player. A criação também é verificada antes do build.

## Modelagem no Blender

- **Bevel** nas bordas da carroceria, para-choque, aros e capacete: 2–3 segmentos,
  largura proporcional à escala. Confira reflexos e silhouette antes de subdividir.
- Use **Shade Smooth / normais ajustadas**, preservando cantos duros mecânicos;
  exporte tangentes quando possível. O pelo evidencia normais quebradas e costuras.
- Dê volume ao **escapamento**, com tubo oco, borda arredondada e interior escuro.
- Modele **volante** com aro arredondado, cubo, raios e encaixe coerente com as mãos.
- Dê espessura ao **banco**, com encosto, abas laterais, almofada e costura em normal map.
- Separe **para-choque**, suportes e fixações; deixe espaço plausível para as rodas esterçarem.
- Abra espaço para as **orelhas** no capacete/banco e teste suas poses extremas,
  evitando interseções com volante, carroceria e orelhas opostas.
- Arredonde o perfil dos **pneus** e a borda dos aros; faça sulcos grandes na malha
  e detalhes pequenos no normal map. Mantenha pivôs no centro geométrico.
- Melhore o volume do **focinho, bochechas, patas e pálpebras**; Shell Fur não muda
  proporções nem recupera os volumes da ilustração de seleção.
- Faça UVs sem sobreposição nas máscaras, texel density consistente e margens
  suficientes; crie LODs para corpo, rodas e coelho. Mantenha olhos/nariz/orelhas
  internas identificáveis por material ou máscara.

## Verificação

Execute **CandyRush > Visuals > Validate visual upgrade** para conferir os
quatro personagens, modos leve/automático, malhas de 8 e 16 camadas, giro,
tilt, pausa, resposta ao pouso, Volume e compilação dos shaders após render.
O comando grava `visual-upgrade-preview.png` na pasta acima do projeto.
Depois, execute **CandyRush > Smoke Test Play Mode** para a cena completa.

Para conferir apenas o pelo sem carregar a corrida, use
**CandyRush > Visuals > Validate rabbit fur colors** fora do Play. O teste
usa uma cena de preview, um coelho por vez, render de 128×128 e verifica as
quatro paletas, os modos leve/8/16 camadas e os erros dos shaders na GPU.

Referências oficiais: [Surface Shaders Built-in](https://docs.unity3d.com/Manual/SL-SurfaceShaders.html),
[Post Processing 3.5 — início](https://docs.unity3d.com/Packages/com.unity.postprocessing@3.5/manual/Quick-start.html),
[controle por script](https://docs.unity3d.com/Packages/com.unity.postprocessing@3.5/manual/Manipulating-the-Stack.html).
