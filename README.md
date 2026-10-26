# LyA2_ColinScript

Repositorio para avances de la programacion del lenguaje ColinScript.

## Analizadores e interfaz

`Sintaxis/ColinScript.sln` contiene la aplicación Windows Forms para .NET
Framework 4.8. Se conservan los analizadores léxico, sintáctico y semántico,
la matriz `Matriz.csv` y la conexión existente a SQL Server `Automatas2`
(`localhost\SQLEXPRESS`, tabla `Matriz`).

El botón **Generar Codigo Intermedio** muestra las prefijas de asignaciones y
las pestañas **Tripletas**, **Cuádruplos** y **Tres direcciones**. Una entrada
que no puede traducirse deja las tablas vacías y muestra el diagnóstico en
Tres direcciones. El texto fuente no se modifica y cada generación reinicia
los contadores de temporales y etiquetas.

## Diagnóstico de la implementación anterior

La gramática del formulario reconoce declaraciones y asignaciones, expresiones,
comparaciones, `If`, `For`, `While`, `Do While`, `In` y `Out`. El generador
intermedio anterior solo seleccionaba líneas que comenzaban con `_` o `Int`.
No representaba control de flujo ni entrada/salida. El conversor infijo no
procesaba de forma general paréntesis o decimales, y las tripletas compartían
un solo temporal por expresión, perdiendo subexpresiones independientes.

## Representación común

`GeneradorIntermedio.cs` traduce el programa completo con un parser de bloques
y expresiones por precedencia, independiente de SQL y Windows Forms. Este parser
es para traducción; los analizadores existentes siguen siendo responsables de
validar la gramática, las declaraciones y los tipos. No se modifican sus reglas.

`ProgramaIntermedio.Operaciones` es la secuencia canónica de operaciones, con
los campos de `Cuadruplo`. De ella se obtienen las tres representaciones, sin
volver a recorrer ni traducir el programa fuente para cada vista.

Se traducen:

- Declaraciones `Int`, `Dec`, `String`, `Bool` con inicialización y asignaciones.
- Aritmética `+`, `-`, `*`, `/`, paréntesis, signo unario y decimales.
- Comparaciones `<`, `>`, `<=`, `>=`, `==`, `!=` y operaciones lógicas.
- `If`, `For` con incremento/decremento, `While`, `Do While` y bloques anidados.
- `In`, `Out` con expresiones o cadenas delimitadas por `$`; comentarios `#`.

Cada operación que produce un valor obtiene un temporal único `T1`, `T2`, etc.
Las etiquetas son `L1`, `L2`, etc. Una condición de ciclo se calcula después de
su etiqueta de inicio, de modo que vuelve a evaluarse en cada iteración.
El incremento de `For` aparece una sola vez, después del cuerpo; `Do While`
ejecuta el cuerpo antes de comprobar la condición.

| Operación | Argumento 1 | Argumento 2 | Resultado |
| --- | --- | --- | --- |
| DECL | tipo | | variable |
| = | valor | | variable |
| operador aritmético/comparación | izquierdo | derecho | temporal |
| NEG / ! | operando | | temporal |
| LABEL | | | etiqueta |
| GOTO | | | etiqueta |
| IF_FALSE | condición | | etiqueta |
| IN | | | variable |
| OUT | valor | | |

Las tripletas tienen `(Operador, Argumento 1, Argumento 2)`, con el número de
fila visible en el margen. Los resultados intermedios y destinos de salto
se referencian como `[fila]`, empezando en cero. La asignación usa
`(=, valor, destino)`, la declaración `(DECL, tipo, variable)`, la entrada
`(IN, variable, vacío)` y la etiqueta `(LABEL, nombre, vacío)`.
Cada fila corresponde a exactamente una operación de la secuencia común.

`GeneradorCuadruplos.GenerarDesdePrefija` conserva su API anterior por
compatibilidad; el formulario usa exclusivamente `GeneradorIntermedio`.
Las prefijas del formulario proceden del mismo árbol de expresiones que
la representación común y conservan el texto original de la asignación.

## Compilación y pruebas

Con Visual Studio (herramientas .NET Framework 4.8) instalado, desde la raíz:

```powershell
& ([scriptblock]::Create((Get-Content Sintaxis/Tests/Run-Tests.ps1 -Raw))) -RepoRoot (Get-Location).Path
```

El script localiza MSBuild y el compilador C#, compila la solución y ejecuta
`CuadruplosTests.cs` e `IntermedioTests.cs`. Usa `bin/Intermedio` y
`obj/Intermedio` para no sobrescribir los binarios de desarrollo existentes.
Las pruebas cargan los ensamblados en el proceso de PowerShell.

