using System;
using System.Threading;
using RfidRastroVerde.Driver_Proj;
using RfidRastroVerde.Models_Proj;

namespace RfidRastroVerde.Cli
{
    public static class CliRunner
    {
        public static int Run(string[] args)
        {
            // defaults
            int pollMs = 80;
            int targetGlobal = 100;

            // args: [target] [pollMs]
            if (args != null && args.Length > 0) int.TryParse(args[0], out targetGlobal);
            if (args != null && args.Length > 1) int.TryParse(args[1], out pollMs);

            if (pollMs < 20) pollMs = 20;
            if (targetGlobal <= 0) targetGlobal = 100;

            var done = new ManualResetEvent(false);

            using (var mgr = new RfidReaderManager())
            {
                // logs do manager (já vem com prefixos se você colocou)
                mgr.Log += (m) => Console.Write(m);

                // imprime UMA vez por EPC global
                // (o Manager deve dedupar globalmente antes de disparar)
                mgr.TagUnique += (TagRead t) =>
                {
                    Console.WriteLine(
                        "Leitor #{0} (SN {1}) | Antena {2} | RSSI {3} | EPC {4}",
                        t.ReaderIndex,
                        string.IsNullOrWhiteSpace(t.ReaderSn) ? "-" : t.ReaderSn,
                        t.Antenna,
                        string.IsNullOrWhiteSpace(t.RssiHex) ? "-" : ("0x" + t.RssiHex),
                        t.Epc
                    );
                };

                // progresso global (se existir no Manager)
                mgr.GlobalProgress += (total) =>
                {
                    Console.WriteLine("Progresso: {0}/{1}", total, mgr.TargetGlobal);
                };

                // finalização global
                mgr.GlobalFinished += (count) =>
                {
                    Console.WriteLine("\nMeta global atingida: {0} tags únicas.\n", count);
                    done.Set();
                };

                Console.WriteLine("Detectando e abrindo leitores... (pollMs={0})", pollMs);

                // detecta automaticamente quantos leitores existem
                mgr.OpenAllDetected(pollIntervalMs: pollMs);

                if (mgr.Drivers.Count == 0)
                {
                    Console.WriteLine("Nenhum leitor aberto. Encerrando.");
                    return 2;
                }

                Console.WriteLine("Leitores abertos: {0}", mgr.Drivers.Count);
                Console.WriteLine("Iniciando ciclo global (meta={0})...\n", targetGlobal);

                // ciclo global "fresh": zera globalSeen e garante leitura do zero
                mgr.StartGlobalFresh(targetGlobal);

                Console.WriteLine("Pressione 'Q' para parar manualmente.\n");

                // espera meta ou Q
                while (true)
                {
                    if (done.WaitOne(50))
                        break;

                    if (Console.KeyAvailable)
                    {
                        var k = Console.ReadKey(true);
                        if (k.Key == ConsoleKey.Q)
                        {
                            Console.WriteLine("\nParada manual solicitada.\n");
                            break;
                        }
                    }
                }

                mgr.StopAll();
                Console.WriteLine("StopAll OK. (HID permanece aberto até CloseAll/Dispose)\n");
            }

            return 0;
        }
    }
}
