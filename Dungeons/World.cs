using System;
using System.IO;
using System.Collections.Generic;
using WorldFileParser;
using WorldFileBuilder;

namespace InteractiveFiction
{
    public delegate void Command(string param);

    class World
    {
        private string worldName;
        private Player player;
        private List<Room> rooms;

        public World(string file)
        {
            player = new Player();

            List<Node> nodes = WorldParser.parseFile(file);
            worldName = nodes[0].name;
            rooms = WorldBuilder.build(player, nodes[0].children);

            Command look = s =>
            {
                if (s.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
                    player.commands["examine"](s.Substring(3).Trim());
                else if (DirectionUtils.isDirection(s))
                    Console.WriteLine(player.room.getWall(s).look());
                else
                    Console.WriteLine(player.room.look());
            };

            

            Command move = s =>
            {
                Direction d;
                try
                {
                    d = DirectionUtils.parse(s);
                }
                catch
                {
                    Console.WriteLine("I don't think that is a valid direction :P");
                    return;
                }

                Wall wall = player.room.getWall(d);
                if (wall == null || wall.door == null || !wall.door.visible)
                    Console.WriteLine("You have nowhere to go.");
                else if (wall.door.locked)
                    Console.WriteLine("This door is locked. Find a key.");
                else
                {
                    player.room = wall.door.room;
                    look("");
                }
            };

            Command take = s =>
            {
                if (s == "")
                {
                    Console.WriteLine("Take what?");
                    return;
                }

                Item i = player.room.getItem(s);
                if (i == null)
                {
                    if (player.getItem(s) != null)
                        Console.WriteLine("This item is already equipped.");
                    else
                        Console.WriteLine("You can't see such an item.");
                    return;
                }

                if (!i.visible)
                {
                    Console.WriteLine("You can't see such an item.");
                    return;
                }

                if (!i.takeable)
                {
                    Console.WriteLine("Sorry you can't take this.");
                    return;
                }

                player.addItem(i);
                player.room.removeItem(i);

                Console.WriteLine(char.ToUpper(i.name[0]) + i.name.Substring(1) + " taken.");
            };

            Command examine = s =>
            {
                if (s == "")
                {
                    Console.WriteLine("Examine what?");
                    return;
                }

                Item i = player.getItem(s);

                if (i == null)
                {
                    i = player.room.getItem(s);

                    if (i == null || !i.visible)
                    {
                        Console.WriteLine("You can't see such an item.");
                        return;
                    }
                }

                if (!i.visible)
                {
                    Console.WriteLine("You can't see such an item.");
                    return;
                }

                Command c = i.getCommand("examine");
                if (c != null)
                    c("");

                Console.WriteLine(i.examine());
            };

            Command inventory = s =>
            {
                if (player.inventory.Count == 0)
                {
                    Console.WriteLine("Your inventory is empty.");
                    return;
                }

                Console.WriteLine("Your inventory:");

                foreach (Item i in player.inventory)
                    Console.WriteLine("  - " + i.name);
            };

            Command use = s =>
            {
                string[] ps = s.Split(new string[] { " on " }, StringSplitOptions.RemoveEmptyEntries);

                if (ps.Length == 0)
                {
                    Console.WriteLine("Use what?");
                    return;
                }

                Item i = player.getItem(ps[0].Trim());
                if (i == null)
                {
                    i = player.room.getItem(ps[0].Trim());

                    if (i == null || !i.visible)
                    {
                        Console.WriteLine("You can't see such an item.");
                        return;
                    }

                    if (!i.useIfNotEquipped)
                    {
                        Console.WriteLine("This item is not equipped.");
                        return;
                    }
                }

                if (!i.visible)
                {
                    Console.WriteLine("You can't see such an item.");
                    return;
                }

                Command c = i.getCommand("use");

                if (c == null)
                {
                    Console.WriteLine("You can't use this item.");
                    return;
                }

                string p = "";
                for (int a = 1; a < ps.Length; a++)
                    p += ps[a].Trim();

                c(p);
            };

            Command help = s =>
            {
                foreach (string c in player.commands.Keys)
                {
                    if (c == "?" || c == "help")
                        continue;

                    Console.WriteLine(c);
                }

                foreach (Item i in player.inventory)
                {
                    foreach (string c in i.commands.Keys)
                    {
                        if (!player.commands.ContainsKey(c))
                            Console.WriteLine(c);
                    }
                }
            };

            player.commands.Add("look", look);
            player.commands.Add("move", move);
            player.commands.Add("go", move);
            player.commands.Add("take", take);
            player.commands.Add("examine", examine);
            player.commands.Add("inventory", inventory);
            player.commands.Add("use", use);
            player.commands.Add("help", help);
            player.commands.Add("?", help);
        }

        public void play()
        {
            Console.WriteLine("Welcome to " + worldName + "!\n(c) Roi Atalla\n");

            Console.WriteLine("Type any command when prompted. If you need help, type 'help'. To specify a target when using an item, use the following syntax: 'use <item> on <target>'. Good luck!!\n\n");

            if(player.intro != null)
                Console.WriteLine(player.intro + "\n");

            player.commands["look"]("");

            while (true)
            {
                Console.Write("\nCommand: ");
                string input = Console.ReadLine().Trim();

                if (input.StartsWith("exit", StringComparison.OrdinalIgnoreCase))
                    break;
                else
                {
                    int i = input.IndexOf(' ');

                    string c;
                    if (i == -1)
                        c = input;
                    else
                        c = input.Substring(0, i).Trim();

                    c = c.ToLower();

                    string param = i >= 0 ? input.Substring(i + 1).Trim() : "";

                    try
                    {
                        player.commands[c](param);
                    }
                    catch (KeyNotFoundException e)
                    {
                        try
                        {
                            string[] prms = param.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            if (prms.Length == 0)
                            {
                                Console.WriteLine("Invalid arguments");
                                continue;
                            }

                            Item it = player.getItem(prms[0]);
                            if (it == null)
                            {
                                it = player.room.getItem(prms[0]);

                                if (it == null || !it.visible)
                                    Console.WriteLine("There is no such item.");
                                else if(it != null && it.visible)
                                {
                                    if (it.getCommand(c) == null)
                                        throw new KeyNotFoundException();

                                    Console.WriteLine("You don't have that item equipped.");
                                }
                                continue;
                            }

                            if (!it.visible)
                            {
                                Console.WriteLine("There is no such item.");
                                return;
                            }

                            param = "";
                            foreach (string s in prms)
                                param += s + " ";

                            Command cmd = it.getCommand(c);

                            if (cmd == null)
                                throw new KeyNotFoundException();

                            cmd(param.Trim());
                        }
                        catch (KeyNotFoundException ex)
                        {
                            Console.WriteLine("What? I can't understand you. I only speak English.");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: " + e.Message + e.StackTrace);
                    }
                }
            }

            Console.WriteLine("\nBye!");
        }
    }

    class Player
    {
        public Player()
        {
            inventory = new List<Item>();
            commands = new Dictionary<String, Command>();
        }

		public string intro { get; set; }

		public Room room { get; set; }

        public void addItem(Item i)
        {
			inventory.Add(i);
        }

        public Item getItem(string name)
        {
			foreach (Item i in inventory)
                if (i.Equals(name))
                    return i;
            return null;
        }

        public bool removeItem(Item i)
        {
			return inventory.Remove(i);
        }

		public List<Item> inventory { get; private set; }

		public Dictionary<String, Command> commands { get; private set; }
    }

    class Room
    {
		private Wall center;

        public Room(string name)
        {
            this.name = name;

            north = new Wall(this, "north");
            east = new Wall(this, "east");
            south = new Wall(this, "south");
            west = new Wall(this, "west");

            center = new Wall(this, "center");
        }

        public List<Wall> getWalls()
        {
            List<Wall> walls = new List<Wall>();
            walls.Add(north);
            walls.Add(south);
            walls.Add(east);
            walls.Add(west);
            return walls;
        }

        public Wall getWall(Direction direction)
        {
            switch (direction)
            {
                case Direction.NORTH: return north;
                case Direction.SOUTH: return south;
                case Direction.EAST: return east;
                case Direction.WEST: return west;
                default: return null;
            }
        }

        public Wall getWall(string direction)
        {
            try
            {
                return getWall(DirectionUtils.parse(direction));
            }
            catch
            {
                return null;
            }
        }

        public void setWall(Direction direction, Wall wall)
        {
            switch (direction)
            {
                case Direction.NORTH: north = wall; break;
                case Direction.SOUTH: south = wall; break;
                case Direction.EAST: east = wall; break;
                case Direction.WEST: west = wall; break;
            }
        }

		public Wall north { get; set; }

        public Wall south  { get; set; }

		public Wall east { get; set; }

		public Wall west { get; set; }

		public string name { get; set; }

		public string description { get; set; }

        public void addItem(Item item)
        {
            center.addItem(item);
        }

        public void addAll(List<Item> items)
        {
            center.addAll(items);
        }

        public Item getItem(string name)
        {
            Item it = center.getItem(name);
            if (it != null)
                return it;

            foreach (Wall w in getWalls())
            {
                Item i = w.getItem(name);
                if (i != null)
                    return i;
            }

            if (name.ToLower().Contains("door"))
            {
                if (name.Equals("door", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Which door?");
                    Item i = new Item(null, "");
                    i.description = "";
                    return i;
                }
                else
                {
                    string[] s = name.Split(' ');
                    if (s.Length > 1 && s[1].Equals("door", StringComparison.OrdinalIgnoreCase))
                    {
                        Wall w;
                        try
                        {
                            w = getWall(s[0]);
                        }
                        catch
                        {
                            Console.WriteLine("No such direction.");
                            return new Item(null, "\b");
                        }

                        return w.door;
                    }
                }
            }

            return null;
        }

        public bool removeItem(Item i)
        {
            if (center.removeItem(i))
                return true;
            else
            {
                foreach (Wall w in getWalls())
                    if (w.removeItem(i))
                        return true;
            }

            return false;
        }

        public int visibleItemCount()
        {
            return center.visibleItemCount();
        }

        public List<Item> Items
        {
            get { return center.items; }
            set { center.addAll(value); }
        }

        public string look()
        {
            string look = "";

            look += "You are in " + name + ".";

            if (description != null)
                look += " " + description;

            if(center.visibleItemCount() > 0)
            {
                look += '\n' + center.look();

                look = look.Replace("To the center", "In the center of the room");
            }

            foreach (Wall w in getWalls())
                if(w.visibleItemCount() > 0 || w.door != null)
                    look += '\n' + w.look();

            return look;
        }
    }

    class Wall
    {
        public Wall(Room room, string name)
        {
            parentRoom = room;
            this.name = name;
            try
            {
                direction = DirectionUtils.parse(name);
            }
            catch { }

            items = new List<Item>();
        }

		public string name { get; private set; }

		public Direction direction { get; private set; }

        public void addItem(Item item)
        {
            items.Add(item);
        }

        public void addAll(List<Item> items)
        {
            this.items.AddRange(items);
        }

        public Item getItem(string name)
        {
            foreach (Item i in items)
                if (i.Equals(name))
                    return i;

            foreach (Item i in items)
            {
                Item it = i.getChild(name);
                if (it != null)
                    return it;
            }

            return null;
        }

        public bool removeItem(Item i)
        {
            if (items.Remove(i))
                return true;
            else
            {
                foreach (Item it in items)
                    if (it.removeChild(i))
                        return true;
            }

            return false;
        }

        public int visibleItemCount()
        {
            int count = 0;
            foreach (Item i in items)
                if (i.visible)
                    count++;
            return count;
        }

		public List<Item> items { get; set; }

		public Room parentRoom { get; set; }

		public Door door { get; set; }

        public string look()
        {
            string look = "";

            int visibleCount = visibleItemCount();
            if (visibleCount > 0 || (door != null && door.visible))
            {
                look += "To the " + name.ToLower() + ", you see ";

                if (door != null && door.visible)
                {
                    look += "a door ";

                    if (visibleCount > 0)
                        look += "and ";
                    else
                        look += "\b.";
                }

                bool and = false;
                if (visibleCount > 0)
                {
                    for (int a = 0; a < items.Count; a++)
                    {
                        Item i = items[a];
                        if (i.visible)
                        {
                            if (and)
                            {
                                if (a < items.Count - 1)
                                    look += ", ";
                                else if (items.Count == 2)
                                    look += " and ";
                                else
                                    look += ", and ";
                            }
                            else
                                and = true;

                            look += i.look();
                        }
                    }

                    look += '.';

                    foreach (Item i in items)
                        if (i.visible && i.visibleItemCount() > 0)
                            look += '\n' + i.lookChildren();
                }

                look += '\n';
            }

            return look;
        }
    }

    class Item
    {
        private List<string> aliases;

        public Item(Wall parent, string name)
        {
            this.parent = parent;
            this.name = name;

            children = new List<Item>();
            aliases = new List<string>();

            commands = new Dictionary<string, Command>();
        }

		public bool takeable { get; set; }

		public bool useIfNotEquipped { get; set; }

		public bool visible { get; set; }

		public Wall parent { get; set; }

		public string name { get; set; }

        public bool Equals(string name)
        {
            if (this.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (string n in aliases)
                if (n.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            
            return false;
        }

		private string _description;

		public string description
		{
			get { return _description; }
			set
			{
				_description = value;

				if (detail == null)
					detail = value;
			}
		}

		public string detail { get; set; }

		public Dictionary<string, Command> commands { get; private set; }

        public void addCommand(string s, Command c)
        {
            commands.Add(s, c);
        }

        public Command getCommand(string s)
        {
            try
            {
                return commands[s];
            }
            catch
            {
                return null;
            }
        }

        public int visibleItemCount()
        {
            int count = 0;
            foreach (Item i in children)
                if (i.visible)
                    count++;
            return count;
        }

        public void addAlias(string alias)
        {
            aliases.Add(alias);
        }

        public void addChild(Item i)
        {
            children.Add(i);
        }

        public void addAll(List<Item> items)
        {
            children.AddRange(items);
        }

        public Item getChild(string name)
        {
            foreach (Item i in children)
            {
                if (i.Equals(name))
                    return i;

                Item it = i.getChild(name);
                if (it != null)
                    return it;
            }

            return null;
        }

        public bool removeChild(Item i)
        {
            if (children.Remove(i))
                return true;
            else
            {
                foreach (Item it in children)
                    if (it.removeChild(i))
                        return true;
            }

            return false;
        }

		public List<Item> children { get; private set; }

        public string look()
        {
            if (!visible)
                throw new InvalidOperationException();

            return description;
        }

        public string lookChildren()
        {
            if (!visible)
                throw new InvalidOperationException();

            string look = "";

            if (visibleItemCount() > 0)
                look += "On the " + char.ToLower(name[0]) + name.Substring(1) + ", you see ";
            else
                return look;

            bool and = false;
            foreach (Item i in children)
                if (i.visible)
                {
                    if (and)
                        look += " and ";
                    else
                        and = true;

                    look += i.look();
                }

            look += '.';

            foreach (Item i in children)
                if (i.visible && i.visibleItemCount() > 0)
                    look += "\n" + i.lookChildren();

            return look;
        }

        public string examine()
        {
            if (!visible || detail == null)
                throw new InvalidOperationException();

            if (detail == "")
                return lookChildren();
            
            return char.ToUpper(detail[0]) + detail.Substring(1) + ". " + lookChildren();
        }
    }

    class Door : Item
    {
        public Door(Wall parent, string target)
            : base(parent, target)
        {
            update();
        }

		public string name
		{
			get { return base.name; }
			set { base.name = value; update(); }
		}

        private void update()
        {
            if (locked)
                description = detail = "Locked door";
            else
                description = detail = "Door leading to " + name + "";
        }

		public Room room { get; set; }

		private bool _locked;

		public bool locked
		{
			get { return _locked; }
			set
			{
				_locked = value;
				update();
			}
		}
    }

    interface Instruction
    {
        void execute(Player p, Item origin, Item target);
    }

    class InstructionFactory
    {
        private class Print : Instruction
        {
            private string s;

            public Print(string s)
            {
                this.s = s;
            }

            public void execute(Player p, Item origin, Item target)
            {
                string newS = "";
                if (origin != null)
                    newS = s.Replace("%origin%", origin.name).Trim();
                else if (s.Contains("%origin%"))
                    throw new InvalidDataException("No origin specified.");

                if (target != null)
                    newS = s.Replace("%target%", target.name).Trim();
                else if (s.Contains("%target%"))
                    throw new InvalidDataException("No target specified.");

                newS = newS.Replace("\\n", "\n");

                Console.WriteLine(newS);
            }
        }

        private class Remove : Instruction
        {
            public void execute(Player p, Item origin, Item target)
            {
                if(!p.removeItem(origin))
                    p.room.removeItem(origin);
            }
        }

        private class Drop : Instruction
        {
            public void execute(Player p, Item origin, Item target)
            {
                if (!p.removeItem(origin))
                    throw new InvalidDataException("You don't have this item in your inventory.");

                target.addChild(origin);
            }
        }

        private class Take : Instruction
        {
            public void execute(Player p, Item origin, Item target)
            {
                if (p.inventory.Contains(origin))
                    throw new InvalidDataException("You already have this item in your inventory.");

                p.addItem(origin);
                p.room.removeItem(origin);
            }
        }

        private class Variable : Instruction
        {
            private object value;

            public Variable(object startValue)
            {
                value = startValue;
            }

            public object Value
            {
                get { return value; }
                set { this.value = value; }
            }

            public void execute(Player p, Item origin, Item target)
            {}
        }

        private class IncrementVariable : Instruction
        {
            private Variable v;

            public IncrementVariable(Variable v)
            {
                this.v = v;
            }

            public void execute(Player p, Item origin, Item target)
            {
                v.Value = (int)v.Value + 1;
            }
        }

        private class DecrementVariable : Instruction
        {
            private Variable v;

            public DecrementVariable(Variable v)
            {
                this.v = v;
            }

            public void execute(Player p, Item origin, Item target)
            {
                v.Value = (int)v.Value - 1;
            }
        }

        private class ModifyVariable : Instruction
        {
            private Variable v;
            private int newValue;

            public ModifyVariable(Variable v, int newValue)
            {
                this.v = v;
                this.newValue = newValue;
            }

            public void execute(Player p, Item origin, Item target)
            {
                v.Value = newValue;
            }
        }

        private class Conditional : Instruction
        {
            private Variable v;
            private object value;
            private Command c;

            public Conditional(Variable v, object value, Command c)
            {
                this.v = v;
                this.value = value;
                this.c = c;
            }

            public void execute(Player p, Item origin, Item target)
            {
                object va;
                if (value is string)
                {
                    if (((string)value).Equals("%origin%", StringComparison.OrdinalIgnoreCase))
                    {
                        if (origin == null)
                            throw new InvalidDataException("No origin specified.");

                        va = origin.name;
                    }
                    else if (((string)value).Equals("%target%", StringComparison.OrdinalIgnoreCase))
                    {
                        if (target == null)
                            throw new InvalidDataException("No target specified.");

                        va = target.name;
                    }
                    else
                        va = value;
                }
                else
                {
                    va = value;
                }

                if (v.Value.Equals(va))
                    c(target == null ? "" : target.name);
                else if (va is string)
                    throw new InvalidDataException("Invalid target.");
            }
        }

        private class Assignment : Instruction
        {
            private string room, item, field;
            private bool value;

            public Assignment(string item, string field, bool value)
            {
                string[] parts = item.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                room = parts[0].Trim();
                this.item = parts[1].Trim();
                this.field = field;
                this.value = value;
            }

            public void execute(Player p, Item origin, Item target)
            {
                Item i;
                if (room.Equals("%origin%", StringComparison.OrdinalIgnoreCase))
                {
                    if (origin == null)
                        throw new InvalidDataException("No origin specified.");

                    i = origin;
                }
                else if (room.Equals("%target%", StringComparison.OrdinalIgnoreCase))
                {
                    if (target == null)
                        throw new InvalidDataException("No target specified.");

                    i = target;
                }
                else
                {
                    i = p.getItem(item);
                    if (i == null)
                        i = findItem(p.room);

                    if (i == null)
                        return;
                }

                if (field.Equals("visible",StringComparison.OrdinalIgnoreCase))
                    i.visible = value;
                else if (field.Equals("takeable",StringComparison.OrdinalIgnoreCase))
                    i.takeable = value;

                if (field.Equals("locked", StringComparison.OrdinalIgnoreCase))
                {
                    if (i is Door)
                    {
                        ((Door)i).locked = value;

                        string r = room;
                        r = r.ToLower().Replace("%target%", p.room.name);

                        Console.WriteLine(char.ToUpper(r[0]) + r.Substring(1) + "'s " + ((Door)i).parent.name + " door is " + (value ? "locked." : "unlocked."));
                    }
                    else
                        throw new InvalidDataException("Invalid target specified.");
                }
            }

            private List<Room> visited = new List<Room>();

            private Item findItem(Room room)
            {
                if (visited.Contains(room))
                    return null;

                visited.Add(room);

                if (room.name.Equals(this.room, StringComparison.OrdinalIgnoreCase))
                {
                    if (item.Equals("NorthDoor", StringComparison.OrdinalIgnoreCase))
                        return room.north.door;
                    else if (item.Equals("SouthDoor", StringComparison.OrdinalIgnoreCase))
                        return room.south.door;
                    else if (item.Equals("WestDoor", StringComparison.OrdinalIgnoreCase))
                        return room.west.door;
                    else if (item.Equals("EastDoor", StringComparison.OrdinalIgnoreCase))
                        return room.east.door;

                    return room.getItem(item);
                }
                else
                {
                    foreach (Wall w in room.getWalls())
                    {
                        if (w.door != null)
                        {
                            Item i = findItem(w.door.room);
                            if (i != null)
                                return i;
                        }
                    }
                }

                return null;
            }
        }

        private class GameOver : Instruction
        {
            private bool win;

            public GameOver(bool win)
            {
                this.win = win;
            }

            public void execute(Player p, Item origin, Item target)
            {
                Console.WriteLine("You " + (win ? "win!" : "lose!"));

                Console.Write("Press ENTER to exit... ");

                Console.Read();

                Environment.Exit(0);
            }
        }

        public static Instruction createPrint(string s)
        {
            return new Print(s);
        }

        public static Instruction createRemove()
        {
            return new Remove();
        }

        public static Instruction createDrop()
        {
            return new Drop();
        }

        public static Instruction createTake()
        {
            return new Take();
        }

        public static Instruction createVariable(object startValue)
        {
            return new Variable(startValue);
        }

        public static Instruction incrementVariable(Instruction variable)
        {
            if (!(variable is Variable))
                throw new InvalidCastException("This instruction is not a variable.");

            return new IncrementVariable((Variable)variable);
        }

        public static Instruction decrementVariable(Instruction variable)
        {
            if (!(variable is Variable))
                throw new InvalidCastException("This instruction is not a variable.");

            return new DecrementVariable((Variable)variable);
        }

        public static Instruction modifyVariable(Instruction variable, int newValue)
        {
            if (!(variable is Variable))
                throw new InvalidCastException("This instruction is not a variable.");

            return new ModifyVariable((Variable)variable, newValue);
        }

        public static Instruction createConditional(Instruction variable, object value, Command c)
        {
            if (!(variable is Variable))
                throw new InvalidCastException("This instruction is not a variable.");

            return new Conditional((Variable)variable, value, c);
        }

        public static Instruction createAssignment(string item, string field, bool value)
        {
            return new Assignment(item,field,value);
        }

        public static Instruction createGameOver(bool win)
        {
            return new GameOver(win);
        }
    }

    enum Direction
    {
        NORTH, SOUTH, EAST, WEST, NONE
    }

    class DirectionUtils
    {
        public static bool isDirection(string name)
        {
            try
            {
				parse(name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static Direction parse(string name)
        {
            return (Direction)Enum.Parse(typeof(Direction), name, true);
        }
    }
}
