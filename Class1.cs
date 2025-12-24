using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using quiz;
using System.Text.RegularExpressions;
using System.IO;
namespace QuestServer 
{ 
    internal class HTTPserver 
    { 
        HttpListener HttpListener; 
        Quest quest;
        private int searchIndex = 0; //Для поиска нужного типа вопросов
        private int currentQuestionIndex = -1; //Текущий вопрос
        private int correctAnswers = 0;

        private int shownTQuestions = 0;
        private int shownTMultiQuestions = 0;

        private const int MAX_T = 2;
        private const int MAX_MULTI = 2;

        private bool quizStarted = false;

        public HTTPserver(Quest _quest, string url = "http://localhost:8081/") 
        { 
            HttpListener = new HttpListener(); 
            HttpListener.Prefixes.Add(url); 
            quest = _quest;

            quest.Sort(delegate (Questions q1, Questions q2)
            {
                return q1.Level.CompareTo(q2.Level);
            });
        } 
        
        public async Task Run() 
        { 
            HttpListener.Start(); 
            Console.WriteLine("HTTP Сервер запущен на http://localhost:8081/"); 
            try 
            { 
                while (true) 
                {
                    var context = await HttpListener.GetContextAsync();
                    var tsk = Task.Run(async delegate () {
                        try { await ProcessRequestAsync(context); }
                        catch (Exception ex) { Console.WriteLine($"Ошибка: {ex.Message}"); }
                    });
                } 
            } 
            finally { HttpListener.Stop(); } 
        } 
        
        public async Task ProcessRequestAsync(HttpListenerContext httpContext) 
        { 
            var req = httpContext.Request; 
            var res = httpContext.Response; 
            Console.WriteLine($"{req.HttpMethod} {req.Url?.PathAndQuery}"); 
            try 
            {
                if (req.Url.AbsolutePath == "/favicon.ico")
                {
                    res.StatusCode = 404;
                    res.Close();
                    return;
                }
                if (req.HttpMethod == "GET")
                {
                    if (!quizStarted)
                        await ShowStartPage(res);
                    else
                        await ShowQuestion(res, "");
                }
                else if (req.HttpMethod == "POST")
                {
                    if (!quizStarted)
                        await HandleStartPost(req, res); 
                    else
                        await HandleAnswer(req, res); 
                }
            } 
            catch (Exception e) 
            { 
                Console.WriteLine($"ОШИБКА,{e.Message}"); 
            } 
            finally 
            { 
                res.Close(); 
            } 
        }

        private async Task ShowStartPage(HttpListenerResponse res)
        {
            string html = @"<html><head><title>Настройка викторины</title></head><body>
            <h2>Перед началом викторины</h2><form method='post'>
            <p>Удалить вопросы с заданным термином:</p>
            <input type='text' name='term' /><br><br><input type='submit' value='Начать викторину' />
            </form></body></html>";

            await WriteHtml(res, html);
        }


        private async Task HandleStartPost(HttpListenerRequest req, HttpListenerResponse res)
        {
            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                body = await reader.ReadToEndAsync();

            string term = "";
            if (body.StartsWith("term="))
                term = SimpleUrlDecode(body.Substring(5));

            if (term != "")
            {
                quest.DeliteQ(delegate (Questions q)
                {
                    return q.ContainsTerm(term);
                });
            }

            quizStarted = true;
            await ShowQuestion(res, "");
        }

        private async Task ShowQuestion(HttpListenerResponse res, string message)
        {
            Questions q = null;

            while (searchIndex < quest.Length)
            {
                var candidate = quest[searchIndex];

                if (candidate is TQuestion && shownTQuestions < MAX_T)
                {
                    q = candidate;
                    shownTQuestions++;
                    currentQuestionIndex = searchIndex;
                    searchIndex++;
                    break;
                }

                if (candidate is TMultiQuestion && shownTMultiQuestions < MAX_MULTI)
                {
                    q = candidate;
                    shownTMultiQuestions++;
                    currentQuestionIndex = searchIndex;
                    searchIndex++;
                    break;
                }

                searchIndex++;
            }

            if (q == null)
            {
                await WriteHtml(res,
                    $"<html><body><h2>Викторина завершена.<br>Правильных ответов: {correctAnswers}</h2></body></html>");
                return;
            }

            string html = "<html><head><title>!!ВИКТОРИНА!!</title></head><body>";
            html += $"<h3>{q.Text}</h3>";

            if (q is TMultiQuestion qm)
            {
                html += "<ul>";
                foreach (var v in AnswerList(qm))
                    html += $"<li>{v}</li>";
                html += "</ul>";
            }

            html += @"<form method='post'><input type='text' name='answer'/>
            <br><br><input type='submit' value='Ответить'/></form>";

            if (!string.IsNullOrEmpty(message))
                html += $"<p><b>{message}</b></p>";

            html += "</body></html>";

            await WriteHtml(res, html);
        }


        private async Task HandleAnswer(HttpListenerRequest req, HttpListenerResponse res)
        {
            if (currentQuestionIndex < 0)
            {
                await ShowQuestion(res, "");
                return;
            }

            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                body = await reader.ReadToEndAsync();

            string answer = body.StartsWith("answer=")
                ? SimpleUrlDecode(body.Substring(7)) : "";

            bool isCorrect = quest[currentQuestionIndex].CheckA(answer);

            if (isCorrect) correctAnswers++;

            await ShowQuestion(res, isCorrect ? "Ответ верный!" : "Ответ неверный :(");
        }

        public static string[] AnswerList(TMultiQuestion q)
        {
            Random rnd = new Random();
            string[] done = new string[0];
            for (int i = 0; i < q.Answers.Length; i++)
            {
                string a = q.Answers[rnd.Next(0, q.Answers.Length)];
                if (!done.Contains(a))
                {
                    Array.Resize(ref done, done.Length + 1);
                    done[done.Length - 1] = a;
                }
                else
                {
                    while (done.Contains(a)) a = q.Answers[rnd.Next(0, q.Answers.Length)];
                    Array.Resize(ref done, done.Length + 1);
                    done[done.Length - 1] = a;
                }
            }
            return done;
        }

        private async Task WriteHtml(HttpListenerResponse res, string html) 
        { 
            byte[] buffer = Encoding.UTF8.GetBytes(html); 
            res.ContentType = "text/html; charset=utf-8"; 
            res.ContentLength64 = buffer.Length; 
            await res.OutputStream.WriteAsync(buffer, 0, buffer.Length); 
        } 
        
        string SimpleUrlDecode(string input)
        { 
            var bytes = new List<byte>(); 
            for (int i = 0; i < input.Length; i++) 
            { 
                if (input[i] == '%') 
                { 
                    string hex = input.Substring(i + 1, 2); 
                    bytes.Add(Convert.ToByte(hex, 16)); 
                    i += 2; 
                } 
                else if (input[i] == '+') 
                { 
                    bytes.Add((byte)' '); 
                } 
                else 
                { 
                    bytes.Add((byte)input[i]); 
                } 
            } 
            return Encoding.UTF8.GetString(bytes.ToArray()); 
        } 
    } 
}
