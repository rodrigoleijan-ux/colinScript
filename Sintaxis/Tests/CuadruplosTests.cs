using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ColinScript;

internal static class CuadruplosTests
{
    private static void Igual(string esperado, string actual)
    {
        if (esperado != actual)
            throw new Exception("Esperado: " + esperado + "\nActual: " + actual);
    }

    private static string Filas(IEnumerable<Cuadruplo> filas)
    {
        return string.Join(";", filas.Select(c =>
            c.Operador + "," + c.Argumento1 + "," + c.Argumento2 + "," + c.Resultado));
    }

    private static string Tabla(DataGridView tabla)
    {
        return string.Join(";", tabla.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
            .Select(r => string.Join(",", r.Cells.Cast<DataGridViewCell>()
                .Select(c => Convert.ToString(c.Value)))));
    }

    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            string ruta = Path.Combine(args[0], new AssemblyName(e.Name).Name + ".dll");
            if (!File.Exists(ruta))
                ruta = Path.ChangeExtension(ruta, ".exe");
            return File.Exists(ruta) ? Assembly.LoadFrom(ruta) : null;
        };
        try
        {
            Ejecutar();
            Console.WriteLine("OK: generación, operandos, temporales, validación y regresión de interfaz.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static void Ejecutar()
    {
        var generador = new GeneradorCuadruplos();
        Igual("=,5,,_A", Filas(generador.GenerarDesdePrefija("= _A 5")));
        Igual("=,_A,,_B", Filas(generador.GenerarDesdePrefija("= _B _A")));
        Igual("*,_Y,_Z,T1;+,_X,T1,T2;=,T2,,_W",
            Filas(generador.GenerarDesdePrefija("= _W + _X * _Y _Z")));
        Igual("-,10,3,T3;/,20,T3,T4;=,T4,,_A",
            Filas(generador.GenerarDesdePrefija("= _A / 20 - 10 3")));
        Igual("*,_C,_D,T5;+,_A,_B,T6;-,T6,T5,T7;=,T7,,_R",
            Filas(generador.GenerarDesdePrefija("= _R - + _A _B * _C _D")));

        foreach (string invalida in new[] { "", "=", "= + 1", "= _A + 1", "= _A 1 2", "+ 1 = _A 2" })
        {
            bool rechazo = false;
            try { generador.GenerarDesdePrefija(invalida); }
            catch (ArgumentException) { rechazo = true; }
            if (!rechazo) throw new Exception("Se aceptó una expresión inválida: " + invalida);
        }
        Igual("+,1,2,T8;=,T8,,_A", Filas(generador.GenerarDesdePrefija("= _A + 1 2")));

        // El formulario no requiere SQL para generar código intermedio.
        using (var formulario = new Form1())
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Func<string, object> campo = nombre => typeof(Form1).GetField(nombre, flags).GetValue(formulario);
            var codigo = (TextBox)campo("txt_codigo");
            var prefijas = (DataGridView)campo("dgvPrefijo");
            var tripletas = (DataGridView)campo("dgvTripleta");
            var cuadruplos = (DataGridView)campo("dgvCuadruplos");
            Action generar = () => typeof(Form1).GetMethod("btnCodigoIntermedio_Click", flags)
                .Invoke(formulario, new object[] { null, EventArgs.Empty });

            codigo.Text = "Start {\r\nInt _W = 0\r\n_W = _X + _Y * _Z\r\n} End";
            generar();
            Igual("Int", codigo.Lines[1].Substring(0, 3));
            Igual("_W = 0,= _W 0;_W = _X + _Y * _Z,= _W + _X * _Y _Z", Tabla(prefijas));
            Igual("DECL,Int,_W;=,0,_W;*,_Y,_Z;+,_X,[2];=,[3],_W", Tabla(tripletas));
            Igual("DECL,Int,,_W;=,0,,_W;*,_Y,_Z,T1;+,_X,T1,T2;=,T2,,_W", Tabla(cuadruplos));
            string anterior = Tabla(cuadruplos);
            generar();
            Igual(anterior, Tabla(cuadruplos));

            codigo.Text = "_A = 20 / 5 - 1\r\n_B = _A + 2";
            generar();
            Igual("/,20,5,T1;-,T1,1,T2;=,T2,,_A;+,_A,2,T3;=,T3,,_B", Tabla(cuadruplos));
            codigo.Clear();
            generar();
            Igual("", Tabla(prefijas));
            Igual("", Tabla(tripletas));
            Igual("", Tabla(cuadruplos));
        }
    }
}
