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

        virtual public void DeliteQ(FilterDelegate del, string term)
        {
            questions.RemoveAll(q => del(q));
        }

        virtual public void Sort(SortDelegate del)
        {
            questions.Sort((q1, q2) => del(q1, q2));
        }

        public virtual void LoadFromFile(string filename)
        {
            questions.Clear();
            using (StreamReader reader = new StreamReader(filename))
            {
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

        public virtual int Length => questions.Count;

        public virtual Questions this[int i] => i >= 0 && i < Length ? questions[i] : null;

        public IEnumerator GetEnumerator()
        {
            return questions.GetEnumerator();
        }

        public virtual QuestionView GetQuestionView(int index)
        {
            Questions q = this[index];

            if (q is TMultiQuestion mq)
                return new QuestionView
                {
                    Text = mq.OutputQ(),
                    IsMulti = true,
                    Answers = mq.Answers
                };

            return new QuestionView
            {
                Text = q.OutputQ(),
                IsMulti = false,
                Answers = null
            };
        }
        public virtual bool CheckAnswer(int index, string answer)
        {
            return questions[index].CheckA(answer);
        }
    }

    public class QuestionView
    {
        public string Text;
        public bool IsMulti;
        public string[] Answers;
    }

    class ClientQuest : Quest
    {
        protected Socket client;
        public ClientQuest(string _address, int _port)
        {
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(_address), _port);
            client.Connect(serverEndPoint);
        }

        public override QuestionView GetQuestionView(int index)
        {
            string msg = "<GetQuestion>" + index;
            client.Send(Encoding.Unicode.GetBytes(msg));

            byte[] buf = new byte[8192];
            int len = client.Receive(buf);

            string data = Encoding.Unicode.GetString(buf, 0, len);
            return ParseQuestionView(data);
        }
        private QuestionView ParseQuestionView(string data)
        {
            string[] parts = data.Split('|');

            return new QuestionView
            {
                IsMulti = parts[0] == "M",
                Text = parts[1],
                Answers = parts.Length > 2 && parts[2] != ""
                    ? parts[2].Split(';')
                    : null
            };
        }

        public override int Length
        {
            get
            {
                string message = "<Length>";
                client.Send(Encoding.Unicode.GetBytes(message));

                byte[] buffer = new byte[1024];
                int n = client.Receive(buffer);

                return int.Parse(Encoding.Unicode.GetString(buffer, 0, n));
            }
        }

        public override Questions this[int i]
        {
            get
            {
                string message = "<GetStringQuest>" + i;
                client.Send(Encoding.Unicode.GetBytes(message));

                byte[] buffer = new byte[4096];
                int n = client.Receive(buffer);
                string data = Encoding.Unicode.GetString(buffer, 0, n);

                if (TQuestion.TryParse(data, out var q) ||
                    TMultiQuestion.TryParse(data, out q))
                    return q;

                return null;
            }
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

        override public void DeliteQ(FilterDelegate del, string term)
        {
            string message = "<DeliteQuestion>" + term;
            byte[] msg = Encoding.Unicode.GetBytes(message);
            client.Send(msg);

            byte[] bytes = new byte[10240];
            int bytesRec = client.Receive(bytes);
            string data = Encoding.Unicode.GetString(bytes, 0, bytesRec);
        }

        public string OutputQ()
        {
            string message = "<OutputQuestion>";
            byte[] msg = Encoding.Unicode.GetBytes(message);
            client.Send(msg);

            byte[] bytes = new byte[10240];
            int bytesRec = client.Receive(bytes);
            string data = Encoding.Unicode.GetString(bytes, 0, bytesRec);
            return data;
        }

        public override bool CheckAnswer(int index, string answer)
        {
            string message = "<CheckAnswer>" + index + "|" + answer;
            client.Send(Encoding.Unicode.GetBytes(message));

            byte[] buf = new byte[1024];
            int len = client.Receive(buf);

            return bool.Parse(Encoding.Unicode.GetString(buf, 0, len));
        }
    }
}