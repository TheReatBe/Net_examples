using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace StreamProcess
{
    public class PipeServer
    {
        private static int numThreads = 1;

        public static void Main()
        {
            int i;
            Thread[] servers = new Thread[numThreads];

            Console.WriteLine("\n*** Named pipe server stream with impersonation example ***\n");
            Console.WriteLine("Waiting for client connect...\n");
            for (i = 0; i < numThreads; i++)
            {
                servers[i] = new Thread(ServerThread);
                servers[i].Start();
            }
            Thread.Sleep(250);
            while (i > 0)
            {
                for (int j = 0; j < numThreads; j++)
                {
                    if (servers[j] != null)
                    {
                        if (servers[j].Join(250))
                        {
                            Console.WriteLine("Server thread[{0}] finished.", servers[j].ManagedThreadId);
                            servers[j] = null;
                            i--;    // Уменьшаем счетчик просмотра процесса
                        }
                    }
                }
            }
            Console.WriteLine("\nServer threads exhausted, exiting.");
        }

        private static void ServerThread(object data)
        {
            NamedPipeServerStream pipeServer =
                new NamedPipeServerStream("controlProcessPipe", PipeDirection.InOut, numThreads);

            int threadId = Thread.CurrentThread.ManagedThreadId;

            // Ожидание подключения клиента
            pipeServer.WaitForConnection();

            Console.WriteLine("Client connected on thread[{0}].", threadId);
            try
            {
                // читаем запрос от клиента. Как только клиент
                // сделает запись в канал pipe его токен безопасности будет доступен

                StreamString ss = new StreamString(pipeServer);

                // Проверем индентификацию для подключенного клиента,
                // используя строку, которую проверяет клиент
                ss.WriteString("I am the one true server!");
                string filename = ss.ReadString();

                // Читаем содержимое файла, выдавая себя за клиента
                ReadFileToStream fileReader = new ReadFileToStream(ss, filename);

                // Отображаем имя клиента, которого иммитируем
                Console.WriteLine("Reading file: {0} on thread[{1}] as user: {2}.",
                    filename, threadId, pipeServer.GetImpersonationUserName());
                pipeServer.RunAsClient(fileReader.Start);
            }
            // Перехват исключения IOException если канал pipe сломан
            // или отключен
            catch (IOException e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
            }
            pipeServer.Close();
        }
    }
}

