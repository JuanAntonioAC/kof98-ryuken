# PROGRESS

Fecha de esta nota: 2026-11-09.

## Decisión de la fase 0

Se puede leer y dibujar el banco de sprites de un personaje que ya existe. No hay, con lo comprobado, un hueco libre ni un archivo de movimientos donde registrar a Ryu y Ken como dos luchadores nuevos.

El juego no se ha modificado. Tampoco se ha abierto en esta fase, así que no hay prueba de combate.

## Instalación verificada

- Juego: THE KING OF FIGHTERS '98 ULTIMATE MATCH FINAL EDITION.
- App ID 222420. Build de Steam `8170224`.
- Carpeta: `D:\SteamLibrary\steamapps\common\The King of Fighters'98 Ultimate Match`.
- Ejecutable de trabajo: `KingOfFighters98UM_x64.exe`.
  - Tamaño 6 078 464.
  - SHA-256 `575BC3343824A7DBFD9D10C87D4FB58906926C8D512E329D8A3E2BD0447D4F6A`.
  - Marca de tiempo del PE: 2022-02-08 21:09:07 -06:00.
  - Fecha del archivo: 2026-10-11 13:41:43.
  - Image base `0x140000000`.
  - Ruta de PDB dentro del binario: `P:\Projects\FOCAL\KingOfFighters98UM\Win32\x64\BP_FOCAL_RELEASE\KingOfFighters98UM.pdb`.
- También está `KingOfFighters98UM.exe` (x86), SHA-256 `2B7F65521FDE299DCC84E4C667CE542FA1F7726588DDF6088B5A3173D15FE3B8`. El mod de colores existente engancha la versión x64. Esta investigación sigue por ahí.

## Qué carga el ejecutable

Cadenas leídas en `KingOfFighters98UM_x64.exe`:

- `OBJ\P%02X.GRC`
- `OBJ2\P%02X.GRC`
- `OBJ3\P%02X.BIN`
- `MIDDLE\CHAR%02X.BIN`
- `VIC\CHAR%04X.BIN`
- `VIC\BACK%02X.BIN`
- `FIGHT\COMMAND.BIN`
- `DEF.PAL`
- `ARRANGE.GRC`
- `NEOGEO.GRC`

No aparece `GRC-N`. Los archivos `.grc-n` están en disco, pero esta cadena no los nombra.

También hay textos `SystemAdvanced`, `SystemExtra` y `SystemUltimate`. El ejecutable conoce los tres sistemas. Eso no dice que un personaje nuevo los herede solo con existir.

Hay etiquetas `NEOGEO ROM/...` (`KOF96GEES`, `ARF2EIJI`, `KASUMI` y otras). Son texto dentro del exe. En la carpeta del juego no hay una carpeta con esas ROMs sueltas.

## Bancos que existen

| Carpeta | Archivos | Índices |
|---|---|---|
| `Data\game\obj\*.grc` | 52 | `00`–`32` y `34`. Falta `p33.grc`. |
| `Data\game\obj2\*.grc` | 53 | `00`–`34`, incluido `p33.grc`. |
| `Data\game\obj2\*.grc-n` | 44 | Subconjunto, no referenciado por la cadena del exe. |
| `Data\game\obj3\*.bin` | 53 | `00`–`34`. No existen `35`–`3F`. |
| `Data\game\middle\char*.bin` | 64 | `00`–`3F`, todos de 851 312 bytes. 52 hashes distintos. |
| `Data\game\vic\char*.bin` | 512 | 64 índices por 8 variantes. |

Retratos idénticos, medidos por MD5:

- `00`=`30`, `03`=`31`, `04`=`32`, `05`=`33`, `06`=`34`, `07`=`35`
- `08`=`36`, `10`=`37`, `11`=`3D`, `18`=`3E`, `19`=`2F`, `27`=`3F`

El mod de colores de `D:\KOF98-Color-Select` nombra los IDs 0 a 50 y arma la ruta `Data\game\obj\pXX.grc` con ese ID. Esa tabla no explica los duplicados: el archivo del índice `30` es una copia del `00`, no un retrato distinto. No asigno nombres de versiones EX a esos índices hasta verlos en la pantalla de selección.

## Formato de sprites, comprobado

`tools/GrcInspect` usa el mismo descompresor que ya funciona en el mod de colores. Resultados de esta ejecución:

