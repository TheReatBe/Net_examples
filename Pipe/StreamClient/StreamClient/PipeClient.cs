using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace StreamProcess
{
    /// <summary>
    /// Пока приложение активно - оно клиент, при закрытии становится сервером
    /// </summary>
    public class PipeClient
    {
        private static int numClients = 1;

        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "spawnclient")
                {
                    var pipeClient =
                        new NamedPipeClientStream(".", "controlProcessPipe",
                            PipeDirection.InOut, PipeOptions.None,
                            TokenImpersonationLevel.Impersonation);

                    Console.WriteLine("Connecting to server...\n");
                    pipeClient.Connect();

                    var ss = new StreamString(pipeClient);

                    // Проверяем строку подписи сервера
                    if (ss.ReadString() == "I am the one true server!")
                    {
                        // Маркер безопасности клиента, отправляется при первой записи
                        // Отправляем имя файла, содержимое которого возращается сервером
                        ss.WriteString("c:\\testStream.txt");

                        // Выводим файл
                        Console.Write(ss.ReadString());
                    }
                    else
                    {
                        Console.WriteLine("Server could not be verified.");
                    }
                    pipeClient.Close();
                    // Даем клиентскому процессу время для отображения результатов перед закрытием
                    Thread.Sleep(4000);
                }
            }
            else
            {
                Console.WriteLine("\n*** Named pipe client stream with impersonation example ***\n");
                StartClients();
            }
        }

        /// <summary>
        /// Вспомогательная функция для создания клиентских процессов канала pipe
        /// </summary>
        private static void StartClients()
        {
            string currentProcessName = Environment.CommandLine;

            // Удаляем лишние символы при запуске из Visual Studio
            currentProcessName = currentProcessName.Trim('"', ' ');

            currentProcessName = Path.ChangeExtension(currentProcessName, ".exe");
            Process[] plist = new Process[numClients];

            Console.WriteLine("Spawning client processes...\n");

            if (currentProcessName.Contains(Environment.CurrentDirectory))
            {
                currentProcessName = currentProcessName.Replace(Environment.CurrentDirectory, string.Empty);
            }

            // Удаляем лишние символы при запуске из Visual Studio
            currentProcessName = currentProcessName.Replace("\\", string.Empty);
            currentProcessName = currentProcessName.Replace("\"", string.Empty);

            int i;
            for (i = 0; i < numClients; i++)
            {
                // Запускаем данную программу, но с генерирацией клиент-именнованого канала pipe
                plist[i] = Process.Start(currentProcessName, "spawnclient");
            }
            while (i > 0)
            {
                for (int j = 0; j < numClients; j++)
                {
                    if (plist[j] != null)
                    {
                        if (plist[j].HasExited)
                        {
                            Console.WriteLine($"Client process[{plist[j].Id}] has exited.");
                            plist[j] = null;
                            i--;    // Уменьшаем счетчик просмотра процесса
                        }
                        else
                        {
                            Thread.Sleep(250);
                        }
                    }
                }
            }
            Console.WriteLine("\nClient processes finished, exiting.");
        }

    }
}
