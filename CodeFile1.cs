using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace hw2_3
{
    // Делегат для фильтрации вопросов (для удаления)
    public delegate bool FilterDelegate(Questions q);

    // Делегат для сортировки
    public delegate int SortDelegate(Questions a, Questions b);

    public class Questions
    {
        protected string textOfQuestion;
        protected string correctAnswer;
        protected int level;

        public string Text
        {
            get => textOfQuestion;
        }

        public string Answer
        {
            get => correctAnswer;
        }

        public int Level
        {
            get => level;
        }

        public Questions(string _textOfQuestion = "", string _correctAnswer = "", int _level = 0)
        {
            textOfQuestion = _textOfQuestion;
            correctAnswer = _correctAnswer;
            level = _level;
        }

        public string OutputQ()
        {
            return textOfQuestion;
        }

        public bool CheckA(string answer)
        {
            if (correctAnswer == answer) return true;
            else return false;
        }

        public bool ContainsTerm(string term)
        {
            term = term.Substring(0, term.Length - 2).ToLower();
            return textOfQuestion.ToLower().Contains(term);
        }
    }

    public class TQuestion : Questions
    {
        public TQuestion(string _textOfQuestion, string _correctAnswer, int _level) : base(_textOfQuestion, _correctAnswer, _level)
        {

        }

        public static bool TryParse(string s, out Questions mq)
        {
            mq = null;
            string[] elem = s.Split('?');
            string _textOfQuestion = elem[0] + "?";
            string[] param = elem[1].Split(';');
            bool res = param.Length == 2;

            if (res)
            {
                string _correctAnswer = param[0].Trim();
                int _level = int.Parse(param[1].Trim());

                mq = new TQuestion(_textOfQuestion, _correctAnswer, _level);
            }
            return res;
        }

        public override string ToString()
        {
            return textOfQuestion + $" {level}";
        }
    }

    public class TMultiQuestion : Questions
    {
        protected string[] answers;
        public string[] Answers => answers;
        public TMultiQuestion(string _textOfQuestion, string _correctAnswer, int _level, string[] _answers) : base(_textOfQuestion, _correctAnswer, _level)
        {
            answers = _answers;
        }

        public static bool TryParse(string s, out Questions mq)
        {
            mq = null;
            string[] elem = s.Split('?');
            string _textOfQuestion = elem[0] + "?";
            string[] param = elem[1].Split(';');
            int _level = int.Parse(param[param.Length - 1].Trim());
            bool res = param.Length > 2;

            if (res)
            {
                string[] _answers = new string[0];
                for (int i = 0; i < param.Length - 1; i++)
                {
                    Array.Resize(ref _answers, _answers.Length + 1);
                    _answers[_answers.Length - 1] = param[i].Trim();
                }
                string _correctAnswer = _answers[0].Trim();

                mq = new TMultiQuestion(_textOfQuestion, _correctAnswer, _level, _answers);
            }
            return res;
        }

        public override string ToString()
        {
            return textOfQuestion + " " + string.Join("; ", answers) + $" {level}";
        }
    }

    class Quest : IEnumerable
    {
        public List<Questions> questions = new List<Questions>();

        public void Add(Questions q)
        {
            questions.Add(q);
        }

        virtual public void DeliteQ(FilterDelegate del)
        {
            questions.RemoveAll(q => del(q));
        }

        virtual public void Sort(SortDelegate del)
        {
            questions.Sort((q1, q2) => del(q1, q2));
        }

        public virtual void LoadFromFile(string filename)
        {
            using (StreamReader reader = new StreamReader(filename))
            {
                //int level = 0;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (TQuestion.TryParse(line, out var qw) || TMultiQuestion.TryParse(line, out qw)) Add(qw);
                }
            }
        }

        public override string ToString()
        {
            string s = "";
            foreach (var i in questions)
            {
                s += i.ToString() + "\n";
            }
            return s;
        }

        public int Length => questions.Count;

        public Questions this[int i] => i >= 0 && i < Length ? questions[i] : null;

        public IEnumerator GetEnumerator()
        {
            return questions.GetEnumerator();
        }
    }

    //это класс-наследник квеста, который используется, когда мы выбираем работу с сервером
    //в нём переопределяются нужные методы с отправкой message
    class ClientQuest : Quest
    {
        protected Socket client;
        public ClientQuest(string _address, int _port)
        {
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(_address), _port);
            client.Connect(serverEndPoint);
        }

        override public void LoadFromFile(string filestring)
        {
            string message = "<Load>";
            byte[] msg = Encoding.Unicode.GetBytes(message);
            client.Send(msg);

            byte[] bytes = new byte[10240];
            int bytesRec = client.Receive(bytes);
            string data = Encoding.Unicode.GetString(bytes, 0, bytesRec);
        }

        override public void Sort(SortDelegate del)
        {
            string message = "<Sort>";
            byte[] msg = Encoding.Unicode.GetBytes(message);
            client.Send(msg);

            byte[] bytes = new byte[10240];
            int bytesRec = client.Receive(bytes);
            string data = Encoding.Unicode.GetString(bytes, 0, bytesRec);
        }

        override public void DeliteQ(FilterDelegate del)
        {
            string message = "<DeliteQuestion>";
            byte[] msg = Encoding.Unicode.GetBytes(message);
            client.Send(msg);

            byte[] bytes = new byte[10240];
            int bytesRec = client.Receive(bytes);
            string data = Encoding.Unicode.GetString(bytes, 0, bytesRec);
        }

        //классы ниже переопределить не получается, потому что методы не в квесте, они не найдены.
        //как тогда с ними работать?
        override public string OutputQ()
        {
            string message = "<OutputQuestion>";
            byte[] msg = Encoding.Unicode.GetBytes(message);
            client.Send(msg);

            byte[] bytes = new byte[10240];
            int bytesRec = client.Receive(bytes);
            string data = Encoding.Unicode.GetString(bytes, 0, bytesRec);
            return data;
        }

        override public bool CheckA()
        {
            string message = "<CheckAnswer>";
            byte[] msg = Encoding.Unicode.GetBytes(message);
            client.Send(msg);

            byte[] bytes = new byte[10240];
            int bytesRec = client.Receive(bytes);
            bool data = Convert.ToBoolean(Encoding.Unicode.GetString(bytes, 0, bytesRec));
            return data;
        }
    }
}