| Archivo | Decodificado | Celdas | Primera celda |
|---|---|---|---|
| `obj\p00.grc` | 1 569 984 | 694 | tipo 6, 5×7 tiles |
| `obj2\p00.grc` | 1 581 120 | 704 | tipo 6, 5×7 tiles |
| `obj\p06.grc` | 1 410 304 | 595 | tipo 6, 5×7 tiles |
| `obj2\p06.grc` | 1 412 096 | 595 | mismo directorio inicial que `obj\p06.grc` |
| `obj2\p33.grc` | 591 104 | 1143 | muchas celdas de 1×2 tiles |
| `obj\p34.grc` | 2 632 000 | 2108 | tipo 6, 4×7 tiles |

Los tiles salen en bloques de 128 bytes (16×16, 4 bits por pixel). En estos archivos el resto del banco es divisible entre 128.

Una celda grande de `obj\p00.grc` (índice 71, 112×128) es Kyo: cinta, uniforme y patada alta. Una de `obj\p06.grc` (índice 59, 80×144) es Ryo: gi blanco y patada alta. La muestra está en escala de grises, sin la paleta del juego, en `work/preview/`. Confirma que el ID 0 y el ID 6 del mod de colores corresponden a esos luchadores y que el banco tiene cuadros de movimiento, no un dibujo fijo.

`obj3\p00.bin` y `obj3\p06.bin` no se decodifican con este compresor (`p00` se corta a los 12 bytes). El exe los pide, pero su contenido sigue sin identificar. `fight\command.bin` tampoco es un banco `.grc`.

`def.pal` mide 96 256 bytes. `arrange.grc` y `neogeo.grc` miden unos 4 MB cada uno. El mod de colores ya lee paletas de 16 bits (5-5-5) desde `def.pal` y `arrange.grc`. No las reabrí en esta pasada.

## Herramientas externas