Las comprobaciones incluyen la API anterior, regeneración y limpieza del
formulario, aritmética, precedencia, subexpresiones independientes, condiciones
verdaderas/falsas, ciclos de cero y varias iteraciones, decrementos, ciclos
anidados, entrada/salida y errores estructurales. Un intérprete de pruebas ejecuta
los cuádruplos y una reconstrucción independiente de las tripletas para comparar
las salidas esperadas; también verifica correspondencia y estabilidad de tres
direcciones, temporales únicos y referencias a etiquetas.

Se utilizan los ejemplos de código intermedio, operaciones aritméticas,
condicionales y tablas del repositorio, además de
`Sintaxis/Tests/Ejemplos/ControlCombinado.txt`.

## Límites conservados

- El análisis semántico debe ejecutarse con las herramientas existentes: el
  traductor no comprueba tipos ni variables sin declarar. Las pruebas de
  generación no requieren SQL; no verifican conectividad con el servidor.
- No se agregan construcciones como `Else`, `Break` o `Continue`. `For` conserva
  la forma de la gramática existente: inicialización, condición y variable
  seguida de `+ +` o `- -`. Las operaciones lógicas se emiten como operaciones
  de valor, sin implementar cortocircuito.
- La salida es una representación intermedia, no un nuevo ejecutor de programas.
  Las pruebas numéricas usan un intérprete decimal auxiliar, no sustituyen la
  semántica de tipos del lenguaje ni la ejecución original con Roslyn.
- El ensamblador TASM y la ejecución mediante C#/Roslyn conservan su código y
  sus limitaciones anteriores; no se amplían para consumir la nueva IR.
- `Ejemplo Sintaxis.txt` tiene bloques sin cerrar y se rechaza al traducirlo.
  `Ejemplo Semantico Errores(If).txt` contiene errores deliberados y corresponde
  a los analizadores existentes detectarlos. `Tablas(While).txt` inicia `_It`
  en 1 con condición `_It > 11`, por lo que su cuerpo no se ejecuta.

## Investigación de `Ejemplo Sintaxis.txt`

El original se conserva byte por byte en
`Sintaxis/Tests/Ejemplos/EjemploSintaxis.original.txt`.
La reproducción anterior devuelve `Se esperaba variable; token 58: End`.
No falla el encabezado del `For` ni la asociación de `Do` con su `While`:

| Apertura | Cierre del original |
| --- | --- |
| `Start {`, línea 1 | pendiente |
| `If`, `{` línea 7 | `}` línea 9 |
| `For`, `{` línea 11 | pendiente |
| bloque independiente, `{` línea 13 | `}` de `} End`, línea 19 |
| `Do`, `{` línea 15 | `}` línea 17; condición `While`, línea 18 |

La ruta del fallo es `Generar → Bloque(Start) → Instruccion(For) →
Cuerpo/Bloque(For) → Instruccion → Asignacion → Identificador`.
Después de cerrar el bloque independiente, el parser sigue dentro del `For`:
`End` cae en el caso genérico de asignación y se intenta leer como variable.

`lblErrores` del formulario cuenta filas de `dgvErroresLexicos`, no errores
globales de sintaxis. El sintáctico original recorre `richtxt_token.Lines` y
reduce cada línea por separado. Sus reglas aceptan el encabezado `FOR`, `DO`,
`WHILE`, las llaves individuales y `END = CAE} PARE09`; no hay una regla que
cierre implícitamente todos los bloques con `End`. La validación global JELU
está en el analizador semántico y solo incluye bloques `If`, `While` y `For`,
sin una producción completa para `Do While` o bloques independientes.
Por tanto, cero errores léxicos o aceptación línea por línea no establece una
semántica alternativa para las llaves faltantes.

El generador ahora conserva una pila de bloques con contexto y línea de
apertura. Ante `End` prematuro informa exactamente los bloques pendientes
(`For`, línea 11, y `Start`, línea 1); no ignora `End`, no emite una traducción
parcial y mantiene las validaciones de cuerpos, delimitadores y fin de entrada.

`Sintaxis/Tests/Ejemplos/EjemploSintaxis.cerrado.txt` contiene la reparación
mínima: dos `}` adicionales antes de la última línea, sin cambiar ninguna
instrucción ni regla del lenguaje. Produce 27 operaciones en cada vista.
La regresión comprueba las tres vistas en Windows Forms, el texto de tres
direcciones, referencias de tripletas y saltos a las etiquetas correctas.

Aun cerrando los bloques, el `Do While` original es infinito: `_It` vale cero,
su cuerpo solo imprime `Mundo`, y el incremento del `For` está después de ese
ciclo. Las pruebas ejecutan cuádruplos y tripletas con un límite de pasos y
verifican ese comportamiento: con entrada 2, imprimen 10, Hola y luego Mundo
repetidamente. No se mueve el incremento dentro del `Do` para forzar su fin.
