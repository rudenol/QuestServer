using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using quiz;
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
            quest.LoadFromFile(@"C:\Users\Asura\OneDrive\Desktop\вопросы.txt.txt");
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 8080);
            var HTTPserver = new HTTPserver(quest);//Создаем HTTP сервер передаем группу
            Task httpres = HTTPserver.Run(); // Запускаем сервер HTTP и продолжаем работу
            try
            {
                server.Bind(localEndPoint);
                server.Listen(10);
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (true)
                {
                    Socket handler = server.Accept();
                    Console.WriteLine($"Клиент подключился: {handler.RemoteEndPoint}");

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

                    Console.WriteLine($"Принято от {handler.RemoteEndPoint}: {data}");
                    if (data.Contains("<Load>"))
                    {
                        quest.LoadFromFile(@"C:\Users\Asura\OneDrive\Desktop\вопросы.txt.txt");
                        msg = Encoding.Unicode.GetBytes("OK");
                        handler.Send(msg);
                        Console.WriteLine("Викторина загружена из сервера...");
                    }

                    if (data.Contains("<Sort>"))
                    {
                        quest.Sort(delegate (Questions q1, Questions q2)
                        {
                            return q1.Level.CompareTo(q2.Level);
                        });
                        msg = Encoding.Unicode.GetBytes("OK");
                        handler.Send(msg);
                    }

                    if (data.Contains("<DeliteQuestion>"))
                    {
                        string term = data.Substring(16);
                        quest.DeliteQ(delegate (Questions q)
                        {
                            return q.ContainsTerm(term);
                        }, " ");
                        msg = Encoding.Unicode.GetBytes("OK");
                        handler.Send(msg);
                    }

                    if (data.Contains("<OutputQuestion>"))
                    {
                        string q = quest[currentIndex].OutputQ();
                        msg = Encoding.Unicode.GetBytes(q);
                        handler.Send(msg);
                    }

                    if (data.Contains("<CheckAnswer>"))
                    {
                        string message = data.Substring(13);
                        string[] parts = message.Split('|');

                        int index = int.Parse(parts[0]);
                        string answer = parts[1];

                        bool result = quest[index].CheckA(answer);

                        handler.Send(Encoding.Unicode.GetBytes(result.ToString()));
                        Console.WriteLine($"Ответ {(result ? "верный!" : "неверный :(")}" );
                    }

                    if (data.Contains("<Length>"))
                    {
                        handler.Send(Encoding.Unicode.GetBytes(quest.Length.ToString()));
                    }

                    if (data.Contains("<GetQuestion>"))
                    {
                        int i = int.Parse(data.Substring(13));
                        Questions q = quest[i];

                        string message;

                        if (q is TMultiQuestion mq)
                        {
                            message = "M|" + mq.OutputQ() + "|" + string.Join(";", mq.Answers);
                        }
                        else
                        {
                            message = "S|" + q.OutputQ() + "|";
                        }

                        handler.Send(Encoding.Unicode.GetBytes(message));
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