- [King of Moves: Restoration](https://brigon.dev/projects/king-of-moves-restoration/) restaura presentación de la versión de PS2: escenarios, Color Edit, galerías, lista de movimientos y un visor de hitboxes. El propio proyecto dice que no cambia el combate. No registra luchadores nuevos. No se instala en esta fase.
- [kof-combo-hitboxes](https://github.com/odabugs/kof-combo-hitboxes) 1.0.3 es un visor externo para el exe de Steam o GOG. No edita hitboxes. La versión publicada es de 2018 y este build es posterior. No está comprobado contra este ejecutable. No se usa como editor.

## Codificador, 2026-11-09

`tools/GrcRoundtrip` decodifica un `.grc`, lo vuelve a comprimir y comprueba que el resultado decodifica al mismo byte. La compresión es la del juego: literales por debajo de `0xC0`, escape `0xC1`, copias de 2 a 63 bytes con distancia 1 a 255, y `0xC0` al final.

| Archivo | Original | Decodificado | Reescrito | Resultado |
|---|---:|---:|---:|---|
| `obj\p00.grc` | 1 021 878 | 1 569 984 | 924 745 | igual |
| `obj2\p00.grc` | 1 027 595 | 1 581 120 | 929 977 | igual |
| `obj\p06.grc` | 779 014 | 1 410 304 | 742 141 | igual |
| `obj2\p06.grc` | 780 319 | 1 412 096 | 743 422 | igual |
| `obj2\p33.grc` | 257 247 | 591 104 | 226 595 | igual |
| `obj\p34.grc` | 1 649 051 | 2 632 000 | 1 537 962 | igual |

También pasó un buffer sintético con bytes `0xC0`, `0xC1` y `0xFF`. El juego no se ha tocado: no hay copia instalada.

## Fuentes de Ryu y Ken

No se copió ninguna ROM al proyecto.

- SVC Chaos, versión de PC: `Data\rom\c1.bin` mide 67 108 864 bytes, pero no es el C ROM de la placa. Los primeros 8 bytes de `269-c1r.c1` dentro de `svc.zip` (`EE 5C F0 BB 10 BE 6D 5A`) no aparecen en ese archivo. `p1.bin` del juego de PC tampoco coincide con `269-p1.p1`.
- El inicio de `c1.bin` sí se lee como tiles de 16×16 a 4 bits por pixel, el mismo empaquetado que los `.grc` de KOF. Ahí hay una fuente y gráficos de interfaz, no a Ryu ni a Ken.
- El formato de sprite de Neo Geo (planos `bp0, bp2, bp1, bp3`, y también intercalando los primeros 8 MB con los siguientes) no produce un personaje en las zonas revisadas. Esas muestras están en `work/preview/svc/`.
- `Data\gallery\standing_ryu.png` y `standing_ken.png` miden 181×56. Los retratos de selección miden 118×204. Son imágenes fijas, no cuadros de animación, y no se van a meter en un banco.
- El set de Fightcade `svc.zip` sigue cifrado (CMC50, clave pública `0x57`, más una capa extra de SVC). No hace falta descifrarlo para ver gráficos: `svcsplus.zip` ya trae los ocho C ROM en claro (`svc-c1.bin` … `svc-c8.bin`).
- `tools/SvcAssemble` los lee del zip, intercala cada par y decodifica tiles de 16×16. Con el bit bajo a la izquierda se leen de verdad la barra de vida, los números 1 y 2 y el medidor. Las muestras están en `work/preview/svcsplus/`.
- `tools/SvcDecode` aplica el descifrado publicado del bootleg Super Plus. El programa queda como código 68000 válido: pila `0010F300`, entrada `00C00402`, 5833 instrucciones `RTS` en el orden correcto.
- La tabla de nombres está en `0xB1194`. Cada ficha es `FF`, el id en ASCII y el nombre. Ryu es el id `10`. Ken es el id `11`. Kyo es `00`, Iori `01`, Ryo `02`.
- El mismo programa tiene textos de victoria alrededor de `0x0E40C0`. No son la lista de piezas.
- El id del luchador está en `A4+$D2`. En `0x71940` se multiplica por 16 y se usa para el nombre. En `0xD95A` y `0xDCEE` se multiplica por 8 y se usa la tabla `0xA565E`. La ficha de Ryu (id `10`) está en `0xA56DE`: `1B 12 1C 11 01 00 00 27`. Esos bytes se copian a `+$18` y `+$1C` del objeto: son medidas, no el dibujo.
- Un barrido de bloques de 4×10 a 6×14 tiles en el C ROM descifrado eligió las zonas más densas. Lo que aparece son los nombres RYU, KEN, IORI, MAI y KYO escritos en tiles, y otra vez brazos y caras sueltas. No hay una pose completa ahí.
- En `0xC624` el programa copia la tabla de `0xA4720` hacia `$800100`, de 16 en 16 bytes. El campo `+4` de cada casilla es un puntero a una tira de tiles. Se dibujaron las 18 casillas cuyo puntero tiene una tira de al menos 8 tiles: techos, paredes de ladrillo, nubes y suelos. La casilla `10` (`0xA4820`, puntero `0x2A90A6`) es el edificio ya visto. Ninguna casilla de esta tabla es el sprite de un luchador.
- Super Street Fighter II Turbo: `C:\Users\enano\OneDrive\Documentos\Fightcade\emulator\fbneo\ROMs\ssf2t.zip` (11 892 505 bytes) y `C:\Users\enano\Downloads\ssf2xj.zip` (11 252 080 bytes, 23 archivos `sfx`/`sfxj`). Esos gráficos de CPS2 van cifrados y no se abrieron.

## Qué no está resuelto

- Dónde viven startup, hitboxes, daño, cancelaciones y supers. No apareció un script por personaje. Lo más probable es que estén en el exe, pero eso no quedó demostrado con un volcado de la tabla.
- Si el índice de selección puede pasar de 63. Hay 64 retratos y los bancos de sprite solo llegan a `34`.
- El papel exacto de `obj` frente a `obj2`. Kyo tiene 694 celdas en uno y 704 en el otro. Ryo tiene 595 en los dos, con directorios distintos de offset.
- Compatibilidad del visor de hitboxes de 2018 con este build.
- Git no está en el PATH y `git init` no se pudo ejecutar. La carpeta del proyecto existe igual.

## Siguiente paso verificable

La tabla de `0xA4720` es fondo. Los píxeles del luchador sí están en la ROM C, en una banda aproximada de los tiles `74000` a `110000`: cabezas, brazos, gi y piernas sueltos. Apilar esos tiles en orden no forma una pose.

El copiado a la VRAM está en `0x5FA0`. Cada columna tiene 16 celdas. Cada celda es una palabra de tile, escrita tal cual, y una palabra de atributo a la que se suma el byte alto de `+$30` (ajuste de paleta). El número de columnas sale de `+$78`, con tope 32.

El selector de cuadro está en `0x6822`. Lee un byte del flujo en `+$80`, lo multiplica por 1024 y lo suma al banco que apunta `+$84`. Un hueco de cuadro mide 1024 bytes: 8 columnas de 16 pares tile/atributo.

Ese banco no está en las tablas ya revisadas. `0xA565E` son medidas. `0x262538`, indexada por id por 32, es un mapa repetido `xx 00 01 A8`. `0xB1440` devuelve el id, no píxeles. El número de tile de esas piezas (alrededor de `74912`) no aparece en la ROM de CPU ni como palabra de 16 bits con su banco ni como constante de 32 bits.

Quien escribe `+$84` ya está localizado. En `0x6C66` (y en cinco copias seguidas) el dibujo hace `move.l $C2(a4), $84(a4)` justo antes de usar el cuadro. Las únicas constantes que entran directo a `+$84` son `0x10A100` y `0x10A380`: los dos bloques de jugador en RAM, a `0x280` bytes uno del otro, el mismo paso que la vida y la barra. No son un banco de tiles en la ROM. `+$B6` recibe esas mismas dos direcciones al crear el objeto, y `0x2B7A2` copia `+$B6` de ese objeto otra vez hacia `+$84`.

`+$C2` también guarda la rutina siguiente del objeto: hay más de cien `move.l #dirección, $C2(a4)` cuyo destino es código unos bytes más adelante. En la ficha del id `0x10` el primer long, que es lo que se copia a `+$C2`, vale cero. El banco de tiles del luchador no sale de ahí.

El valor de `+$C2` en ese dibujo sale del registro `A2` al crear el objeto (`0x6C04`, `0x6FB0`, `0x70AAE`). Quien llama pone en `A2` direcciones como `0x2B2FA4`, `0x2B33CC`, `0x2B37F4`, `0x2B3C1C` y `0x2B4044`. El bucle de `0x7168A` saca el puntero de la tabla que empieza en `0x716B4`: `0x29C740`, `0x29B538`, `0x29EB50`. Esas direcciones caen 8 bytes después de las cabeceras de escenario ya vistas. Dibujadas, son la luna, las nubes y el fondo, no una persona.

Ese objeto es el fondo. El luchador no pasa por aquí. Hasta entonces no se toca `obj\p06.grc`.

Otras dos tablas salen del id por 8. `0x6A218` tiene 4 fichas; la rutina devuelve la primera palabra (`00E5`, `00E8`, `00E6`). `0x70986` tiene 12 fichas; las dos primeras palabras se copian a `+$18` y `+$1C`, por ejemplo 104 y -2, o 40 y 40. El id `10` cae fuera de las dos. No son la lista de piezas del cuerpo.

Las llamadas directas a `0x5F22` están todas entre `0x74B36` y `0x76120`: son los montajes de escenario. No hay `JSR` ni `BSR` a `0x5FA0`, `0x6822` ni `0x6BC0`. Las comparaciones con `0x25`, `0x26`, `0x27` y `0x38` tampoco abren esa lista: `0x15A26` es una cadena de casos (incluye ids por encima de `0x25`), `0x45052` recorta un valor a `0x26` y lo guarda en `+$D6`, y `0x6F194` compara el cuadro en `+$70` con `0x27`.

El caso del id `10` sí existe, en `0xFB3A0`. Carga `A2` con `0xFD33E` y llama a `0xFB9C8`. Esa ficha son cuatro palabras: `001B`, `0012`, `001E`, `0011`. Ken, ocho bytes después, es `001A`, `0013`, `0010`, `0001`. Cada palabra se pasa a una rutina distinta (`0xFBD6E`, `0xFBDA2`, `0xFBDD6`, `0xFBE0A`). Esas rutinas no guardan el número ni lo usan como índice: miran un byte en `0x105000`–`0x105003` y, la primera vez, levantan una bandera en `0x105015`–`0x105018`. El valor vuelve en `D0` y el llamador lo tira. No es el banco de tiles.

Detrás de esa ficha, en `0xFD3EE`, hay un `RTS` y después bytes `FF`. No continúa un banco. En `0xFA7E8` hay 42 punteros. El primero (`0x202B6C`) está a cero. El índice `10` (`0x25AE7C`) repite el mapa `xx 00 01 A8` del escenario.

En `obj3`, Kyo tiene 96 anclas de 6 bytes y Ryo 75. La x va de -80 a 0 y la y de -233 a 0. De las 96 de Kyo, 92 tienen el tercer campo por debajo de 694, que es su número de celdas. Las otras cuatro valen 8467. El cuarto bloque, en `0x5B67`, repite `E0 00 00 00 00 00 00 00`.

Ese tercer campo es el índice de celda en `obj\p00.grc`. La celda 0 mide 80×112, tipo 6, 24 tiles, y su ancla es x=-41, y=-112: el dibujo apoya los pies en el origen. Es Kyo de pie, en guardia. Las celdas 2 y 8 miden lo mismo y son el mismo idle con el peso un poco cambiado: 23 y 26 tiles. Las diez primeras anclas repiten esa x,y con las celdas 0 a 9. La celda 416 mide 64×64 y es un golpe. La 614 mide 64×32 y es un antebrazo, con y=-25. En estas celdas el número de tile no pasa de 3694, dentro de los 12 bits del lector. El banco completo tiene 11558 tiles de 128 bytes. Una pose en este juego ya es una celda, no una pila de tiles sueltos.

De las 694 celdas de Kyo, 477 miden al menos 6 tiles de alto y 444 además miden al menos 4 de ancho. Ryo tiene 595 celdas: 402 altas y 369 de cuerpo. En `obj3` hay 4 rachas de Kyo y 3 de Ryo en las que el ancla no se mueve y el índice sube de uno en uno. La primera tabla de desplazamientos mide 512 entradas en los dos. La segunda, en Kyo, mide 694, una por celda, de `0xAD8` a `0x495E`. Entre esos punteros, 519 huecos miden 6 bytes y 23 de ellos son `FE 00` tres veces. La celda 0 ocupa 82 bytes: abre con `1F 21 05 20`, pone `FF 00`, rellena con `FE 00` y luego trae bloques `FD`. Uno de esos bloques, leído byte a byte con signo, da 104, 0, -20, 32, 20. El byte `FD` solo aparece en 14 celdas. La celda 398, de 38 bytes, trae palabras que suben de 6 en 6 (`31BE`, `31C4`, `31CA`). En Ryo las primeras anclas están en x=-52; la de la celda 0 tiene y=-112, igual que su alto, y el orden de celdas es 3, 2, 1, 0, 1, 2.

`0x1B * 4096` y `0x1A * 4096` no abren un cuerpo. Ventanas de 5×7, el tamaño de la guardia de Kyo, salen casi todas con el mismo puntaje. El mejor recorte sigue en piezas sueltas: diadema, brazo y gi, sin una persona de pie.

Las celdas de Kyo que contienen el byte `FD` son 0, 1, 23, 24, 437, 467, 483, 573, 616, 621, 623, 626, 683 y 693. La 0 es la guardia, así que `FD` no marca un super. La 573 es una patada en el aire, de 64×112. La 616 mide 128×64 y son varias cabezas en fila. La 693 mide 32×32 y es un tajo diagonal. Un super de Kyo, si está en este banco, es otra celda más, no un sistema aparte. Ryu y Ken no tienen esas celdas. `seq.bin` se parte en `0x10`, `0x20010`, `0x34010` y `0x74010`. En el último tramo, 3578 de las primeras 4000 palabras de 16 bits caen entre -300 y 300. El exe no nombra `seq.bin`: el texto `SEQ` está dentro de la frase «SEQUER». `OBJ3\P%02X.BIN` sí está, al lado de los retratos. `SKILL` en el exe es la etiqueta «SKILL LIST» del menú de pausa.

En la banda de tiles `74000`–`110000` hay 23 tiras de al menos 6 tiles cuyo borde inferior continúa en el siguiente. La más larga mide 10 tiles y empieza en `108231`. Dibujada en cuatro columnas, se ven una cabeza, un hombro y una pierna uno debajo del otro, no un luchador de pie. La de `82461`, de 7 tiles, son piezas sueltas. Un cuerpo entero no está guardado como una sola tira. En el `.grc`, la celda que ya dibujó a Kyo lleva tipo, ancho, alto, una máscara por columna y 3 bytes por tile usado; el lector toma 12 bits como número de tile y los apila por columnas.

`obj3\pXX.bin` sí lo pide el exe, uno por luchador. Kyo (`p00`) mide 453 312 bytes y Ryo (`p06`) 252 800. Los cuatro primeros enteros son desplazamientos. En el segundo bloque de Kyo hay fichas de 6 bytes: para los diez primeros, x = -41, y = -112, y un índice 0, 1, 2… Luego las coordenadas cambian. Es un ancla por cuadro, no el daño ni el startup. `seq\seq.bin` es un solo archivo de 540 688 bytes, no uno por luchador. Daño, startup, cancelaciones y supers siguen sin una tabla localizada en el exe.

Hasta tener ese cuadro no se sustituye `obj\p06.grc`. Si se sustituye, será con copia y hash, sobre el slot de Ryo, y seguirá peleando como Ryo.
