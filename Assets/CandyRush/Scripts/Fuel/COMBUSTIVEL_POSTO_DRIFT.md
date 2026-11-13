# Combustível, Posto Doce, drift e turbo

Pipeline: **Built-in**. Alvos: **Android/celular e PC**. Controlador existente:
`Scripts/Vehicle/Kart.cs`. A rampa é `SpeedRamp`, em `Scripts/Track/TrackFeatures.cs`.
A física continua usando o integrador do projeto; não foi substituída por WheelCollider/PhysX.

## Começar

1. Aguarde a recompilação. Use **CandyRush > Fuel > Create settings** se o asset
   `Assets/CandyRush/Resources/Candy/FuelRaceSettings.asset` ainda não existir.
2. Abra esse asset no Inspector. Nele ficam consumo por metro, custos, limites,
   duração do drift longo, carga conquistada, duração/força do turbo, controles,
   posição do posto, número de bombas e tempo de abastecimento.
3. Abra `Assets/Scenes/Main.unity` e inicie o Play. `GameManager` monta o posto
   e registra os quatro competidores. Cada raiz de kart tem `KartRaceSystems`,
   que expõe o tanque e o asset de configuração no Inspector durante o Play.
4. Aceleração manual vem ligada para o jogador principal. Na engrenagem
   de configurações, alterne aceleração automática/manual e drift automático.
   As preferências são guardadas no PlayerPrefs.

## Regras e integração

- Todos começam com 100%. Consumo padrão: **0,005 ponto percentual por metro**
  percorrido, inclusive para bots. Parado não consome; no serviço do posto não consome.
- Drift: segure enquanto vira acima de 6 m/s, com volante de pelo menos 25%.
  Soltar dá impulso curto e cobra **15%**; após **1,8 s**, sobe ao nível 2,
  dá impulso maior e cobra **20%**. Os níveis usam faíscas azuis e rosa/roxo,
  com fumaça clara de algodão-doce, nos pneus traseiros.
- O custo é reservado no início e descontado no fim. Outro gasto não pode roubar
  essa reserva. Sem os 20% necessários para prolongar, o drift termina no nível
  curto antes do limite longo. Giro fora de controle, resgate e entrada no pit
  cancelam o drift sem recompensa, sem permitir farm de carga.
- Drift curto dá **10%** da carga do turbo; longo, **20%**. A rampa continua
  carregando a mesma barra. Foi retirada a carga passiva automática por andar.
- Turbo só ativa com a barra cheia e combustível para **30%**. Uma recusa
  preserva a carga. Dura **1,85 s** por padrão. Impulsos do drift não pagam
  novamente o custo de turbo e não consomem a barra inteira.
- Abaixo de 25%, a barra pisca vermelha e há aviso sonoro. Em 0%, o teto
  horizontal cai a **30%** e drift/turbo ficam indisponíveis. A tração permanece
  disponível: é possível chegar lentamente ao posto. O impulso termina se o
  tanque chega a zero; gastar o último combustível não mantém um boost ativo.
- Só **ResetRace / PlaceOnGrid** enche o tanque e libera a parada de novo.
  Resgates/respawn não dão combustível nem uma segunda parada.

`FuelSystem`, `TurboSystem` e `DriftSystem` são estados C# serializáveis porque
o kart atual não é um MonoBehaviour. `KartRaceSystems` os liga ao Inspector.
As fronteiras de início/fim de ação chamam a transação de combustível; a física
recebe apenas o fator de teto e o grip traseiro durante o drift. Os eventos
são notificações: **não acrescente outro desconto em assinantes**, para evitar
cobrança duplicada. Eventos C#: `OnFuelChanged`, `OnFuelLow`, `OnFuelEmpty`,
`OnRefueled`, `OnFuelSpent`, `OnActionRejected`, `OnDriftStart`,
`OnDriftLevelUp`, `OnDriftEnd`, `OnTurboReady`, `OnTurboStart`, `OnTurboEnd`.
O tanque também oferece UnityEvents serializados para combustível alterado,
baixo, vazio e reabastecido.

