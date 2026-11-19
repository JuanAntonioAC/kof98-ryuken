# KOF '98 UMFE — Ryu y Ken

Proyecto local para investigar y construir un mod de **The King of Fighters '98 Ultimate Match Final Edition** (Steam, App ID 222420). El juego instalado no se modifica desde aquí.

El estado de la investigación está en `PROGRESS.md`.

## Herramienta

`tools/GrcInspect` solo lee. `tools/GrcRoundtrip` reescribe un `.grc` y comprueba que decodifica igual; no modifica el juego.

```bat
dotnet run --project tools/GrcInspect -- --png work/preview "D:\SteamLibrary\steamapps\common\The King of Fighters'98 Ultimate Match\Data\game\obj\p06.grc"
```

Las rutas de esta instalación están en `config/paths.json`.
