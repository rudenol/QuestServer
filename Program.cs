using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using hw2_3;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace QuestServer
{
    internal class Program
    {
        static Quest quest = new Quest();
        static void Main(string[] args)
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 8080);
            quest.LoadFromFile(@"C:\Users\Asura\OneDrive\Desktop\вопросы.txt.txt");
            try
            {
                server.Bind(localEndPoint);
                server.Listen(10);
                Console.WriteLine("Сервер запущеню Ожидание подключений...");

                while (true)
                {
                    Socket handler = server.Accept();
                    Console.WriteLine($"Клиент подключился: {server.RemoteEndPoint}");

                    Thread clientTread = new Thread(delegate () { HandlerClient(handler); });
                    clientTread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                server.Close();
            }
        }

        public static void HandlerClient(Socket handler)
        {
            byte[] bytes = new byte[1024];
            string data = null;
            int currentIndex = 0;

            try
            {
                while (true)
                {
                    int bytesRec = handler.Receive(bytes);
                    data = Encoding.Unicode.GetString(bytes, 0, bytesRec);

                    byte[] msg;

                    //здесь должны приниматься сообщения, вызываться методы, и что-то должно отправляться обратно,
                    //но я не понимаю, как это правильно организовать. Здесь просто набросок основной мысли

                    Console.WriteLine($"Принято от {handler.RemoteEndPoint}: {data}");
                    if (data.Contains("<Load>"))
                    {
                        quest.LoadFromFile(@"C:\Users\Asura\OneDrive\Desktop\вопросы.txt.txt");
                        Console.WriteLine("Викторина загружена из сервера...");
                    }
                    if (data.Contains("<Sort>"))
                    {
                        quest.Sort(delegate (Questions q1, Questions q2)
                        {
                            return q1.Level.CompareTo(q2.Level);
                        });
                    }
                    if (data.Contains("<DeliteQuestion>"))
                    {
                        quest.DeliteQ(delegate (Questions q)
                        {
                            return q.ContainsTerm(term);
                        });
                    }
                    if (data.Contains("<OutputQuestion>"))
                    {
                        quest[currentIndex].OutputQ();
                    }
                    if (data.Contains("<CheckAnswer>"))
                    {
                        quest[currentIndex].CheckA(answer);
                    }
                }
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
            finally
            {
                Console.WriteLine($"Клиент отключился: {handler.RemoteEndPoint}");
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }
    }
}