## Controles e HUD

- Celular: joystick à esquerda; DRIFT grande à direita; TURBO acima dele.
  FREAR e ACELERAR ficam sempre disponíveis. O pedal maior fica no canto inferior
  direito; DRIFT (82 unidades) e TURBO (70 unidades) são botões redondos menores.
  A aceleração automática é opcional e não esconde o pedal.
  Cada dedo mantém captura independente até ser solto/cancelado; perder foco limpa entradas.
- PC: **Shift ou Espaço** para drift; **Ctrl ou E** para turbo. Os campos de
  bindings no asset usam o novo **Input System**. Também é possível atribuir
  `InputActionReference` próprios. O cooperativo preserva os mapas de cada jogador;
  controle usa o botão lateral existente para drift e LB para turbo.
- A barra antiga passou a ser **TURBO**. A barra **horizontal de combustível**
  fica no centro superior da tela, respeitando a área segura
  e o viewport de cada jogador. Usa o contorno claro, interior escuro e formato
  arredondado da barra de turbo, com preenchimento amarelo e o texto “Combustível”.
  Não há painel externo, ícone nem mensagens sobre o posto.
  A própria barra acompanha o tanque durante o abastecimento.
- `FuelHUD` cria um Canvas Screen Space Overlay com escala em pixels calculada
  pelo layout responsivo. Todas as imagens/textos são `raycastTarget=false` para
  não roubar os dedos dos controles IMGUI existentes. Não é necessário adicionar
  outro EventSystem nem duplicar os botões no Canvas.
- Para mudar o posicionamento, ajuste `FuelHUD.Display`: a barra usa a área
  segura do viewport, centralizada na horizontal e a 24 unidades do topo.
  O medidor tem 300×32 unidades antes da escala.

## Posto e rota

O material enviado é uma referência **2D**, não um conjunto de modelos 3D.
`PitStopVisuals` constrói o conjunto em 3D: marquise independente com luminárias,
colunas metálicas, bombas com visor e mangueira, vagas e frentistas. Uma loja de
conveniência com vidraças e porta substitui o prédio antigo; a área reservada
impede que árvores e outros elementos do cenário sejam gerados sobre o posto.
Não é uma conversão fiel da ilustração. Há espaço para substituir os meshes
por modelos feitos no Blender sem mudar a lógica.

O posto é montado perto de **3,5% do circuito**, com entrada e saída separadas.
A área de serviço começa dez metros além da borda da pista. Humanos entram
dirigindo e usam o botão de abastecer perto de uma bomba livre. A aproximação
não toma o volante. Bots sempre têm a obrigação de parar até a penúltima volta.

1. Em Play, localize `world-mirror/scene/PostoDoce`. Os filhos `Entrada_0..2`,
   `Fila_0..3`, `Bomba_N_Aproximacao/Parada/Liberacao` e `Saida_0..2` mostram
   os pontos usados. Não conte com mudanças feitas em Play para persistir.
2. Para um posto próprio, crie um prefab com `CandyPitStop`, forneça os arrays
   `entryWaypoints`, `queueWaypoints`, `exitWaypoints` e as bombas (`approach`,
   `stop`, `clear`). Arraste esse prefab para `GameManager.pitStopPrefab` fora
   do Play. Uma instância de cena também pode ser atribuída em `pitStop`.
3. O prefab é instanciado sob a raiz espelhada do projeto. Seus pontos locais
   seguem as coordenadas da pista; uma instância já colocada na cena usa suas
   posições mundiais. Não espelhe os pontos duas vezes.
4. Faça uma rota contínua, na ordem de corrida, sem cortar a linha de chegada.
   Deixe cerca de 3 m entre posições da fila e entre bombas. O ponto `clear`
   deve liberar fisicamente a bomba antes da fila seguinte avançar. Mantenha
   os pontos fora de barreiras/obstáculos de cenário e com altura compatível
   com a origem do chassi (padrão de construção: 0,55 m).
