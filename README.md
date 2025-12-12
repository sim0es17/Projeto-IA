# Projeto – IA: Documentação da Inteligência Artificial em *ClashBound*

Esta documentação descreve em detalhe os sistemas de Inteligência Artificial (IA) implementados no projeto *ClashBound*, com foco no comportamento de inimigos, Pathfinding e lógica inteligente aplicada aos *power-ups*.  
Projeto desenvolvido por: **Bento Simões – 27914**, **Hugo Oliveira – 27920**, **Ricardo Costa – 27927**.

---

## 1. Máquina de Estados Finitos (FSM)

A Máquina de Estados Finitos (FSM) é uma das arquiteturas mais utilizadas na IA para jogos, devido à sua simplicidade, previsibilidade e eficiência. Em *ClashBound*, este sistema controla o comportamento dos inimigos, permitindo que alternem entre estados bem definidos e que reajam ao contexto do jogo.

### 1.1 Justificação da FSM

A escolha deste modelo garante:

- Baixo custo computacional e decisões rápidas.  
- Fluxo lógico claro e fácil de depurar.  
- Escalabilidade controlada para futuros novos estados.  
- Comportamentos fiáveis e consistentes num ambiente *multiplayer*.

### 1.2 Implementação no Código

A FSM é definida num `enum` com estados como Idle, Patrol, Chase, Attack e Stunned.  
Cada estado tem o seu método dedicado (ex.: `HandlePatrol()`), sendo chamado num `switch` dentro do método `Update()`.

As transições ocorrem quando determinadas condições são satisfeitas:

- Deteção do jogador (`CanSeePlayer()`).  
- Distância ao alvo.  
- Conclusão de animações.  
- Temporizadores controlados por corrotinas (ex.: transição Stunned → Chase).

Esta separação garante modularidade e reduz risco de conflitos entre estados.

### 1.3 FSM em Ambiente Multiplayer

Toda a IA é executada exclusivamente no **Master Client**, garantindo:

- Autoridade centralizada das decisões.  
- Sincronização precisa entre todos os jogadores.  
- Menor carga na rede ao transmitir apenas posições e estados, não cálculos.

---

## 2. Pathfinding – Algoritmo BFS

O Pathfinding é a tecnologia que permite que os inimigos naveguem por cenários com plataformas, buracos, diferentes alturas e obstáculos. Em *ClashBound*, foi usado o algoritmo **Breadth-First Search (BFS)** para encontrar o caminho mais curto em grelhas uniformes.

### 2.1 Porque foi escolhido o BFS?

- Garante caminhos óptimos em menor número de passos.  
- Funciona muito bem em grelhas uniformes, comuns em plataformas 2D.  
- É mais leve que algoritmos como A*, ideal para *multiplayer*.  
- Tem implementação simples e alta estabilidade.

### 2.2 Estrutura da Grelha

O mapa é discretizado numa grelha virtual, convertendo coordenadas do mundo (`Vector2`) em coordenadas inteiras (`Vector2Int`) através de um `cellSize`.  
Isto simplifica:

- Verificação de obstáculos.  
- Cálculo de vizinhos.  
- Redução de ruído físico.

Cada célula pode ser livre, bloqueada ou acessível mediante salto.

### 2.3 Implementação do BFS

O método principal, `RunBFS`, utiliza:

- `Queue<Vector2Int> frontier` — fila FIFO para explorar nós.  
- `Dictionary<Vector2Int, Vector2Int> cameFrom` — registo do caminho.  
- `Physics2D.OverlapCircle` — deteção de colisões em cada célula.  
- `maxSearchSteps` — limite de segurança para evitar sobrecarga.

O sistema previne ciclos, apenas adiciona vizinhos válidos e interrompe a busca se o alvo for inacessível ou demasiado distante.

### 2.4 Reconstrução e Movimento

Quando a busca termina com sucesso:

- O caminho é reconstruído de trás para a frente e invertido.  
- Cada nó é seguido sequencialmente por `MoveTowardsNode`.  
- O inimigo salta automaticamente se o próximo nó for mais alto.  
- O recalculo é periódico (`pathUpdateRate`) para equilibrar precisão e desempenho.

Este método cria uma navegação fluida e inteligente, muito superior a movimentos lineares.

---

## 3. Lógica Inteligente – *Smart Power-Up*

O sistema Smart Power-Up aplica um efeito específico baseado no estado do jogador e no contexto à sua volta. Embora não utilize uma Árvore de Decisão formal, funciona como uma mini-estrutura hierárquica de prioridades.

### 3.1 Hierarquia de Decisão

A ordem das decisões é:

1. **Cura (Heal)** — se a vida for inferior ao `lowHealthThreshold` (ex.: 30%).  
2. **Aumento de Dano (DamageBoost)** — se existirem inimigos próximos (`IsEnemyNearby()`).  
3. **Velocidade (SpeedBoost)** — se não houver perigo nem urgência de sobrevivência.

Esta abordagem melhora a experiência de jogo, oferecendo efeitos úteis conforme a situação.

### 3.2 Detalhes de Implementação

- A percentagem de vida é calculada dinamicamente.  
- A presença de inimigos é verificada com `Physics2D.OverlapCircle`.  
- Buffs são temporários, funcionando com corrotinas para aplicar, esperar e restaurar valores originais.  
- O objeto destrói-se após cumprir o seu propósito, evitando acumulação desnecessária no jogo.

---

## 4. Considerações de Desempenho

Para garantir estabilidade e fluidez, foram tomadas várias medidas:

- Execução da IA apenas no Master Client.  
- Limitação do número de passos do BFS.  
- Uso de layers específicas para colisões.  
- Métodos de movimentação simples e de baixo custo.  
- Separação entre lógica e animações.

---

## 5. Melhorias Futuras Possíveis

Há várias direções de evolução natural:

- Implementação de Behaviour Trees para comportamentos mais complexos.  
- Substituição do BFS por A* em mapas maiores.  
- Sistema de perceção auditiva.  
- Coordenação entre inimigos.  
- Smart Power-Ups adaptativos baseados no estilo de jogo do utilizador.  
- Inimigos com memória curta (última posição vista do jogador).

---

## Conclusão

A IA de *ClashBound* demonstra uma implementação sólida dos pilares fundamentais da Inteligência Artificial em jogos:

- A **FSM** estrutura e organiza o comportamento dos inimigos.  
- O **BFS** permite navegação inteligente em plataformas.  
- O **Smart Power-Up** adapta-se ao contexto para melhorar a experiência do jogador.

O resultado é um sistema fiável, eficiente e totalmente adaptado ao ambiente *multiplayer*, oferecendo uma jogabilidade mais dinâmica, equilibrada e envolvente.
