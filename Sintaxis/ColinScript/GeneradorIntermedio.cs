using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColinScript
{
    public sealed class TripletaIntermedia
    {
        public string Operador { get; internal set; }
        public string Argumento1 { get; internal set; }
        public string Argumento2 { get; internal set; }
    }

    // La secuencia es la unica fuente de las tres representaciones.
    public sealed class ProgramaIntermedio
    {
        public List<Cuadruplo> Operaciones { get; } = new List<Cuadruplo>();
        public List<KeyValuePair<string, string>> Prefijas { get; } = new List<KeyValuePair<string, string>>();

        public List<TripletaIntermedia> GenerarTripletas()
        {
            var referencias = new Dictionary<string, string>();
            var etiquetas = new Dictionary<string, string>();
            for (int i = 0; i < Operaciones.Count; i++)
                if (Operaciones[i].Operador == "LABEL") etiquetas.Add(Operaciones[i].Resultado, "[" + i + "]");
            Func<string, string> valor = s => referencias.ContainsKey(s) ? referencias[s] : s;
            var filas = new List<TripletaIntermedia>();
            for (int i = 0; i < Operaciones.Count; i++)
            {
                var c = Operaciones[i];
                string a = valor(c.Argumento1), b = valor(c.Argumento2);
                if (c.Operador == "=") b = c.Resultado;
                else if (c.Operador == "DECL") b = c.Resultado;
                else if (c.Operador == "IN") a = c.Resultado;
                else if (c.Operador == "LABEL") a = c.Resultado;
                else if (c.Operador == "GOTO") a = etiquetas[c.Resultado];
                else if (c.Operador == "IF_FALSE") b = etiquetas[c.Resultado];
                filas.Add(new TripletaIntermedia { Operador = c.Operador, Argumento1 = a, Argumento2 = b });
                if (c.Resultado.StartsWith("T", StringComparison.Ordinal)) referencias[c.Resultado] = "[" + i + "]";
            }
            return filas;
        }

        public string GenerarTresDirecciones()
        {
            return string.Join(Environment.NewLine, Operaciones.Select(c =>
            {
                switch (c.Operador)
                {
                    case "DECL": return "declare " + c.Argumento1 + " " + c.Resultado;
                    case "=": return c.Resultado + " = " + c.Argumento1;
                    case "LABEL": return c.Resultado + ":";
                    case "GOTO": return "goto " + c.Resultado;
                    case "IF_FALSE": return "ifFalse " + c.Argumento1 + " goto " + c.Resultado;
                    case "IN": return "in " + c.Resultado;
                    case "OUT": return "out " + c.Argumento1;
                    case "NEG": case "!": return c.Resultado + " = " + (c.Operador == "NEG" ? "-" : "!") + c.Argumento1;
                    default: return c.Resultado + " = " + c.Argumento1 + " " + c.Operador + " " + c.Argumento2;
                }
            }));
        }
    }

    // Parser exclusivo de traduccion; no sustituye los analizadores existentes.
    // No depende de SQL ni de Windows Forms.
    public sealed class GeneradorIntermedio
    {
        private sealed class Expresion
        {
            public string Token;
            public Expresion Izquierda, Derecha;
            public string Prefija => Izquierda == null ? Token : Token + " " + Izquierda.Prefija + (Derecha == null ? "" : " " + Derecha.Prefija);
        }
        private sealed class BloqueAbierto
        {
            public string Contexto;
            public int Linea;
        }
        private readonly List<BloqueAbierto> bloques = new List<BloqueAbierto>();
        private readonly List<string> tokens = new List<string>();
        private readonly List<int> comienzos = new List<int>();
        private readonly List<int> finales = new List<int>();
        private string fuente;
        private int posicion, temporal, etiqueta;
        private ProgramaIntermedio programa;
        private string Actual => posicion < tokens.Count ? tokens[posicion] : "<fin>";
        private static readonly Regex Lexema = new Regex(@"\G(?:\s+|\#[^\r\n]*|\$[^$]*\$|_[A-Za-z][A-Za-z0-9]*|[A-Za-z]+|\d+(?:\.\d+)?|>=|<=|==|!=|[{}();=+*/<>!&|\-])");

        public ProgramaIntermedio Generar(string codigo)
        {
            tokens.Clear(); comienzos.Clear(); finales.Clear(); bloques.Clear(); posicion = temporal = etiqueta = 0;
            programa = new ProgramaIntermedio();
            codigo = codigo ?? ""; fuente = codigo;
            for (int i = 0; i < codigo.Length;)
            {
                Match m = Lexema.Match(codigo, i);
                if (!m.Success) throw new FormatException("Caracter o cadena invalida en posicion " + i + ".");
                i += m.Length;
                if (!char.IsWhiteSpace(m.Value[0]) && m.Value[0] != '#')
                { tokens.Add(m.Value); comienzos.Add(m.Index); finales.Add(i); }
            }
            if (Aceptar("Start")) { Bloque("Start"); Exigir("End"); }
            else while (Actual != "<fin>") Instruccion();
            if (Actual != "<fin>") throw Error("Fin de programa esperado");
            return programa;
        }
        private int LineaActual()
        {
            int offset = posicion < comienzos.Count ? comienzos[posicion] : fuente.Length;
            int linea = 1;
            for (int i = 0; i < offset; i++) if (fuente[i] == '\n') linea++;
            return linea;
        }
        private FormatException Error(string texto) => new FormatException(texto + "; linea " + LineaActual() + "; token " + posicion + ": " + Actual);
        private FormatException FinDentroDeBloque()
        {
            string pendientes = string.Join(", ", bloques.AsEnumerable().Reverse()
                .Select(b => b.Contexto + " (apertura en linea " + b.Linea + ")"));
            return Error("End prematuro: faltan " + bloques.Count + " llaves de cierre '}' para " + pendientes +
                ". End termina el programa despues de cerrar sus bloques");
        }
        private bool Aceptar(string token) { if (Actual != token) return false; posicion++; return true; }
        private void Exigir(string token) { if (!Aceptar(token)) throw Error("Se esperaba " + token); }
        private string Identificador() { if (!Actual.StartsWith("_", StringComparison.Ordinal)) throw Error("Se esperaba variable"); return tokens[posicion++]; }
        private string Etiqueta() => "L" + ++etiqueta;
        private void Emitir(string op, string a = "", string b = "", string r = "")
        { programa.Operaciones.Add(new Cuadruplo { Operador = op, Argumento1 = a, Argumento2 = b, Resultado = r }); }
        private void Bloque(string contexto = "bloque independiente")
        {
            var bloque = new BloqueAbierto { Contexto = contexto, Linea = LineaActual() };
            Exigir("{");
            bloques.Add(bloque);
            try
            {
                while (Actual != "}")
                {
                    if (Actual == "End") throw FinDentroDeBloque();
                    if (Actual == "<fin>") throw Error("Bloque sin cerrar: " + contexto + " abierto en linea " + bloque.Linea);
                    Instruccion();
                }
                Exigir("}");
            }
            finally { bloques.RemoveAt(bloques.Count - 1); }
        }
        private void Cuerpo(string contexto)
        {
            if (Actual == "{") Bloque(contexto);
            else
            {
                if (Actual == "End" || Actual == "}" || Actual == "<fin>")
                    throw Error("Se esperaba el cuerpo de " + contexto);
                Instruccion();
            }
        }
        private Expresion Condicion() { Exigir("("); var e = ExpresionBinaria(); Exigir(")"); return e; }
        private void Instruccion()
        {
            if (Aceptar(";")) return;
            if (Actual == "{") { Bloque(); return; }
            if (Aceptar("If"))
            {
                var condicion = Condicion(); string fin = Etiqueta();
                Emitir("IF_FALSE", Traducir(condicion), r: fin); Cuerpo("If"); Emitir("LABEL", r: fin);
            }
            else if (Aceptar("While"))
            {
                var condicion = Condicion(); string inicio = Etiqueta(), fin = Etiqueta();
                Emitir("LABEL", r: inicio); Emitir("IF_FALSE", Traducir(condicion), r: fin);
                Cuerpo("While"); Emitir("GOTO", r: inicio); Emitir("LABEL", r: fin);
            }
            else if (Aceptar("Do"))
            {
                string inicio = Etiqueta(), fin = Etiqueta(); Emitir("LABEL", r: inicio);
                Cuerpo("Do"); Exigir("While"); var condicion = Condicion();
                Emitir("IF_FALSE", Traducir(condicion), r: fin); Emitir("GOTO", r: inicio); Emitir("LABEL", r: fin);
                Aceptar(";");
            }
            else if (Aceptar("For"))
            {
                Exigir("("); Asignacion(); Exigir(";"); var condicion = ExpresionBinaria(); Exigir(";");
                string variable = Identificador();
                string op = Actual;
                if (op != "+" && op != "-") throw Error("Se esperaba incremento o decremento");
                posicion++; Exigir(op); Exigir(")");
                string inicio = Etiqueta(), fin = Etiqueta(); Emitir("LABEL", r: inicio);
                Emitir("IF_FALSE", Traducir(condicion), r: fin); Cuerpo("For");
                string t = "T" + ++temporal; Emitir(op, variable, "1", t); Emitir("=", t, r: variable);
                Emitir("GOTO", r: inicio); Emitir("LABEL", r: fin);
            }
            else if (Aceptar("In")) Emitir("IN", r: Identificador());
            else if (Aceptar("Out")) Emitir("OUT", Traducir(ExpresionBinaria()));
            else if (Actual == "End") throw Error("End fuera del cierre de Start; se esperaba '} End'");
            else if (Actual == "}") throw Error("Llave de cierre sin bloque abierto");
            else Asignacion();
        }
        private void Asignacion()
        {
            string tipo = "";
            if (Actual == "Int" || Actual == "Dec" || Actual == "String" || Actual == "Bool") tipo = tokens[posicion++];
            int inicio = posicion;
            string variable = Identificador();
            if (tipo != "") Emitir("DECL", tipo, r: variable);
            Exigir("="); var e = ExpresionBinaria();
            programa.Prefijas.Add(new KeyValuePair<string, string>(fuente.Substring(comienzos[inicio], finales[posicion - 1] - comienzos[inicio]), "= " + variable + " " + e.Prefija));
            Emitir("=", Traducir(e), r: variable);
        }
        private static int Precedencia(string op)
        {
            switch (op) { case "|": return 1; case "&": return 2;
                case "==": case "!=": case "<": case ">": case "<=": case ">=": return 3;
                case "+": case "-": return 4; case "*": case "/": return 5; default: return 0; }
        }
        private Expresion ExpresionBinaria(int minimo = 1)
        {
            Expresion izquierda;
            if (Aceptar("(")) { izquierda = ExpresionBinaria(); Exigir(")"); }
            else if (Actual == "-" || Actual == "+" || Actual == "!")
            { string op = tokens[posicion++]; izquierda = new Expresion { Token = op == "-" ? "NEG" : op, Izquierda = ExpresionBinaria(6) }; }
            else
            {
                if (!(Actual.StartsWith("_") || Actual.StartsWith("$") || char.IsDigit(Actual[0]) || Actual == "True" || Actual == "False"))
                    throw Error("Se esperaba expresion");
                izquierda = new Expresion { Token = tokens[posicion++] };
            }
            while (Precedencia(Actual) >= minimo)
            {
                string op = tokens[posicion++];
                izquierda = new Expresion { Token = op, Izquierda = izquierda, Derecha = ExpresionBinaria(Precedencia(op) + 1) };
            }
            return izquierda;
        }
        private string Traducir(Expresion e)
        {
            if (e.Izquierda == null) return e.Token;
            string a = Traducir(e.Izquierda);
            if (e.Token == "+" && e.Derecha == null) return a;
            string b = e.Derecha == null ? "" : Traducir(e.Derecha);
            string t = "T" + ++temporal; Emitir(e.Token, a, b, t); return t;
        }
    }
}
