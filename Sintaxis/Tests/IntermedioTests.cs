using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ColinScript;

internal static class IntermedioTests
{
    private static int casos;
    private sealed class LimiteEjecucion : Exception
    {
        public readonly List<string> Salida;
        public LimiteEjecucion(List<string> salida) : base("Ciclo inesperado") { Salida = salida; }
    }
    private static void Igual(string esperado, string actual)
    { if (esperado != actual) throw new Exception("Esperado: " + esperado + "\nActual: " + actual); }
    private static string Ejecutar(IList<Cuadruplo> operaciones, params decimal[] entrada)
    {
        var memoria = new Dictionary<string, object>();
        var etiquetas = new Dictionary<string, int>();
        var salida = new List<string>();
        var datos = new Queue<decimal>(entrada);
        for (int i = 0; i < operaciones.Count; i++)
            if (operaciones[i].Operador == "LABEL") etiquetas.Add(operaciones[i].Resultado, i);
        Func<string, object> valor = s =>
        {
            if (s.StartsWith("$")) return s.Substring(1, s.Length - 2).Replace("\\n", "\n");
            if (s == "True") return 1m;
            if (s == "False") return 0m;
            decimal n;
            if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) return n;
            return memoria[s];
        };
        Func<string, decimal> numero = s => Convert.ToDecimal(valor(s), CultureInfo.InvariantCulture);
        int pasos = 0;
        for (int pc = 0; pc < operaciones.Count; pc++)
        {
            if (++pasos > 100000) throw new LimiteEjecucion(salida);
            var c = operaciones[pc];
            switch (c.Operador)
            {
                case "DECL": memoria[c.Resultado] = 0m; break;
                case "=": memoria[c.Resultado] = valor(c.Argumento1); break;
                case "IN": memoria[c.Resultado] = datos.Dequeue(); break;
                case "OUT": salida.Add(Convert.ToString(valor(c.Argumento1), CultureInfo.InvariantCulture)); break;
                case "LABEL": break;
                case "GOTO": pc = etiquetas[c.Resultado]; break;
                case "IF_FALSE": if (numero(c.Argumento1) == 0) pc = etiquetas[c.Resultado]; break;
                default:
                    decimal a = numero(c.Argumento1), b = c.Argumento2 == "" ? 0 : numero(c.Argumento2), r;
                    switch (c.Operador)
                    {
                        case "+": r = a + b; break; case "-": r = a - b; break;
                        case "*": r = a * b; break; case "/": r = a / b; break;
                        case "NEG": r = -a; break; case "!": r = a == 0 ? 1 : 0; break;
                        case "<": r = a < b ? 1 : 0; break; case ">": r = a > b ? 1 : 0; break;
                        case "<=": r = a <= b ? 1 : 0; break; case ">=": r = a >= b ? 1 : 0; break;
                        case "==": r = a == b ? 1 : 0; break; case "!=": r = a != b ? 1 : 0; break;
                        case "&": r = a != 0 && b != 0 ? 1 : 0; break;
                        case "|": r = a != 0 || b != 0 ? 1 : 0; break;
                        default: throw new Exception("Operacion desconocida " + c.Operador);
                    }
                    memoria[c.Resultado] = r; break;
            }
        }
        if (datos.Count != 0) throw new Exception("Entrada perdida");
        return string.Join(";", salida);
    }

    // Reconstruccion independiente: resultados implicitos [fila] y destinos de salto por fila.
    private static List<Cuadruplo> DesdeTripletas(ProgramaIntermedio p)
    {
        var filas = p.GenerarTripletas();
        var ops = new List<Cuadruplo>();
        for (int i = 0; i < filas.Count; i++)
        {
            var t = filas[i]; string a = t.Argumento1, b = t.Argumento2, r = "[" + i + "]";
            switch (t.Operador)
            {
                case "=": case "DECL": r = b; b = ""; break;
                case "IN": r = a; a = ""; break;
                case "LABEL": r = "[" + i + "]"; a = ""; break;
                case "GOTO": r = a; a = ""; break;
                case "IF_FALSE": r = b; b = ""; break;
            }
            ops.Add(new Cuadruplo { Operador = t.Operador, Argumento1 = a, Argumento2 = b, Resultado = r });
        }
        return ops;
    }
    private static void Caso(string codigo, string salida, params decimal[] entrada)
    {
        var g = new GeneradorIntermedio(); var p = g.Generar(codigo);
        Igual(salida, Ejecutar(p.Operaciones, entrada));
        Igual(salida, Ejecutar(DesdeTripletas(p), entrada));
        Igual(p.Operaciones.Count.ToString(), p.GenerarTresDirecciones().Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length.ToString());
        Igual(p.GenerarTresDirecciones(), g.Generar(codigo).GenerarTresDirecciones());
        var temporales = p.Operaciones.Where(c => c.Resultado.StartsWith("T")).Select(c => c.Resultado).ToList();
        Igual(temporales.Count.ToString(), temporales.Distinct().Count().ToString());
        casos++;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Caso("Start { Int _A = (10 + 2) * (9 - 4) / 3 Out _A Out 20 / (10 - 6) Out 10 - 3 - 2 } End", "20;5;5");
            Caso("Dec _A = -2.5 * (3 + 1) Out _A Out -(2 + 3)", "-10.0;-5");
            Caso("If (1 < 2) { Out $si$ } If (2 < 1) { Out $no$ } Out $fin$", "si;fin");
            Caso("If (2 >= 2) Out 1 If (2 <= 2) Out 2 If (2 == 2) Out 3 If (2 != 2) Out 4", "1;2;3");
            Caso("Bool _B = True If (_B == True) Out $verdad$", "verdad");
            Caso("Int _I = 0 While (_I < 3) { Out _I _I = _I + 1 } Out _I", "0;1;2;3");
            Caso("Int _I = 0 While (_I < 0) { Out 9 } Do { Out _I _I = _I + 1 } While (_I < 0) Out _I", "0;1");
            Caso("Int _I = 0 Do { _I = _I + 1 Out _I } While (_I < 3)", "1;2;3");
            Caso("Int _I = 0 For (_I = 3; _I > 0; _I - -) { Out _I } Out _I", "3;2;1;0");
            Caso("Int _I = 0 For (_I = 1; _I < 0; _I + +) Out 99 Out _I", "1");
            Caso("Int _I = 0 Int _J = 0 Int _K = 0 For (_I = 0; _I < 2; _I + +) { _J = 0 While (_J < 2) { _K = 0 Do { If (_K == 0) Out _I * 10 + _J _K = _K + 1 } While (_K < 2) _J = _J + 1 } } Out _I", "0;1;10;11;2");
            Caso("Dec _A = 0 In _A Out $valor$ Out (_A + 2) * 3 # comentario\n Out $If { ; } While$", "valor;18;If { ; } While", 4);
            Caso("String _S = $hola mundo$ Out _S", "hola mundo");
            Caso("If ((1 < 2) & (2 < 3)) Out 1 If (!(1 == 1)) Out 9", "1");
            Caso("Start { For (Int _I = 0; _I < 2; _I + +) Out _I } End", "0;1");
            foreach (string invalido in new[] { "Start { Int _A = 1", "If (1 < 2) {", "Do { Out 1 }", "Out $sin cerrar", "_A = 1 +", "_A = 1 @ 2", "Start {} End Out 1", "For (_I = 0; _I < 2; _I + -) {}" })
            {
                bool rechazo = false;
                try { new GeneradorIntermedio().Generar(invalido); } catch (FormatException) { rechazo = true; }
                if (!rechazo) throw new Exception("Se acepto: " + invalido);
                casos++;
            }
            string raiz = args[0];
            Caso(File.ReadAllText(Path.Combine(raiz, "Tests/Ejemplos/ControlCombinado.txt")), "resultado;10", 3);
            Caso(File.ReadAllText(Path.Combine(raiz, "Ejemplo Semantico (If).txt")), "", 8);
            foreach (string nombre in new[] { "CodigoIntermedio.txt", "CodigoIntermedio2.txt" })
                Caso(File.ReadAllText(Path.Combine(raiz, nombre)), "");
            Caso(File.ReadAllText(Path.Combine(raiz, "Ejemplo Semantico (If).txt")), "7", 2);
            Caso(File.ReadAllText(Path.Combine(raiz, "Tablas(While).txt")), "TABLAS DE MULTIPLICAR\n;Ingresa un numero: ", 3);
            string tabla = "TABLAS DE MULTIPLICAR\n;Ingresa un numero: ";
            for (int i = 1; i <= 10; i++) tabla += ";3;x;" + i + ";=;" + (3 * i) + ";\n";
            Caso(File.ReadAllText(Path.Combine(raiz, "Tablas Multiplicar.txt")), tabla, 3);
            for (int op = 1; op <= 4; op++)
                Caso(File.ReadAllText(Path.Combine(raiz, "OperacionesAritmeticas.txt")),
                    "OPERACIONES\n;1-Suma   2-Resta   3-Multiplicacion   4-Division\n;Ingresa una opcion: ;Ingresa un numero: ;Ingresa otro numero: ;" + new[] { "", "12", "4", "32", "2" }[op], op, 8, 4);
            RegresionEjemploSintaxis(raiz);
            Interfaz(raiz);
            Console.WriteLine("OK: " + casos + " casos, cuadruplos y tripletas ejecutados; interfaz y ejemplos verificados.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void RegresionEjemploSintaxis(string raiz)
    {
        string ruta = Path.Combine(raiz, "Ejemplo Sintaxis.txt");
        string original = File.ReadAllText(Path.Combine(raiz, "Tests/Ejemplos/EjemploSintaxis.original.txt"));
        string cerrado = File.ReadAllText(Path.Combine(raiz, "Tests/Ejemplos/EjemploSintaxis.cerrado.txt"));
        var generador = new GeneradorIntermedio();
        try { generador.Generar(original); throw new Exception("Se acepto el original sin cerrar"); }
        catch (FormatException e)
        {
            Igual("End prematuro: faltan 2 llaves de cierre '}' para For (apertura en linea 11), Start (apertura en linea 1). End termina el programa despues de cerrar sus bloques; linea 19; token 58: End", e.Message);
        }
        // La fixture original es una copia byte por byte, no una version reparada al vuelo.
        string actual = File.ReadAllText(ruta);
        if (actual != original && actual != cerrado) throw new Exception("Cambio inesperado en el ejemplo de regresion");
        if (actual == original && !File.ReadAllBytes(ruta).SequenceEqual(File.ReadAllBytes(Path.Combine(raiz, "Tests/Ejemplos/EjemploSintaxis.original.txt"))))
            throw new Exception("La fixture no conserva los bytes originales");
        if (actual == original)
        {
            try { generador.Generar(actual); throw new Exception("Se acepto el archivo original"); }
            catch (FormatException e) { if (!e.Message.Contains("token 58: End")) throw; }
        }
        var programa = generador.Generar(cerrado);
        string esperado = string.Join(Environment.NewLine, new[] {
            "declare Int _NumA", "_NumA = 5", "declare Int _NumB", "_NumB = 0",
            "declare Int _It", "_It = 0", "in _NumB", "T1 = _NumA > _NumB",
            "ifFalse T1 goto L1", "T2 = _NumA * _NumB", "out T2", "L1:",
            "_It = 0", "L2:", "T3 = _It < 9", "ifFalse T3 goto L3", "out $Hola$",
            "L4:", "out $Mundo$", "T4 = _It < 9", "ifFalse T4 goto L5", "goto L4", "L5:",
            "T5 = _It + 1", "_It = T5", "goto L2", "L3:"
        });
        Igual(esperado, programa.GenerarTresDirecciones());
        Igual("27", programa.Operaciones.Count.ToString());
        Igual("27", programa.GenerarTripletas().Count.ToString());
        Igual("16", programa.Operaciones.FindIndex(c => c.Operador == "OUT" && c.Argumento1 == "$Hola$").ToString());
        Igual("18", programa.Operaciones.FindIndex(c => c.Operador == "OUT" && c.Argumento1 == "$Mundo$").ToString());
        var triples = programa.GenerarTripletas();
        Igual("[26]", triples[15].Argumento2); // Salida del For.
        Igual("[22]", triples[20].Argumento2); // Salida del Do While.
        Igual("[17]", triples[21].Argumento1); // Retorno al cuerpo del Do, no al For.
        Igual("[13]", triples[25].Argumento1); // Retorno a la condicion del For.
        foreach (var operaciones in new[] { programa.Operaciones, DesdeTripletas(programa) })
        {
            try { Ejecutar(operaciones, 2); throw new Exception("El Do While original termino inesperadamente"); }
            catch (LimiteEjecucion limite)
            {
                Igual("10", limite.Salida[0]); Igual("Hola", limite.Salida[1]);
                if (limite.Salida.Count < 4 || limite.Salida.Skip(2).Any(x => x != "Mundo"))
                    throw new Exception("Se altero el comportamiento del Do While infinito");
            }
        }
        foreach (string invalido in new[] {
            "End", "Start { End", "Start { If (1 < 2) } End", "Start { While (1 < 2) } End",
            "Start { Do { Out 1 } } End", "Start { Do { Out 1 } While (1 < 2) End",
            "Start { For (_I = 0; _I < 2; _I + -) {} } End", "Start {} End Out 1", "}"
        })
        {
            try { generador.Generar(invalido); throw new Exception("Se acepto: " + invalido); }
            catch (FormatException) { }
        }
        Igual(esperado, generador.Generar(cerrado).GenerarTresDirecciones());
        casos++;
    }
    private static void Interfaz(string raiz)
    {
        using (var f = new Form1())
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Func<string, object> campo = n => typeof(Form1).GetField(n, flags).GetValue(f);
            var codigo = (TextBox)campo("txt_codigo");
            var cuadruplos = (DataGridView)campo("dgvCuadruplos");
            var tripletas = (DataGridView)campo("dgvTripleta");
            var tac = (TextBox)campo("txtTresDirecciones");
            Action generar = () => typeof(Form1).GetMethod("btnCodigoIntermedio_Click", flags).Invoke(f, new object[] { null, EventArgs.Empty });
            codigo.Text = "Start { Int _I = 0 While (_I < 2) { Out _I _I = _I + 1 } } End";
            generar();
            Igual(new GeneradorIntermedio().Generar(codigo.Text).GenerarTresDirecciones(), tac.Text);
            Igual(cuadruplos.Rows.Count.ToString(), tripletas.Rows.Count.ToString());
            string anterior = tac.Text; generar(); Igual(anterior, tac.Text);
            codigo.Text = File.ReadAllText(Path.Combine(raiz, "Tests/Ejemplos/EjemploSintaxis.original.txt")); generar();
            Igual("0", cuadruplos.Rows.Count.ToString()); Igual("0", tripletas.Rows.Count.ToString());
            if (!tac.Text.Contains("End prematuro: faltan 2") || !tac.Text.Contains("For (apertura en linea 11)"))
                throw new Exception("El formulario no explica los bloques abiertos del original");
            codigo.Text = File.ReadAllText(Path.Combine(raiz, "Tests/Ejemplos/EjemploSintaxis.cerrado.txt")); generar();
            Igual("27", cuadruplos.Rows.Count.ToString()); Igual("27", tripletas.Rows.Count.ToString());
            var programaCerrado = new GeneradorIntermedio().Generar(codigo.Text);
            Igual(programaCerrado.GenerarTresDirecciones(), tac.Text);
            for (int fila = 0; fila < programaCerrado.Operaciones.Count; fila++)
            {
                var c = programaCerrado.Operaciones[fila]; var t = programaCerrado.GenerarTripletas()[fila];
                Igual(c.Operador, Convert.ToString(cuadruplos.Rows[fila].Cells[0].Value));
                Igual(c.Argumento1, Convert.ToString(cuadruplos.Rows[fila].Cells[1].Value));
                Igual(c.Argumento2, Convert.ToString(cuadruplos.Rows[fila].Cells[2].Value));
                Igual(c.Resultado, Convert.ToString(cuadruplos.Rows[fila].Cells[3].Value));
                Igual(t.Operador, Convert.ToString(tripletas.Rows[fila].Cells[0].Value));
                Igual(t.Argumento1, Convert.ToString(tripletas.Rows[fila].Cells[1].Value));
                Igual(t.Argumento2, Convert.ToString(tripletas.Rows[fila].Cells[2].Value));
            }
            codigo.Text = "_I = 1 +"; generar();
            Igual("0", cuadruplos.Rows.Count.ToString()); Igual("0", tripletas.Rows.Count.ToString());
            if (!tac.Text.StartsWith("No se pudo")) throw new Exception("Falta diagnostico");
            codigo.Clear(); generar(); Igual("", tac.Text);
            Igual("0", cuadruplos.Rows.Count.ToString());
        }
    }
}
