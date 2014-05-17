using System;
using System.IO;
using System.Collections.Generic;

namespace WorldFileParser
{
    class WorldParser
    {
        public static List<Node> parseFile(string file)
        {
            StreamReader reader = new StreamReader(file);

            string data = reader.ReadToEnd();

            reader.Close();

            data = removeComments(data);

            data = data.Replace("\t", "").Replace("\r", "").Replace("\n", "");

            return parseNode(data);
        }

        private static string removeComments(string data)
        {
            int i;
            do
            {
                i = data.IndexOf("//");

                if (i < 0)
                    continue;

                int end = data.IndexOf("\n", i + 2);

                if (end < 0)
                    end = data.Length;

                data = data.Replace(data.Substring(i, end - i + 1), "");
            } while (i >= 0);

            return data;
        }

        private static List<Node> parseNode(string data)
        {
            List<Node> nodes = new List<Node>();

            int index = 0;
            while (index < data.Length)
            {
                int pair = data.IndexOf('=', index);
                int brace = data.IndexOf('{', index);
                if (pair != -1 && (pair < brace || brace == -1))
                {
                    int sc = data.IndexOf(';', index);

                    if (sc == -1)
                        throw new InvalidDataException("No semicolon found.");

                    string s = data.Substring(index, sc - index);
                    int p = s.IndexOf("=");

                    nodes.Add(new Pair(s.Substring(0,p).Trim(), s.Substring(p+1).Trim()));

                    index = sc + 1;
                }
                else if (brace != -1 && (pair > brace || pair == -1))
                {
                    Node node;

                    int braceNum = 1, open = data.IndexOf('{', index), closed = open + 1;

                    for (; closed < data.Length; closed++)
                    {
                        char c = data[closed];
                        if (c == '{')
                            braceNum++;
                        else if (c == '}')
                            braceNum--;

                        if (braceNum == 0)
                            break;
                    }

                    if (closed == data.Length)
                        throw new InvalidDataException("No closing bracket found.");

                    node = new Node(data.Substring(index, open - index).Trim());
                    node.addAll(parseNode(data.Substring(open + 1, closed - open - 1).Trim()));

                    index = closed + 1;

                    nodes.Add(node);
                }
                else
                    throw new InvalidDataException("Invalid data detected.");
            }

            return nodes;
        }
    }

    class Node
    {
        public Node(string name)
        {
            this.name = name;
			children = new List<Node>();
        }

		public string name { get; set; }

        public void addValue(Pair value)
        {
            children.Add(value);
        }

        public void addAll(List<Pair> pairs)
        {
            children.AddRange(pairs);
        }

        public void addAll(List<Node> nodes)
        {
            children.AddRange(nodes);
        }

		public List<Node> children { get; private set; }
    }

    class Pair : Node
    {
        public Pair(string name, string value)
            : base(name)
        {
            this.value = value;
        }

		public string value { get; set; }
    }
}
