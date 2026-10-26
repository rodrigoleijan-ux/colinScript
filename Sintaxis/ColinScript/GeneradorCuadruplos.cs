using System;
using System.Collections.Generic;

namespace ColinScript
{
    public sealed class Cuadruplo
    {
        public string Operador { get; set; }
        public string Argumento1 { get; set; }
        public string Argumento2 { get; set; }
        public string Resultado { get; set; }
    }

    // Una instancia por programa conserva temporales únicos entre instrucciones.
    public sealed class GeneradorCuadruplos
    {
        private int temporal;

        public List<Cuadruplo> GenerarDesdePrefija(string prefija)
        {
            if (string.IsNullOrWhiteSpace(prefija))
                throw new ArgumentException("La expresión prefija está vacía.", nameof(prefija));

            string[] tokens = prefija.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool asignacion = tokens[0] == "=";
            int inicio = asignacion ? 2 : 0;
            if (asignacion && (tokens.Length < 3 || EsOperador(tokens[1])))
                throw new ArgumentException("La asignación prefija no es válida.", nameof(prefija));

            var pila = new Stack<string>();
            var cuadruplos = new List<Cuadruplo>();
            int siguienteTemporal = temporal;

            for (int i = tokens.Length - 1; i >= inicio; i--)
            {
                string token = tokens[i];
                if (!EsOperador(token))
                {
                    pila.Push(token);
                    continue;
                }

                if (token == "=" || pila.Count < 2)
                    throw new ArgumentException("La expresión prefija no es válida.", nameof(prefija));

                string argumento1 = pila.Pop();
                string argumento2 = pila.Pop();
                string resultado = "T" + ++siguienteTemporal;
                cuadruplos.Add(new Cuadruplo
                {
                    Operador = token,
                    Argumento1 = argumento1,
                    Argumento2 = argumento2,
                    Resultado = resultado
                });
                pila.Push(resultado);
            }

            if (pila.Count != 1)
                throw new ArgumentException("La expresión prefija no es válida.", nameof(prefija));

            if (asignacion)
            {
                cuadruplos.Add(new Cuadruplo
                {
                    Operador = "=",
                    Argumento1 = pila.Pop(),
                    Argumento2 = string.Empty,
                    Resultado = tokens[1]
                });
            }

            temporal = siguienteTemporal;
            return cuadruplos;
        }

        private static bool EsOperador(string token)
        {
            return token == "+" || token == "-" || token == "*" || token == "/" || token == "=";
        }
    }
}