5. O trigger de entrada é um `BoxCollider` marcado como **Is Trigger**, com
   `CandyPitEntryTrigger`. **Os karts atuais não usam Rigidbody/Collider**;
   portanto, a integração padrão usa a região lógica de entrada amostrada
   no passo fixo. O trigger físico é um adaptador para uma integração futura
   com proxies/colliders; não adicione um Rigidbody dinâmico ao kart atual.
6. `CandyPitStop.Step` controla aceleração/deceleração, fila FIFO e bomba
   reservada. O kart para por 2,5 s, o tanque enche progressivamente e só então
   segue os pontos de saída. Uma segunda entrada passa normalmente, sem serviço.
7. Coloque `PitCrewAnimation` em cada bomba e atribua braço da mangueira,
   prancheta, pneu, partículas e AudioSource. Um Animator opcional pode usar
   o bool **Refueling**. O pneu é cosmético. Partículas e áudio pausam com a corrida.

Durante o controle do pit, o integrador normal fica suspenso para aquele kart;
pose e rodas usam o interpolador existente. Checkpoints continuam contando
em sequência, sem resgate indevido por estar fora da pista. Karts no pit
não colidem com os outros competidores. A liberação da bomba ocorre depois
de limpar a área, não no instante de completar o tanque.

## Bots

`BotPitStrategy` alterna perfis agressivo, equilibrado e econômico, com limiar
aleatório entre aproximadamente 20% e 40%, volta preferida e tempos diferentes
para drift/turbo. Usa o mesmo tanque e os mesmos custos dos humanos. O pedido
de parada fica guardado e a captura da rota ocorre na próxima entrada; na
penúltima volta é obrigatória, independentemente do tanque. Essa regra pressupõe
**pelo menos duas voltas**; o circuito atual tem três. Não há combustível grátis
ou redução de custo exclusiva para bots.

## Balanceamento inicial

Desconsiderando consumo por distância, um tanque permite **6 drifts curtos**
(90%, sobra 10%), **5 longos** (100%) ou **3 turbos** (90%, sobra 10%). Exemplos:

| Combinação | Gasto | Sobra para distância |
| --- | ---: | ---: |
| 2 turbos + 2 drifts curtos | 90% | 10% |
| 1 turbo + 3 drifts longos | 90% | 10% |
| 1 turbo + 4 drifts curtos | 90% | 10% |
| 2 longos + 2 curtos | 70% | 30% |

Com uma única parada, existem até **200% de orçamento total**, descontando o
combustível que ainda havia no tanque ao abastecer. Cada 1.000 m consome 5%
no ajuste inicial. Assim, planeje ações por distância, não por quantidade de curvas.
O turbo custa duas vezes um drift curto, mas entrega um impulso maior e exige
carga cheia; reserve-o para retas e saídas de curvas. Uma parada na **volta 2
de 3**, após um primeiro trecho de gasto, é um ponto de partida para testes.
Perfis agressivos podem precisar parar na volta 1 e economizar depois. O posto
físico existe em todas as voltas; a escolha da volta é uma regra de estratégia.

## Validação

- **CandyRush > Fuel > Validate gameplay**: custos, reservas, eventos,
  carga da rampa, tanque vazio, fila com quatro karts/uma bomba, serviço único,
  reset, obrigação dos bots e áreas responsivas de drift/turbo.
- **CandyRush > Visuals > Validate shop and station**: centralização e
  enquadramento da prévia dos quatro coelhos, barra horizontal com consumo,
  recuo do posto e waypoints dentro do piso para uma a quatro bombas. Executa
  também a validação de combustível acima.
- Os pedidos locais em `Temp/CandyRushFuel.request` e
  `Temp/CandyRushFuelSmoke.request` rodam no Editor existente. O segundo testa
  a cena Main em Play e volta ao modo de edição, sem fechar a Unity ou salvar
  a cena. O resultado fica em arquivos `.status.txt` na pasta Temp.
- Meça FPS no celular real, especialmente com Shell Fur, partículas e tela dividida.
  O sistema não cria corrotinas por kart, não procura objetos em cada passo e
  limita as partículas por bomba. Os testes no Editor não substituem teste no aparelho.
