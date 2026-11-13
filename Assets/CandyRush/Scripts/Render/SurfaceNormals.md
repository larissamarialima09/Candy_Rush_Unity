# Relevo do cenário

`SurfaceNormals.cs` gera três normal maps em espaço linear, uma vez por
sessão. Os materiais compartilham as texturas de 256 × 256 com mipmaps,
filtragem trilinear e repetição contínua nas bordas.

- Grass: detalhe alongado no terreno e no horizonte.
- Biscuit: poros no biscoito do castelo, creme da fábrica e áreas de escape.
- Glaze: ondulações suaves no glacê, paredes doces e pintura da pista.

A pista mantém seu normal map de confetes existente. O shader CandyRush/Lit
agora usa UVs independentes para o normal map, permitindo ajustar o relevo
sem alterar as estampas. `SurfaceNormals.Apply` recebe material, superfície,
intensidade e repetição; os valores específicos estão em CircuitSurface,
CandyCastle e CandyFactory. O relevo afeta a iluminação, sem mudar colisões.
